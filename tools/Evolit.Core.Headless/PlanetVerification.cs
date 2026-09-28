using System;
using System.Collections.Generic;
using System.Diagnostics;
using Evolit.Core;

internal static class PlanetVerification
{
    public static void Run()
    {
        AssertGodotPlanetMeshWinding();
        AssertTopologyAtFrequency(PlanetGenerationScale.SmallFrequency);
        AssertTopologyAtFrequency(PlanetGenerationScale.MediumFrequency);
        AssertTopologyAtFrequency(PlanetGenerationScale.LargeFrequency);
        AssertTopologyDeterminism();
        AssertDeterministicGeneration();
        AssertHydrologyAndPhysical();
        AssertSnapshotRoundTrip();
        Console.WriteLine("PLANET VERIFY PASS");
    }

    private static void AssertGodotPlanetMeshWinding()
    {
        var outward = CoreVector3.UnitZ;
        var a = new CoreVector3(0f, 1f, 1f);
        var b = new CoreVector3(1f, 0f, 1f);
        var c = new CoreVector3(-1f, 0f, 1f);
        if (TriangleWinding.RequiresSwapToClockwise(a, b, c, outward))
            throw new InvalidOperationException("Godot clockwise outward triangle was marked for reversal.");

        if (!TriangleWinding.RequiresSwapToClockwise(a, c, b, outward))
            throw new InvalidOperationException("Counter-clockwise outward triangle was not marked for reversal.");

        Console.WriteLine("PLANET_MESH_WINDING PASS godot_front_face=clockwise outward=visible");
    }

