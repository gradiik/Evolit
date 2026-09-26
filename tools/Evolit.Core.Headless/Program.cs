using System.Diagnostics;
using System.Reflection;
using Evolit.Core;

return ProgramMain(args);

static int ProgramMain(string[] args)
{
    try
    {
        if (args.Length == 0 || string.Equals(args[0], "verify", StringComparison.OrdinalIgnoreCase))
        {
            RunVerification();
            return 0;
        }

        if (string.Equals(args[0], "benchmark-all", StringComparison.OrdinalIgnoreCase))
        {
            var ticks = args.Length >= 2 ? int.Parse(args[1]) : 1000;
            var seed = args.Length >= 3 ? args[2] : "evolit-benchmark";
            foreach (var count in new[] { 1_000, 5_000, 10_000, 25_000, 50_000 })
                RunBenchmark(count, ticks, seed);
            return 0;
        }

        if (string.Equals(args[0], "worldgen-verify", StringComparison.OrdinalIgnoreCase))
        {
            RunWorldgenVerification();
            return 0;
        }
        if (string.Equals(args[0], "worldgen-seeds", StringComparison.OrdinalIgnoreCase))
        {
            RunWorldgenSeeds();
            return 0;
        }
        if (string.Equals(args[0], "worldgen-summary", StringComparison.OrdinalIgnoreCase))
        {
            RunWorldgenSummary(args.Length >= 2 ? args[1] : "evolit-summary");
            return 0;
        }
        if (string.Equals(args[0], "worldgen-benchmark", StringComparison.OrdinalIgnoreCase))
        {
            RunWorldgenBenchmark();
            return 0;
        }

        if (string.Equals(args[0], "environment-verify", StringComparison.OrdinalIgnoreCase))
        {
            RunEnvironmentVerification();
            return 0;
        }

        if (string.Equals(args[0], "bootstrap-test", StringComparison.OrdinalIgnoreCase))
        {
            RunBootstrapTest();
            return 0;
        }

        if (string.Equals(args[0], "environment-benchmark", StringComparison.OrdinalIgnoreCase))
        {
            var ticks = args.Length >= 2 ? int.Parse(args[1]) : 1000;
            foreach (var radius in new[] { 18, 40, 58, 80 })
                RunEnvironmentBenchmark(radius, ticks, "environment-benchmark");
            return 0;
        }

        if (string.Equals(args[0], "benchmark", StringComparison.OrdinalIgnoreCase))
        {
            var count = args.Length >= 2 ? int.Parse(args[1]) : 10_000;
            var ticks = args.Length >= 3 ? int.Parse(args[2]) : 1000;
            var seed = args.Length >= 4 ? args[3] : "evolit-benchmark";
            RunBenchmark(count, ticks, seed);
            return 0;
        }

        Console.Error.WriteLine("Usage: verify | worldgen-verify | worldgen-seeds | worldgen-summary [seed] | worldgen-benchmark | environment-verify | bootstrap-test | environment-benchmark [ticks] | benchmark [organisms] [ticks] [seed] | benchmark-all [ticks] [seed]");
        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex);
        return 1;
    }
}

static void RunVerification()
{
    AssertNoGodotDependency();
    AssertDeterministicReplay();
    AssertSaveRestoreDeterminism();
    AssertGeneticDeterminism();
    AssertOrganismStore();
    AssertEnvironment();
    RunEnvironmentVerification();
    Console.WriteLine("VERIFY PASS");
}

static void AssertNoGodotDependency()
{
    var refs = typeof(CoreSimulation).Assembly.GetReferencedAssemblies();
    if (refs.Any(name => name.Name?.StartsWith("Godot", StringComparison.OrdinalIgnoreCase) == true))
        throw new InvalidOperationException("Evolit.Core must not reference Godot assemblies.");
}

static void AssertDeterministicReplay()
{
    var a = ScenarioFactory.Create("determinism", 10_000);
    var b = ScenarioFactory.Create("determinism", 10_000);
    a.Step(10_000);
    b.Step(10_000);
    var snapshotA = CoreSnapshotSerializer.Serialize(a.CaptureSnapshot());
    var snapshotB = CoreSnapshotSerializer.Serialize(b.CaptureSnapshot());
    if (!string.Equals(snapshotA, snapshotB, StringComparison.Ordinal))
        throw new InvalidOperationException("Same-seed determinism failed: complete snapshots differ.");
}

