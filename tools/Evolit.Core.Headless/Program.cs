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

        if (string.Equals(args[0], "benchmark", StringComparison.OrdinalIgnoreCase))
        {
            var count = args.Length >= 2 ? int.Parse(args[1]) : 10_000;
            var ticks = args.Length >= 3 ? int.Parse(args[2]) : 1000;
            var seed = args.Length >= 4 ? args[3] : "evolit-benchmark";
            RunBenchmark(count, ticks, seed);
            return 0;
        }

        Console.Error.WriteLine("Usage: verify | benchmark [organisms] [ticks] [seed] | benchmark-all [ticks] [seed]");
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
    var hashA = SnapshotHash.Compute(a.CaptureSnapshot());
    var hashB = SnapshotHash.Compute(b.CaptureSnapshot());
    if (hashA != hashB)
        throw new InvalidOperationException($"Same-seed determinism failed: {hashA:X16} != {hashB:X16}");
}

static void AssertSaveRestoreDeterminism()
{
    var source = ScenarioFactory.Create("restore", 5_000);
    source.Step(5_000);
    var checkpoint = source.CaptureSnapshot();
    var serialized = CoreSnapshotSerializer.Serialize(checkpoint);
    var roundTrip = CoreSnapshotSerializer.Deserialize(serialized);

    source.Step(5_000);
    var expected = SnapshotHash.Compute(source.CaptureSnapshot());

    var restored = CoreSimulation.Restore(roundTrip);
    restored.Step(5_000);
    var actual = SnapshotHash.Compute(restored.CaptureSnapshot());
    if (expected != actual)
        throw new InvalidOperationException($"Save/restore determinism failed: {expected:X16} != {actual:X16}");
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

    for (var index = 0; index < ids.Length; index += 2)
    {
        if (!store.Remove(ids[index]))
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
    sim.Step(1000);
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

    var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
    var samples = new double[ticks];
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
    public static CoreSimulation Create(string seedText, int organismCount)
    {
        const int radius = 40;
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