    public static void RunSeedSweep(int seedCount)
    {
        if (seedCount < 1 || seedCount > 100)
            throw new ArgumentOutOfRangeException(nameof(seedCount), "Planet seed count must be 1..100.");

        var failures = new List<string>();
        var landRatioSum = 0.0;
        var landComponentsSum = 0L;
        var riversSum = 0L;
        var lakesSum = 0L;
        var largestShareSum = 0.0;

        for (var i = 0; i < seedCount; i++)
        {
            var seed = $"planet-quality-{i:00}";
            try
            {
                var world = PlanetWorldGenerator.Generate(new PlanetGenerationSettings(
                    seed,
                    PlanetGenerationScale.MediumFrequency,
                    WorldLandAmount.Normal,
                    WorldClimate.Temperate,
                    GeologicalActivity.Normal));
                ValidateGeneratedPlanet(world);

                var summary = world.Summary;
                if (summary.LandRatio is < 0.18f or > 0.72f)
                    throw new InvalidOperationException($"land ratio {summary.LandRatio:P1}");
                if (summary.RiverCells <= 0)
                    throw new InvalidOperationException("no rivers");

                var landComponents = LandComponentSizes(world.Topology, world.Cells);
                var landCells = Math.Max(1, (int)MathF.Round(summary.Cells * summary.LandRatio));
                var largestShare = landComponents.Count > 0 ? landComponents[0] / (double)landCells : 0;
                var secondShare = landComponents.Count > 1 ? landComponents[1] / (double)landCells : 0;
                var majorLimit = Math.Max(20, landCells / 100);
                var majorContinents = 0;
                var tinyIslands = 0;
                var tinyLimit = Math.Max(4, summary.Cells / 250);
                for (var component = 0; component < landComponents.Count; component++)
                {
                    if (landComponents[component] >= majorLimit)
                        majorContinents++;
                    if (landComponents[component] < tinyLimit)
                        tinyIslands++;
                }

                if (majorContinents is < 1 or > 3)
                    throw new InvalidOperationException($"major continents {majorContinents}");
                if (majorContinents > 1 && largestShare > 0.85)
                    throw new InvalidOperationException($"dominant continent {largestShare:P1}");
                if (majorContinents > 1 && secondShare < 0.04)
                    throw new InvalidOperationException($"second continent too small {secondShare:P1}");
                if (tinyIslands > Math.Max(16, summary.Cells / 900))
                    throw new InvalidOperationException($"tiny island fragmentation {tinyIslands}");
                if (summary.IslandCount is < 1 or > 12)
                    throw new InvalidOperationException($"archipelago islands {summary.IslandCount}");
                if (summary.LakeCells > landCells * 0.20)
                {
                    var lakeComponents = LakeComponentSizes(world.Topology, world.Cells);
                    throw new InvalidOperationException(
                        $"excessive lake coverage {summary.LakeCells}/{landCells}; " +
                        $"lake_components={string.Join(',', lakeComponents)}");
                }

                var inlandSeaCells = 0;
                var beachCells = 0;
                var lowCoastCells = 0;
                var shallowLakeCells = 0;
                var deepLakeCells = 0;
                for (var cellIndex = 0; cellIndex < world.Cells.Length; cellIndex++)
                {
                    var cell = world.Cells[cellIndex];
                    if (cell.IsLake && cell.ElevationMeters < 0f)
                        inlandSeaCells++;
                    if (!cell.IsLake && !cell.IsRiver && cell.CoastDistance <= 1 &&
                        cell.ElevationMeters is >= 0f and <= 2f)
                        beachCells++;
                    if (!cell.IsLake && !cell.IsRiver && cell.CoastDistance <= 4 &&
                        cell.ElevationMeters is >= 0f and <= 250f)
                        lowCoastCells++;
                    if (cell.IsLake && cell.WaterDepthMeters < 10f)
                        shallowLakeCells++;
                    if (cell.IsLake && cell.WaterDepthMeters >= 10f)
                        deepLakeCells++;
                }
                if (beachCells == 0)
                    throw new InvalidOperationException("no 0–2 m coastal beach cells");

                landRatioSum += summary.LandRatio;
                landComponentsSum += majorContinents;
                riversSum += summary.RiverCells;
                lakesSum += summary.LakeCells;
                largestShareSum += largestShare;

                Console.WriteLine(
                    $"PLANET_SEED seed={seed} cells={summary.Cells} land={summary.LandRatio:P1} " +
                    $"major_continents={majorContinents} largest={largestShare:P1} second={secondShare:P1} " +
                    $"tiny_islands={tinyIslands} rivers={summary.RiverCells} lakes={summary.LakeCells} " +
                    $"inland_sea_cells={inlandSeaCells} beach_0_2m={beachCells} low_coast_0_250m={lowCoastCells} " +
                    $"lake_depth_lt10m={shallowLakeCells} lake_depth_ge10m={deepLakeCells} " +
                    $"generation_ms={world.Metrics.TotalMs:0.###}");
            }
            catch (Exception ex)
            {
                failures.Add($"{seed}: {ex.Message}");
            }
        }

        var passed = seedCount - failures.Count;
        if (passed > 0)
        {
            Console.WriteLine(
                $"PLANET_SEED_SUMMARY passed={passed}/{seedCount} " +
                $"mean_land={landRatioSum / passed:P1} mean_components={landComponentsSum / (double)passed:0.00} " +
                $"mean_largest={largestShareSum / passed:P1} mean_rivers={riversSum / (double)passed:0.0} " +
                $"mean_lakes={lakesSum / (double)passed:0.0}");
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Planet generation failed for {failures.Count}/{seedCount} seeds: {string.Join(" | ", failures)}");

        Console.WriteLine($"PLANET SEEDS PASS count={seedCount}");
    }

    public static void RunBenchmark()
    {
        var cases = new[]
        {
            ("Small", PlanetGenerationScale.SmallFrequency),
            ("Medium", PlanetGenerationScale.MediumFrequency),
            ("Large", PlanetGenerationScale.LargeFrequency)
        };

        foreach (var (name, frequency) in cases)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBefore = GC.GetTotalMemory(true);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();

            var world = PlanetWorldGenerator.Generate(new PlanetGenerationSettings(
                $"planet-benchmark-{name.ToLowerInvariant()}",
                frequency,
                WorldLandAmount.Normal,
                WorldClimate.Temperate,
                GeologicalActivity.Normal));

            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var memoryDelta = Math.Max(0, GC.GetTotalMemory(false) - memoryBefore);
            ValidateGeneratedPlanet(world);

            var m = world.Metrics;
            var terrainTriangles = 6L * world.Cells.Length - 12L;
            var terrainVertices = terrainTriangles * 3L;
            Console.WriteLine(
                $"PLANET_BENCHMARK size={name} frequency={frequency} cells={world.Cells.Length} " +
                $"terrain_triangles={terrainTriangles} terrain_vertices={terrainVertices} " +
                $"topology_ms={m.TopologyMs:0.###} macro_ms={m.MacroElevationMs:0.###} " +
                $"coast_ms={m.CoastBathymetryMs:0.###} geology_ms={m.GeologyMs:0.###} " +
                $"hydrology_ms={m.HydrologyMs:0.###} climate_ms={m.ClimateMs:0.###} " +
                $"resources_ms={m.ResourcesMs:0.###} environment_ms={m.EnvironmentBuildMs:0.###} " +
                $"reported_total_ms={m.TotalMs:0.###} measured_total_ms={elapsed:0.###} " +
                $"min_elevation_m={world.Summary.MinElevationMeters:0} max_elevation_m={world.Summary.MaxElevationMeters:0} " +
                $"max_water_depth_m={world.Summary.MaxWaterDepthMeters:0} " +
                $"allocated_bytes={allocated} memory_delta={memoryDelta}");
        }

        Console.WriteLine("PLANET BENCHMARK PASS");
    }