static void AssertSaveRestoreDeterminism()
{
    var source = ScenarioFactory.Create("restore", 5_000);
    source.Step(5_000);
    var checkpoint = source.CaptureSnapshot();
    var serialized = CoreSnapshotSerializer.Serialize(checkpoint);
    var roundTrip = CoreSnapshotSerializer.Deserialize(serialized);

    source.Step(5_000);
    var expected = CoreSnapshotSerializer.Serialize(source.CaptureSnapshot());

    var restored = CoreSimulation.Restore(roundTrip);
    restored.Step(5_000);
    var actual = CoreSnapshotSerializer.Serialize(restored.CaptureSnapshot());
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        throw new InvalidOperationException("Save/restore determinism failed: complete snapshots differ.");
}

static void AssertGeneticDeterminism()
{
    var storeA = new GenomeStore();
    var storeB = new GenomeStore();
    var parent = Genome.Create(0.5f, 0.4f, 0.6f, 0.5f, 0.7f, 0.8f, 0.3f);
    var parentA = storeA.Add(parent);
    var parentB = storeB.Add(parent);
    var rngA = new DeterministicRandom(12345);
    var rngB = new DeterministicRandom(12345);
    var childA = GenomeInheritance.CreateOffspring(storeA, parentA, null, ref rngA, new MutationSettings(1f, 0.05f));
    var childB = GenomeInheritance.CreateOffspring(storeB, parentB, null, ref rngB, new MutationSettings(1f, 0.05f));
    if (storeA.Get(childA) != storeB.Get(childB))
        throw new InvalidOperationException("Genetic determinism failed.");

    var rngDifferent = new DeterministicRandom(54321);
    var childDifferent = GenomeInheritance.CreateOffspring(storeB, parentB, null, ref rngDifferent, new MutationSettings(1f, 0.05f));
    if (storeA.Get(childA) == storeB.Get(childDifferent))
        throw new InvalidOperationException("Different mutation streams unexpectedly produced the same genome.");
}

static void AssertOrganismStore()
{
    var store = new OrganismStore();
    var cell = CellId.FromAxial(0, 0);
    var genome = new GenomeId(1);
    var lineage = new LineageId(1);
    var ids = new OrganismId[10_000];
    for (var index = 0; index < ids.Length; index++)
        ids[index] = store.Create(cell, genome, lineage);

    var movedCell = CellId.FromAxial(1, -1);
    if (!store.SetCell(ids[1], movedCell) ||
        !store.SetEnergyHealth(ids[1], 0.25f, 0.75f) ||
        !store.TryGet(ids[1], out var updated) ||
        updated.CellId != movedCell ||
        Math.Abs(updated.Energy - 0.25f) > 0.00001f ||
        Math.Abs(updated.Health - 0.75f) > 0.00001f)
    {
        throw new InvalidOperationException("Organism update/lookup failed.");
    }

    for (var index = 0; index < ids.Length; index += 2)
    {
        if (!store.Remove(ids[index]) || store.TryGet(ids[index], out _))
            throw new InvalidOperationException("Organism removal failed.");
    }

    var seen = new HashSet<OrganismId>();
    for (var index = 1; index < ids.Length; index += 2)
    {
        if (!store.TryGet(ids[index], out var state) || state.Id != ids[index])
            throw new InvalidOperationException("Stable organism ID was corrupted by dense compaction.");
        if (!seen.Add(state.Id))
            throw new InvalidOperationException("Duplicate organism ID after dense compaction.");
    }

    for (var index = 0; index < 5_000; index++)
    {
        var id = store.Create(cell, genome, lineage);
        if (!seen.Add(id))
            throw new InvalidOperationException("Duplicate organism ID after add/remove cycle.");
    }

    if (store.Count != 10_000)
        throw new InvalidOperationException($"Unexpected organism count after add/remove cycle: {store.Count}.");
}