    private static void AssertTopologyAtFrequency(int frequency, bool validatePolygons = true)
    {
        var (topology, geometry) = PlanetTopologyFactory.Build(frequency);
        var expected = PlanetGenerationScale.CellCountForFrequency(frequency);
        if (topology.Kind != WorldTopologyKind.Planet || topology.Count != expected || geometry.Count != expected)
            throw new InvalidOperationException($"Planet topology count/kind mismatch for frequency {frequency}.");

        var ids = new HashSet<CellId>();
        var pentagons = 0;
        var visited = new bool[topology.Count];
        var queue = new Queue<int>();

        for (var i = 0; i < topology.Count; i++)
        {
            var id = topology.GetCellId(i);
            if (!id.IsPlanet || id.PlanetIndex != i || !ids.Add(id))
                throw new InvalidOperationException($"Invalid or duplicate planet cell id at {i}.");

            if (!topology.TryGetSurfaceDirection(i, out var direction) ||
                !IsFinite(direction) ||
                Math.Abs(direction.Length - 1f) > 0.0005f)
                throw new InvalidOperationException($"Planet cell {i} has an invalid surface direction.");

            var neighbors = topology.GetNeighborIndices(i);
            if (neighbors.Length == 5)
                pentagons++;
            else if (neighbors.Length != 6)
                throw new InvalidOperationException($"Planet cell {i} has degree {neighbors.Length}.");

            var unique = new HashSet<int>();
            for (var n = 0; n < neighbors.Length; n++)
            {
                var neighbor = neighbors[n];
                if (neighbor == i || !unique.Add(neighbor))
                    throw new InvalidOperationException($"Planet cell {i} has duplicate/self neighbor.");

                var reverse = topology.GetNeighborIndices(neighbor);
                var found = false;
                for (var r = 0; r < reverse.Length; r++)
                {
                    if (reverse[r] == i)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new InvalidOperationException($"Planet adjacency {i}<->{neighbor} is not mutual.");
            }

            var polygon = geometry.GetPolygon(i);
            if (polygon.Length != neighbors.Length)
                throw new InvalidOperationException($"Planet cell {i} polygon/neighbor count mismatch.");

            var areaNormal = CoreVector3.Zero;
            for (var p = 0; p < polygon.Length; p++)
            {
                var corner = polygon[p];
                if (!IsFinite(corner) || Math.Abs(corner.Length - 1f) > 0.0005f)
                    throw new InvalidOperationException($"Planet cell {i} has an invalid polygon corner.");

                var nextCorner = polygon[(p + 1) % polygon.Length];
                var edgeLength = (corner - nextCorner).Length;
                if (!float.IsFinite(edgeLength) || edgeLength <= 0.000001f || edgeLength > 0.75f)
                    throw new InvalidOperationException(
                        $"Planet cell {i} has an implausible polygon edge {edgeLength:0.000000}.");

                areaNormal += CoreVector3.Cross(corner, nextCorner);
            }
            if (CoreVector3.Dot(areaNormal, direction) <= 0f)
                throw new InvalidOperationException($"Planet cell {i} polygon winding is inverted.");

            if (validatePolygons)
                AssertPolygonDoesNotSelfIntersect(i, direction, polygon);
        }

        if (pentagons != 12)
            throw new InvalidOperationException($"Planet topology has {pentagons} pentagons instead of 12.");

        visited[0] = true;
        queue.Enqueue(0);
        var connected = 0;
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            connected++;
            var neighbors = topology.GetNeighborIndices(at);
            for (var n = 0; n < neighbors.Length; n++)
            {
                var next = neighbors[n];
                if (visited[next])
                    continue;
                visited[next] = true;
                queue.Enqueue(next);
            }
        }

        if (connected != topology.Count)
            throw new InvalidOperationException($"Planet topology graph is disconnected: {connected}/{topology.Count}.");

        Console.WriteLine(
            $"PLANET_TOPOLOGY frequency={frequency} cells={topology.Count} pentagons={pentagons} connected={connected}");
    }

    private static void AssertTopologyDeterminism()
    {
        const int frequency = 10;
        var (a, ga) = PlanetTopologyFactory.Build(frequency);
        var (b, gb) = PlanetTopologyFactory.Build(frequency);

        if (a.Count != b.Count || ga.PolygonVertices.Length != gb.PolygonVertices.Length)
            throw new InvalidOperationException("Planet topology determinism count mismatch.");

        for (var i = 0; i < a.Count; i++)
        {
            if (a.GetCellId(i) != b.GetCellId(i) ||
                ga.Centers[i] != gb.Centers[i])
                throw new InvalidOperationException($"Planet topology ordering differs at cell {i}.");

            var an = a.GetNeighborIndices(i);
            var bn = b.GetNeighborIndices(i);
            if (an.Length != bn.Length)
                throw new InvalidOperationException($"Planet topology neighbor count differs at cell {i}.");
            for (var n = 0; n < an.Length; n++)
                if (an[n] != bn[n])
                    throw new InvalidOperationException($"Planet topology neighbor ordering differs at cell {i}.");
        }

        for (var i = 0; i < ga.PolygonVertices.Length; i++)
            if (ga.PolygonVertices[i] != gb.PolygonVertices[i])
                throw new InvalidOperationException($"Planet polygon ordering differs at vertex {i}.");
    }

    private static void AssertDeterministicGeneration()
    {
        var settings = new PlanetGenerationSettings(
            "planet-determinism",
            8,
            WorldLandAmount.Normal,
            WorldClimate.Temperate,
            GeologicalActivity.Normal);
        var a = PlanetWorldGenerator.Generate(settings);
        var b = PlanetWorldGenerator.Generate(settings);

        if (a.Cells.Length != b.Cells.Length)
            throw new InvalidOperationException("Same-seed planet cell counts differ.");

        for (var i = 0; i < a.Cells.Length; i++)
        {
            if (a.Cells[i] != b.Cells[i])
                throw new InvalidOperationException($"Same-seed planet generation differs at cell {i}.");
            if (a.Geometry.Centers[i] != b.Geometry.Centers[i])
                throw new InvalidOperationException($"Same-seed planet geometry differs at cell {i}.");
        }

        var different = PlanetWorldGenerator.Generate(settings with { Seed = "planet-determinism-other" });
        var changed = false;
        for (var i = 0; i < a.Cells.Length; i++)
        {
            if (Math.Abs(a.Cells[i].ElevationMeters - different.Cells[i].ElevationMeters) > 0.001f)
            {
                changed = true;
                break;
            }
        }
        if (!changed)
            throw new InvalidOperationException("Different planet seeds produced identical elevation.");
    }

    private static void AssertHydrologyAndPhysical()
    {
        var world = PlanetWorldGenerator.Generate(new PlanetGenerationSettings(
            "planet-hydrology",
            12,
            WorldLandAmount.Normal,
            WorldClimate.Temperate,
            GeologicalActivity.Normal));
        ValidateGeneratedPlanet(world);
    }