static void AssertEnvironment()
{
    var sim = ScenarioFactory.Create("environment", 100);
    var replay = ScenarioFactory.Create("environment", 100);
    sim.Step(1000);
    replay.Step(1000);
    if (!string.Equals(
            CoreSnapshotSerializer.Serialize(sim.CaptureSnapshot()),
            CoreSnapshotSerializer.Serialize(replay.CaptureSnapshot()),
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Environment initialization/update is not deterministic.");
    }

    var center = CellId.FromAxial(0, 0);
    if (sim.Topology.GetNeighbors(center).Length != 6)
        throw new InvalidOperationException("Hex topology neighbor lookup failed for center cell.");

    foreach (var id in sim.Topology.Cells)
    {
        var cell = sim.Environment.Get(id);
        if (!float.IsFinite(cell.TemperatureCelsius) ||
            !float.IsFinite(cell.Humidity) ||
            !float.IsFinite(cell.PressureKPa) ||
            cell.Humidity is < 0f or > 1f ||
            cell.SubstrateDevelopment is < 0f or > 1f)
        {
            throw new InvalidOperationException($"Invalid environment values in cell {id}.");
        }
    }
}



static void RunWorldgenVerification()
{
    if (WorldGenerationScale.SmallRadius < WorldGenerationScale.LegacySmallRadius * 2 ||
        WorldGenerationScale.MediumRadius < WorldGenerationScale.LegacyMediumRadius * 2 ||
        WorldGenerationScale.LargeRadius < WorldGenerationScale.LegacyLargeRadius * 2)
    {
        throw new InvalidOperationException("0.0.8 world-size presets are not at least 2x their legacy linear extent.");
    }

    var settings = new WorldGenerationSettings("worldgen-determinism", WorldGenerationScale.SmallRadius);
    var a = ProceduralWorldGenerator.Generate(settings);
    var b = ProceduralWorldGenerator.Generate(settings);
    var other = ProceduralWorldGenerator.Generate(settings with { Seed = "worldgen-other" });

    if (!GeneratedWorldEquivalent(a, b))
        throw new InvalidOperationException("Same seed/settings produced different generated cell state.");
    if (GeneratedWorldEquivalent(a, other, compareSettings: false))
        throw new InvalidOperationException("Different seeds produced identical generated cell state.");

    var aBootstrap = BootstrapGeneratedWorld(a, settings.Seed);
    var bBootstrap = BootstrapGeneratedWorld(b, settings.Seed);
    var otherBootstrap = BootstrapGeneratedWorld(other, "worldgen-other");

    if (!string.Equals(aBootstrap.Snapshot, bBootstrap.Snapshot, StringComparison.Ordinal))
        throw new InvalidOperationException("World generation + bootstrap is not deterministic.");
    if (string.Equals(aBootstrap.Snapshot, otherBootstrap.Snapshot, StringComparison.Ordinal))
        throw new InvalidOperationException("Different worldgen seeds produced identical stabilized snapshots.");
    ValidateGeneratedWorld(a);
    ValidateGeneratedWorld(other);

    var edgeSettings = new[]
    {
        new WorldGenerationSettings("edge-low-cold-calm", WorldGenerationScale.SmallRadius, WorldLandAmount.Low, WorldClimate.Cold, GeologicalActivity.Calm),
        new WorldGenerationSettings("edge-high-warm-active", WorldGenerationScale.SmallRadius, WorldLandAmount.High, WorldClimate.Warm, GeologicalActivity.Active),
        new WorldGenerationSettings("edge-medium-active", WorldGenerationScale.MediumRadius, WorldLandAmount.Normal, WorldClimate.Temperate, GeologicalActivity.Active)
    };
    foreach (var edge in edgeSettings)
        ValidateGeneratedWorld(ProceduralWorldGenerator.Generate(edge));

    Console.WriteLine($"WORLDGEN VERIFY PASS bootstrap_ticks={aBootstrap.Ticks} converged={aBootstrap.Converged}");
}

static bool GeneratedWorldEquivalent(GeneratedWorld a, GeneratedWorld b, bool compareSettings = true)
{
    if (a.Cells.Length != b.Cells.Length)
        return false;
    if (compareSettings && !a.Settings.Equals(b.Settings))
        return false;

    for (var i = 0; i < a.Cells.Length; i++)
    {
        var x = a.Cells[i];
        var y = b.Cells[i];
        if (x.Id != y.Id ||
            x.ElevationMeters != y.ElevationMeters ||
            x.WaterDepthMeters != y.WaterDepthMeters ||
            x.TemperatureCelsius != y.TemperatureCelsius ||
            x.Humidity != y.Humidity ||
            x.PressureKPa != y.PressureKPa ||
            x.MineralPotential != y.MineralPotential ||
            x.NutrientPotential != y.NutrientPotential ||
            x.GeothermalPotential != y.GeothermalPotential ||
            x.Substrate != y.Substrate ||
            x.DrainageTarget != y.DrainageTarget ||
            x.FlowAccumulation != y.FlowAccumulation ||
            x.Slope != y.Slope ||
            x.IsRiver != y.IsRiver ||
            x.IsLake != y.IsLake)
            return false;
    }

    return true;
}

static void RunWorldgenSeeds()
{
    for (var i = 0; i < 20; i++)
    {
        var seed = $"seed-{i:00}";
        var radius = i switch
        {
            < 12 => WorldGenerationScale.SmallRadius,
            < 18 => WorldGenerationScale.MediumRadius,
            _ => WorldGenerationScale.LargeRadius
        };
        var world = ProceduralWorldGenerator.Generate(new WorldGenerationSettings(seed, radius));
        ValidateGeneratedWorld(world);
        var bootstrap = BootstrapGeneratedWorld(world, seed);
        PrintWorldgenSummary(seed, world, bootstrap);
    }
    Console.WriteLine("WORLDGEN SEEDS PASS");
}

static void RunWorldgenSummary(string seed)
{
    var world = ProceduralWorldGenerator.Generate(new WorldGenerationSettings(seed, WorldGenerationScale.MediumRadius));
    ValidateGeneratedWorld(world);
    var bootstrap = BootstrapGeneratedWorld(world, seed);
    PrintWorldgenSummary(seed, world, bootstrap);
}

static void RunWorldgenBenchmark()
{
    foreach (var radius in new[] { WorldGenerationScale.SmallRadius, WorldGenerationScale.MediumRadius, WorldGenerationScale.LargeRadius })
    {
        GC.Collect();
        var before = GC.GetTotalMemory(true);
        var world = ProceduralWorldGenerator.Generate(new WorldGenerationSettings($"bench-{radius}", radius));
        var bootstrap = BootstrapGeneratedWorld(world, $"bench-{radius}");
        var memory = Math.Max(0, GC.GetTotalMemory(false) - before);
        var m = world.Metrics;
        Console.WriteLine(
            $"WORLDGEN_BENCH radius={radius} cells={world.Cells.Length} " +
            $"topology_ms={m.TopologyMs:0.###} macro_elevation_ms={m.MacroElevationMs:0.###} " +
            $"coast_bathymetry_ms={m.CoastBathymetryMs:0.###} geology_ms={m.GeologyMs:0.###} " +
            $"hydrology_ms={m.HydrologyMs:0.###} climate_ms={m.ClimateMs:0.###} " +
            $"resources_ms={m.ResourcesMs:0.###} environment_build_ms={m.EnvironmentBuildMs:0.###} " +
            $"generation_total_ms={m.TotalMs:0.###} core_construct_ms={bootstrap.CoreConstructionMs:0.###} " +
            $"bootstrap_ms={bootstrap.BootstrapMs:0.###} bootstrap_ticks={bootstrap.Ticks} converged={bootstrap.Converged} memory_delta={memory}");
    }
}

static void ValidateGeneratedWorld(GeneratedWorld world)
{
    var s = world.Summary;
    if (s.Cells <= 0 || s.LandRatio < 0.20f || s.LandRatio > 0.75f)
        throw new InvalidOperationException($"Invalid land ratio {s.LandRatio:0.###}.");
    if (s.LargestContinentCells <= 0)
        throw new InvalidOperationException("World lacks coherent land.");
    var landCells = Math.Max(1, (int)Math.Round(s.Cells * s.LandRatio));
    if (s.LargestContinentCells < landCells * 0.16)
        throw new InvalidOperationException("World land is excessively fragmented.");
    if (s.MountainCells <= 0)
        throw new InvalidOperationException("World has no mountain/highland structure.");
    if (s.RiverCells <= 0)
        throw new InvalidOperationException("World has no readable river network.");

    var boundaryCells = 0;
    var boundaryLand = 0;
    foreach (var cell in world.Cells)
    {
        if (world.Topology.GetNeighbors(cell.Id).Length >= 6)
            continue;
        boundaryCells++;
        if (cell.ElevationMeters >= 0f)
            boundaryLand++;
    }
    if (boundaryCells > 0 && boundaryLand > boundaryCells * 0.10)
        throw new InvalidOperationException("Too much land reaches the finite world boundary.");

    foreach (var cell in world.Cells)
    {
        if (!float.IsFinite(cell.ElevationMeters) ||
            !float.IsFinite(cell.WaterDepthMeters) || cell.WaterDepthMeters < 0 ||
            !float.IsFinite(cell.TemperatureCelsius) ||
            !float.IsFinite(cell.Humidity) || cell.Humidity is < 0f or > 1f ||
            !float.IsFinite(cell.PressureKPa) || cell.PressureKPa <= 0 ||
            cell.MineralPotential is < 0f or > 1f ||
            cell.NutrientPotential is < 0f or > 1f ||
            cell.GeothermalPotential is < 0f or > 1f)
            throw new InvalidOperationException($"Invalid generated cell {cell.Id}.");
        if (cell.DrainageTarget >= 0 &&
            world.Cells[cell.DrainageTarget].ElevationMeters > cell.ElevationMeters + 0.001f)
            throw new InvalidOperationException($"Uphill drainage from {cell.Id}.");
    }
}

static (string Snapshot, int Ticks, bool Converged, double FinalChange, double CoreConstructionMs, double BootstrapMs) BootstrapGeneratedWorld(GeneratedWorld world, string seed)
{
    const int minimumTicks = 200;
    const int maximumTicks = 2_000;
    const int checkInterval = 100;
    const double threshold = 0.0025;

    var coreStart = Stopwatch.GetTimestamp();
    var simulation = new CoreSimulation(
        world.Topology,
        world.Environment,
        SeedMixer.FromString(seed),
        SimulationMode.Bootstrap);
    var coreConstructionMs = Stopwatch.GetElapsedTime(coreStart).TotalMilliseconds;
    var bootstrapStart = Stopwatch.GetTimestamp();
    var previous = simulation.Environment.CaptureSnapshot();
    var finalChange = double.PositiveInfinity;
    var ticks = 0;
    var converged = false;

    for (; ticks < maximumTicks; ticks += checkInterval)
    {
        simulation.Step(checkInterval);
        finalChange = simulation.Environment.MeasureChange(previous);
        AssertPhysicalBounds(simulation);
        previous = simulation.Environment.CaptureSnapshot();
        if (ticks + checkInterval >= minimumTicks && finalChange <= threshold)
        {
            ticks += checkInterval;
            converged = true;
            break;
        }
    }

    simulation.Mode = SimulationMode.Live;
    var bootstrapMs = Stopwatch.GetElapsedTime(bootstrapStart).TotalMilliseconds;
    return (
        CoreSnapshotSerializer.Serialize(simulation.CaptureSnapshot()),
        Math.Min(ticks, maximumTicks),
        converged,
        finalChange,
        coreConstructionMs,
        bootstrapMs);
}

static void PrintWorldgenSummary(
    string seed,
    GeneratedWorld world,
    (string Snapshot, int Ticks, bool Converged, double FinalChange, double CoreConstructionMs, double BootstrapMs) bootstrap)
{
    var s = world.Summary;
    var largestPct = s.Cells == 0 ? 0 : s.LargestContinentCells * 100.0 / s.Cells;
    Console.WriteLine(
        $"WORLDGEN seed={seed} radius={world.Settings.Radius} cells={s.Cells} land={s.LandRatio:P1} water={(1f-s.LandRatio):P1} " +
        $"components={s.LandComponents} largest={largestPct:0.0}% islands={s.IslandCount} " +
        $"mountains={s.MountainCells} rivers={s.RiverCells} lakes={s.LakeCells} " +
        $"elevation={s.MinElevationMeters:0}..{s.MaxElevationMeters:0}m max_water={s.MaxWaterDepthMeters:0}m " +
        $"temp={s.MinTemperatureCelsius:0.0}..{s.MaxTemperatureCelsius:0.0}C humidity={s.MinHumidity:P0}..{s.MaxHumidity:P0} " +
        $"generation_ms={world.Metrics.TotalMs:0.###} core_ms={bootstrap.CoreConstructionMs:0.###} bootstrap_ms={bootstrap.BootstrapMs:0.###} bootstrap_ticks={bootstrap.Ticks} " +
        $"converged={bootstrap.Converged} final_change={bootstrap.FinalChange:0.######}");
}

static void RunEnvironmentVerification()
{
    var a = ScenarioFactory.Create("physical-environment", 0, 24);
    var b = ScenarioFactory.Create("physical-environment", 0, 24);
    var different = ScenarioFactory.Create("physical-environment-other", 0, 24);
    a.Step(2_000);
    b.Step(2_000);
    different.Step(2_000);

    var aJson = CoreSnapshotSerializer.Serialize(a.CaptureSnapshot());
    var bJson = CoreSnapshotSerializer.Serialize(b.CaptureSnapshot());
    var differentJson = CoreSnapshotSerializer.Serialize(different.CaptureSnapshot());
    if (!string.Equals(aJson, bJson, StringComparison.Ordinal))
        throw new InvalidOperationException("Physical environment replay is not deterministic.");
    if (string.Equals(aJson, differentJson, StringComparison.Ordinal))
        throw new InvalidOperationException("Different seeds produced identical physical environments.");

    AssertPhysicalBounds(a);

    var checkpoint = CoreSnapshotSerializer.Deserialize(CoreSnapshotSerializer.Serialize(a.CaptureSnapshot()));
    a.Step(1_000);
    var expected = CoreSnapshotSerializer.Serialize(a.CaptureSnapshot());
    var restored = CoreSimulation.Restore(checkpoint);
    restored.Step(1_000);
    if (!string.Equals(expected, CoreSnapshotSerializer.Serialize(restored.CaptureSnapshot()), StringComparison.Ordinal))
        throw new InvalidOperationException("Physical environment save/restore continuation diverged.");

    Console.WriteLine("ENVIRONMENT VERIFY PASS");
}

static void RunBootstrapTest()
{
    var a = ScenarioFactory.Create("bootstrap", 0, 32);
    var b = ScenarioFactory.Create("bootstrap", 0, 32);
    a.Mode = SimulationMode.Bootstrap;
    b.Mode = SimulationMode.Bootstrap;
    var before = a.Environment.CaptureSnapshot();
    a.Step(2_500);
    b.Step(2_500);
    var middle = a.Environment.CaptureSnapshot();
    var firstChange = a.Environment.MeasureChange(before);
    a.Step(2_500);
    b.Step(2_500);
    var secondChange = a.Environment.MeasureChange(middle);
    if (!string.Equals(CoreSnapshotSerializer.Serialize(a.CaptureSnapshot()), CoreSnapshotSerializer.Serialize(b.CaptureSnapshot()), StringComparison.Ordinal))
        throw new InvalidOperationException("Bootstrap replay diverged.");
    AssertPhysicalBounds(a);
    if (!double.IsFinite(firstChange) || !double.IsFinite(secondChange))
        throw new InvalidOperationException("Bootstrap stabilization metric is not finite.");
    if (secondChange > firstChange)
        throw new InvalidOperationException($"Bootstrap did not stabilize: first={firstChange:0.######}, second={secondChange:0.######}.");
    Console.WriteLine($"BOOTSTRAP PASS first_change={firstChange:0.######} second_change={secondChange:0.######} decreasing=True final_cells={a.Topology.Count}");
}

static void AssertPhysicalBounds(CoreSimulation sim)
{
    foreach (var id in sim.Topology.Cells)
    {
        var cell = sim.Environment.Get(id);
        var physical = sim.Environment.GetPhysical(id);
        if (!float.IsFinite(cell.TemperatureCelsius) || cell.TemperatureCelsius is < -80f or > 65f ||
            !float.IsFinite(cell.Humidity) || cell.Humidity is < 0f or > 1f ||
            !float.IsFinite(cell.PressureKPa) || cell.PressureKPa is < 0f or > 115f ||
            !float.IsFinite(cell.WaterDepthMeters) || cell.WaterDepthMeters < 0f ||
            !float.IsFinite(physical.WindX) || !float.IsFinite(physical.WindY) ||
            physical.WaterAvailability is < 0f or > 1f ||
            cell.NutrientPotential is < 0f or > 1f ||
            cell.SubstrateDevelopment is < 0f or > 1f)
            throw new InvalidOperationException($"Physical environment invariant failed in {id}.");
    }
}

static void RunEnvironmentBenchmark(int radius, int ticks, string seed)
{
    var initStart = Stopwatch.GetTimestamp();
    var sim = ScenarioFactory.Create(seed + radius, 0, radius);
    var initMs = Stopwatch.GetElapsedTime(initStart).TotalMilliseconds;
    sim.Step(100);
    var samples = new double[ticks];
    GC.Collect();
    var beforeMemory = GC.GetTotalMemory(true);
    var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
    var totalStart = Stopwatch.GetTimestamp();
    for (var i = 0; i < ticks; i++)
    {
        var start = Stopwatch.GetTimestamp();
        sim.Step();
        samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    var totalMs = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
    var allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocated;
    var memory = Math.Max(0, GC.GetTotalMemory(false) - beforeMemory);
    Array.Sort(samples);
    Console.WriteLine($"ENV_BENCH cells={sim.Topology.Count} ticks={ticks} init_ms={initMs:0.###} avg_ms={(totalMs/ticks):0.######} p50_ms={Percentile(samples,0.5):0.######} p95_ms={Percentile(samples,0.95):0.######} p99_ms={Percentile(samples,0.99):0.######} alloc_per_tick={(allocated/(double)ticks):0.##} memory_delta={memory} cells_per_sec={(sim.Topology.Count*ticks/Math.Max(0.000001,totalMs/1000.0)):0.##}");
}

static void RunBenchmark(int organismCount, int ticks, string seed)
{
    if (organismCount <= 0 || ticks <= 0)
        throw new ArgumentOutOfRangeException("Benchmark counts must be positive.");

    var beforeMemory = GC.GetTotalMemory(true);
    var initStart = Stopwatch.GetTimestamp();
    var sim = ScenarioFactory.Create(seed, organismCount);
    var initMs = Stopwatch.GetElapsedTime(initStart).TotalMilliseconds;
    var afterInitMemory = GC.GetTotalMemory(true);

    sim.Step(100);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // Allocate benchmark bookkeeping before allocation measurement so
    // alloc_per_tick reflects the simulation hot path rather than the harness.
    var samples = new double[ticks];
    var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
    var totalStart = Stopwatch.GetTimestamp();
    for (var index = 0; index < ticks; index++)
    {
        var start = Stopwatch.GetTimestamp();
        sim.Step();
        samples[index] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    var totalMs = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
    var allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocated;

    Array.Sort(samples);
    var avg = totalMs / ticks;
    var p50 = Percentile(samples, 0.50);
    var p95 = Percentile(samples, 0.95);
    var p99 = Percentile(samples, 0.99);
    var memoryDelta = Math.Max(0, afterInitMemory - beforeMemory);
    var bytesPerOrganism = organismCount == 0 ? 0 : memoryDelta / (double)organismCount;

    Console.WriteLine(
        $"BENCH organisms={organismCount} ticks={ticks} init_ms={initMs:0.###} " +
        $"avg_ms={avg:0.######} p50_ms={p50:0.######} p95_ms={p95:0.######} p99_ms={p99:0.######} " +
        $"ticks_per_sec={(ticks / Math.Max(0.000001, totalMs / 1000.0)):0.##} " +
        $"alloc_per_tick={(allocated / (double)ticks):0.##} memory_delta={memoryDelta} bytes_per_organism={bytesPerOrganism:0.##}");
}

static double Percentile(double[] sorted, double p)
{
    if (sorted.Length == 0)
        return 0;
    var index = (int)Math.Ceiling(p * sorted.Length) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
}

static class ScenarioFactory
{
    public static CoreSimulation Create(string seedText, int organismCount, int radius = 40)
    {
        var topologyBuilder = new WorldTopologyBuilder();
        var cells = new List<CellId>();
        var set = new HashSet<CellId>();
        for (var q = -radius; q <= radius; q++)
        {
            var minR = Math.Max(-radius, -q - radius);
            var maxR = Math.Min(radius, -q + radius);
            for (var r = minR; r <= maxR; r++)
            {
                var id = CellId.FromAxial(q, r);
                cells.Add(id);
                set.Add(id);
            }
        }

        var directions = new (int q, int r)[]
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)
        };
        foreach (var id in cells)
        {
            var neighbors = new List<CellId>(6);
            foreach (var direction in directions)
            {
                var neighbor = CellId.FromAxial(id.Q + direction.q, id.R + direction.r);
                if (set.Contains(neighbor))
                    neighbors.Add(neighbor);
            }
            topologyBuilder.Add(id, neighbors);
        }

        var topology = topologyBuilder.Build();
        var environment = new EnvironmentStore(topology);
        var seed = SeedMixer.FromString(seedText);
        var envRandom = new DeterministicRandom(SeedMixer.Combine(seed, 77));
        foreach (var id in topology.Cells)
        {
            var latitude = Math.Abs(id.R) / (float)radius;
            environment.SetInitial(id, new EnvironmentCellState(
                ElevationMeters: (envRandom.NextFloat() - 0.35f) * 2200f,
                WaterDepthMeters: Math.Max(0f, (0.35f - envRandom.NextFloat()) * 800f),
                TemperatureCelsius: 28f - latitude * 18f + (envRandom.NextFloat() - 0.5f) * 5f,
                Humidity: envRandom.NextFloat(),
                PressureKPa: 101.325f,
                LightAvailability: Math.Clamp(1f - latitude * 0.35f, 0f, 1f),
                MineralPotential: 0.35f + envRandom.NextFloat() * 0.6f,
                NutrientPotential: 0.15f + envRandom.NextFloat() * 0.25f,
                OrganicMatter: 0f,
                SubstrateDevelopment: 0f,
                GeothermalPotential: envRandom.NextFloat() * 0.15f,
                Substrate: SubstrateKind.MineralRegolith));
        }

        var simulation = new CoreSimulation(topology, environment, seed);
        var genome = simulation.Genomes.Add(Genome.Create(0.45f, 0.5f, 0.5f, 0.55f, 0.6f, 0.6f, 0.45f));
        var lineage = simulation.Lineages.CreateFounder(genome, simulation.Clock.TickCount);
        ref var random = ref simulation.Random.Organisms;
        for (var index = 0; index < organismCount; index++)
        {
            var cell = topology.GetCellId(random.NextInt(topology.Count));
            simulation.Organisms.Create(cell, genome, lineage, 1f, 1f);
        }
        return simulation;
    }
}

static class SnapshotHash
{
    public static ulong Compute(CoreSimulationSnapshot snapshot)
    {
        var hash = 1469598103934665603UL;
        Add(ref hash, snapshot.TickCount);
        Add(ref hash, BitConverter.DoubleToInt64Bits(snapshot.FixedDeltaSeconds));
        Add(ref hash, (long)snapshot.Mode);

        var organisms = snapshot.Organisms ?? new OrganismStoreSnapshot();
        Add(ref hash, organisms.NextId);
        for (var index = 0; index < organisms.Ids.Length; index++)
        {
            Add(ref hash, organisms.Ids[index].Value);
            Add(ref hash, organisms.Cells[index].Value);
            Add(ref hash, organisms.Genomes[index].Value);
            Add(ref hash, organisms.Lineages[index].Value);
            Add(ref hash, BitConverter.DoubleToInt64Bits(organisms.AgeSeconds[index]));
            Add(ref hash, BitConverter.SingleToInt32Bits(organisms.Energy[index]));
            Add(ref hash, BitConverter.SingleToInt32Bits(organisms.Health[index]));
            Add(ref hash, (byte)organisms.Flags[index]);
        }

        var env = snapshot.Environment ?? new EnvironmentSnapshot();
        for (var index = 0; index < env.Humidity.Length; index++)
        {
            Add(ref hash, BitConverter.SingleToInt32Bits(env.Humidity[index]));
            Add(ref hash, BitConverter.SingleToInt32Bits(env.NutrientPotential[index]));
            Add(ref hash, BitConverter.SingleToInt32Bits(env.OrganicMatter[index]));
        }

        var rng = snapshot.Random ?? new CoreRandomStreamsSnapshot();
        AddState(ref hash, rng.World);
        AddState(ref hash, rng.Genetics);
        AddState(ref hash, rng.Mutation);
        AddState(ref hash, rng.Environment);
        AddState(ref hash, rng.Organisms);
        return hash;
    }

    private static void AddState(ref ulong hash, RandomState state)
    {
        Add(ref hash, unchecked((long)state.S0));
        Add(ref hash, unchecked((long)state.S1));
        Add(ref hash, unchecked((long)state.S2));
        Add(ref hash, unchecked((long)state.S3));
    }

    private static void Add(ref ulong hash, long value)
    {
        unchecked
        {
            var raw = (ulong)value;
            for (var shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(raw >> shift);
                hash *= 1099511628211UL;
            }
        }
    }

    private static void Add(ref ulong hash, int value) => Add(ref hash, (long)value);
    private static void Add(ref ulong hash, byte value) => Add(ref hash, (long)value);
}