    private static void ValidateGeneratedPlanet(GeneratedPlanetWorld world)
    {
        if (world.Topology.Kind != WorldTopologyKind.Planet ||
            world.Topology.Count != world.Cells.Length ||
            world.Geometry.Count != world.Cells.Length)
            throw new InvalidOperationException("Generated planet topology/cell mismatch.");

        var state = new byte[world.Cells.Length];
        var stack = new int[world.Cells.Length];

        for (var i = 0; i < world.Cells.Length; i++)
        {
            var cell = world.Cells[i];
            if (!float.IsFinite(cell.ElevationMeters) ||
                !float.IsFinite(cell.WaterDepthMeters) || cell.WaterDepthMeters < 0f ||
                !float.IsFinite(cell.TemperatureCelsius) ||
                !float.IsFinite(cell.Humidity) || cell.Humidity is < 0f or > 1f ||
                !float.IsFinite(cell.PressureKPa) || cell.PressureKPa <= 0f ||
                !float.IsFinite(cell.FlowAccumulation) || cell.FlowAccumulation < 0f ||
                !float.IsFinite(cell.Slope) || cell.Slope < 0f ||
                !float.IsFinite(cell.LocalReliefMeters) || cell.LocalReliefMeters < 0f ||
                cell.GeologicalRegionId < 0 ||
                cell.MacroplateId < 0 ||
                !float.IsFinite(cell.PlateBoundaryStrength) ||
                cell.PlateBoundaryStrength is < 0f or > 1f ||
                cell.MineralPotential is < 0f or > 1f ||
                cell.NutrientPotential is < 0f or > 1f ||
                cell.GeothermalPotential is < 0f or > 1f)
                throw new InvalidOperationException($"Planet physical bounds failed at {cell.Id}.");

            var isOcean = cell.ElevationMeters < 0f && !cell.IsLake;
            if (cell.IsRiver && isOcean)
                throw new InvalidOperationException($"Planet river {cell.Id} is inside ocean terrain.");
            if (cell.IsRiver && cell.WaterDepthMeters <= 0f)
                throw new InvalidOperationException($"Planet river {cell.Id} is not a water-bearing habitat cell.");

            if (isOcean)
                continue;

            if (cell.DrainageTarget < 0 || cell.DrainageTarget >= world.Cells.Length)
                throw new InvalidOperationException($"Planet land cell {cell.Id} has no drainage target.");

            var neighbors = world.Topology.GetNeighborIndices(i);
            var direct = false;
            for (var n = 0; n < neighbors.Length; n++)
                if (neighbors[n] == cell.DrainageTarget)
                    direct = true;
            if (!direct)
                throw new InvalidOperationException($"Planet drainage from {cell.Id} skips a neighbor.");

            var degree = neighbors.Length;
            if (cell.RiverDirection < 0 || cell.RiverDirection >= degree)
                throw new InvalidOperationException($"Planet cell {cell.Id} has invalid drainage direction.");
            if (cell.StreamOrder < 0 || cell.UpstreamBranches < 0)
                throw new InvalidOperationException($"Planet cell {cell.Id} has invalid river metadata.");

            var target = world.Cells[cell.DrainageTarget];
            if (target.FlowAccumulation + 0.001f < cell.FlowAccumulation)
                throw new InvalidOperationException($"Planet flow accumulation decreases after {cell.Id}.");
            if (cell.IsRiver && target.IsRiver)
            {
                if (target.RiverWidth + 0.001f < cell.RiverWidth)
                    throw new InvalidOperationException($"Planet river narrows downstream after {cell.Id}.");
                if (target.StreamOrder < cell.StreamOrder)
                    throw new InvalidOperationException($"Planet stream order decreases downstream after {cell.Id}.");
            }

            var sourceSurface = cell.ElevationMeters + (cell.IsLake ? cell.WaterDepthMeters : 0f);
            var targetSurface = target.ElevationMeters + (target.IsLake ? target.WaterDepthMeters : 0f);
            if (targetSurface > sourceSurface + 2f)
                throw new InvalidOperationException($"Planet drainage flows uphill after {cell.Id}.");

            if (cell.IsRiver && cell.DrainageTarget < 0)
                throw new InvalidOperationException($"Planet river {cell.Id} has no downstream target.");
        }

        for (var start = 0; start < world.Cells.Length; start++)
        {
            var startIsOcean =
                world.Cells[start].ElevationMeters < 0f &&
                !world.Cells[start].IsLake;
            if (startIsOcean || state[start] != 0)
                continue;

            var depth = 0;
            var current = start;
            while (current >= 0 &&
                   !(world.Cells[current].ElevationMeters < 0f && !world.Cells[current].IsLake))
            {
                if (state[current] == 2)
                    break;
                if (state[current] == 1)
                    throw new InvalidOperationException($"Planet drainage cycle at {world.Cells[current].Id}.");

                if (depth >= world.Cells.Length)
                    throw new InvalidOperationException("Planet drainage traversal exceeded world size.");
                state[current] = 1;
                stack[depth++] = current;
                current = world.Cells[current].DrainageTarget;
            }

            while (depth > 0)
                state[stack[--depth]] = 2;
        }

        world.Environment.ValidatePhysicalBounds();
    }

    private static List<int> LandComponentSizes(
        WorldTopology topology,
        GeneratedWorldCell[] cells)
    {
        var visited = new bool[cells.Length];
        var queue = new Queue<int>();
        var result = new List<int>();

        for (var start = 0; start < cells.Length; start++)
        {
            if (visited[start] || cells[start].ElevationMeters < 0f)
                continue;

            visited[start] = true;
            queue.Enqueue(start);
            var size = 0;
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                size++;
                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var next = neighbors[n];
                    if (visited[next] || cells[next].ElevationMeters < 0f)
                        continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            result.Add(size);
        }

        result.Sort((a, b) => b.CompareTo(a));
        return result;
    }

    private static List<int> LakeComponentSizes(
        WorldTopology topology,
        GeneratedWorldCell[] cells)
    {
        var visited = new bool[cells.Length];
        var queue = new Queue<int>();
        var result = new List<int>();
        for (var start = 0; start < cells.Length; start++)
        {
            if (visited[start] || !cells[start].IsLake)
                continue;
            visited[start] = true;
            queue.Enqueue(start);
            var size = 0;
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                size++;
                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var next = neighbors[n];
                    if (visited[next] || !cells[next].IsLake)
                        continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
            result.Add(size);
        }
        result.Sort((a, b) => b.CompareTo(a));
        return result;
    }

    private static void AssertSnapshotRoundTrip()
    {
        var generated = PlanetWorldGenerator.Generate(new PlanetGenerationSettings("planet-snapshot", 6));
        var simulation = new CoreSimulation(
            generated.Topology,
            generated.Environment,
            SeedMixer.FromString("planet-snapshot"),
            SimulationMode.Live);
        simulation.Step(20);

        var json = CoreSnapshotSerializer.Serialize(simulation.CaptureSnapshot());
        var snapshot = CoreSnapshotSerializer.Deserialize(json);
        var restored = CoreSimulation.Restore(snapshot);

        if (restored.Topology.Kind != WorldTopologyKind.Planet ||
            restored.Topology.Count != generated.Topology.Count ||
            restored.Topology.SurfaceDirections.Length != generated.Topology.Count)
            throw new InvalidOperationException("Planet topology did not survive Core snapshot round-trip.");

        var first = restored.Topology.GetCellId(0);
        if (!first.IsPlanet || !restored.Topology.TryGetSurfaceDirection(0, out var direction) ||
            Math.Abs(direction.Length - 1f) > 0.0005f)
            throw new InvalidOperationException("Planet snapshot lost stable cell geometry.");

        simulation.Step(240);
        restored.Step(240);
        var expected = CoreSnapshotSerializer.Serialize(simulation.CaptureSnapshot());
        var actual = CoreSnapshotSerializer.Serialize(restored.CaptureSnapshot());
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException("Planet save/restore determinism failed after continued simulation.");
    }

    private static void AssertPolygonDoesNotSelfIntersect(
        int cellIndex,
        CoreVector3 normal,
        ReadOnlySpan<CoreVector3> polygon)
    {
        var reference = Math.Abs(normal.Y) < 0.9f ? CoreVector3.UnitY : CoreVector3.UnitX;
        var axisX = CoreVector3.Cross(reference, normal).Normalized();
        var axisY = CoreVector3.Cross(normal, axisX).Normalized();
        var points = new (float X, float Y)[polygon.Length];
        for (var i = 0; i < polygon.Length; i++)
            points[i] = (CoreVector3.Dot(polygon[i], axisX), CoreVector3.Dot(polygon[i], axisY));

        for (var a = 0; a < points.Length; a++)
        {
            var aNext = (a + 1) % points.Length;
            for (var b = a + 1; b < points.Length; b++)
            {
                var bNext = (b + 1) % points.Length;
                if (a == b || aNext == b || bNext == a)
                    continue;
                if (a == 0 && bNext == 0)
                    continue;

                if (SegmentsProperlyIntersect(points[a], points[aNext], points[b], points[bNext]))
                    throw new InvalidOperationException($"Planet cell {cellIndex} polygon self-intersects.");
            }
        }
    }

    private static bool SegmentsProperlyIntersect(
        (float X, float Y) a,
        (float X, float Y) b,
        (float X, float Y) c,
        (float X, float Y) d)
    {
        static float Cross(
            (float X, float Y) p,
            (float X, float Y) q,
            (float X, float Y) r) =>
            (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);

        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        const float epsilon = 0.0000001f;

        return abC * abD < -epsilon && cdA * cdB < -epsilon;
    }

    private static bool IsFinite(CoreVector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
