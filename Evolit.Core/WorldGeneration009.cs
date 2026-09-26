using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Evolit.Core;

internal static class WorldGeneration009Pipeline
{
    private const float PriorityFloodEpsilon = 0.025f;

    private static readonly (int Q, int R)[] Directions =
    [
        (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)
    ];

    private readonly record struct Plate(
        float X,
        float Y,
        float MotionX,
        float MotionY,
        bool Continental,
        float Crust,
        float Volcanism);

    private sealed class HydrologyResult
    {
        public required int[] Drainage { get; init; }
        public required float[] Accumulation { get; init; }
        public required float[] Slope { get; init; }
        public required bool[] Rivers { get; init; }
        public required bool[] Lakes { get; init; }
        public required int[] BasinId { get; init; }
        public required int[] RiverLength { get; init; }
        public required float[] RiverWidth { get; init; }
        public required float[] FilledElevation { get; init; }
    }

    public static GeneratedWorld Generate(WorldGenerationSettings settings, ulong attemptSeed)
    {
        var totalStart = Stopwatch.GetTimestamp();

        var topologyStart = Stopwatch.GetTimestamp();
        var ids = BuildCells(settings.Radius);
        var topology = BuildTopology(ids);
        var topologyMs = Stopwatch.GetElapsedTime(topologyStart).TotalMilliseconds;
        var count = ids.Length;

        var elevation = new float[count];
        var water = new float[count];
        var temperature = new float[count];
        var humidity = new float[count];
        var minerals = new float[count];
        var nutrients = new float[count];
        var geothermal = new float[count];
        var substrate = new SubstrateKind[count];

        var province = new int[count];
        var continentalness = new float[count];
        var tectonicUplift = new float[count];
        var convergence = new float[count];
        var divergence = new float[count];
        var coastDistance = new int[count];

        var macroSeed = SeedMixer.Combine(attemptSeed, 0x1001UL);
        var geologySeed = SeedMixer.Combine(attemptSeed, 0x2001UL);
        var islandSeed = SeedMixer.Combine(attemptSeed, 0x3001UL);
        var erosionSeed = SeedMixer.Combine(attemptSeed, 0x4001UL);
        var climateSeed = SeedMixer.Combine(attemptSeed, 0x5001UL);

        var macroStart = Stopwatch.GetTimestamp();
        var plates = BuildPlates(macroSeed);
        GenerateMacroGeography(
            settings,
            ids,
            plates,
            macroSeed,
            elevation,
            province,
            continentalness,
            convergence,
            divergence,
            tectonicUplift);
        var macroMs = Stopwatch.GetElapsedTime(macroStart).TotalMilliseconds;

        var coastStart = Stopwatch.GetTimestamp();
        var seaLevel = ChooseSeaLevel(settings, elevation);
        for (var i = 0; i < count; i++)
            elevation[i] -= seaLevel;

        AddContinentalShelfIslands(
            settings,
            ids,
            topology,
            plates,
            islandSeed,
            elevation,
            province);
        CleanupLandMask(topology, elevation);
        ApplyTectonicRelief(
            settings,
            ids,
            topology,
            plates,
            geologySeed,
            elevation,
            province,
            convergence,
            divergence,
            tectonicUplift,
            geothermal);
        AddIslandArcs(
            settings,
            ids,
            topology,
            plates,
            islandSeed,
            elevation,
            province,
            convergence,
            tectonicUplift,
            geothermal);
        RelaxExtremeSlopes(topology, elevation, 2);

        // Re-clean only tiny artifacts after geological uplift; meaningful island arcs survive.
        CleanupLandMask(topology, elevation, preserveSmallIslands: true);
        BuildCoastDistance(topology, elevation, coastDistance);
        ApplyOceanBathymetry(ids, settings, climateSeed, elevation, water, coastDistance);
        var coastMs = Stopwatch.GetElapsedTime(coastStart).TotalMilliseconds;

        var geologyStart = Stopwatch.GetTimestamp();
        GenerateGeologyAndSubstrate(
            settings,
            ids,
            plates,
            geologySeed,
            elevation,
            water,
            province,
            tectonicUplift,
            geothermal,
            coastDistance,
            minerals,
            substrate);
        var geologyMs = Stopwatch.GetElapsedTime(geologyStart).TotalMilliseconds;

        var hydrologyStart = Stopwatch.GetTimestamp();
        var hydro = BuildHydrology(topology, elevation);
        CarveMainRiverValleys(topology, elevation, hydro, erosionSeed);
        // A second pass makes drainage match the final carved relief.
        hydro = BuildHydrology(topology, elevation);
        ApplyLakeWaterDepth(elevation, water, hydro);
        ApplyRiverWaterDepth(water, hydro);
        FinalizeHydrologicSubstrate(water, hydro, substrate);
        var hydrologyMs = Stopwatch.GetElapsedTime(hydrologyStart).TotalMilliseconds;

        var climateStart = Stopwatch.GetTimestamp();
        GenerateInitialClimate(
            settings,
            topology,
            ids,
            climateSeed,
            elevation,
            water,
            hydro,
            tectonicUplift,
            temperature,
            humidity);
        var climateMs = Stopwatch.GetElapsedTime(climateStart).TotalMilliseconds;

        var resourcesStart = Stopwatch.GetTimestamp();
        GenerateResources(
            humidity,
            hydro,
            minerals,
            substrate,
            nutrients);
        var resourcesMs = Stopwatch.GetElapsedTime(resourcesStart).TotalMilliseconds;

        var environmentStart = Stopwatch.GetTimestamp();
        var environment = new EnvironmentStore(topology);
        for (var i = 0; i < count; i++)
        {
            var pressure = Math.Clamp(
                101.325f * MathF.Exp(-Math.Max(-500f, elevation[i]) / 8434f),
                20f,
                115f);

            environment.SetInitial(ids[i], new EnvironmentCellState(
                ElevationMeters: elevation[i],
                WaterDepthMeters: water[i],
                TemperatureCelsius: temperature[i],
                Humidity: humidity[i],
                PressureKPa: pressure,
                LightAvailability: water[i] > 120f ? 0.55f : 1f,
                MineralPotential: minerals[i],
                NutrientPotential: nutrients[i],
                OrganicMatter: 0f,
                SubstrateDevelopment: 0f,
                GeothermalPotential: geothermal[i],
                Substrate: substrate[i]));
        }
        var environmentMs = Stopwatch.GetElapsedTime(environmentStart).TotalMilliseconds;

        var cells = new GeneratedWorldCell[count];
        for (var i = 0; i < count; i++)
        {
            var pressure = Math.Clamp(
                101.325f * MathF.Exp(-Math.Max(-500f, elevation[i]) / 8434f),
                20f,
                115f);

            cells[i] = new GeneratedWorldCell(
                ids[i],
                elevation[i],
                water[i],
                temperature[i],
                humidity[i],
                pressure,
                minerals[i],
                nutrients[i],
                geothermal[i],
                substrate[i],
                hydro.Drainage[i],
                hydro.Accumulation[i],
                hydro.Slope[i],
                hydro.Rivers[i],
                hydro.Lakes[i],
                province[i],
                continentalness[i],
                tectonicUplift[i],
                coastDistance[i],
                hydro.BasinId[i],
                hydro.RiverLength[i],
                hydro.RiverWidth[i]);
        }

        var summary = Summarize(topology, cells);
        var quality = AnalyzeQuality(topology, cells, hydro);

        return new GeneratedWorld
        {
            Settings = settings,
            Topology = topology,
            Cells = cells,
            Environment = environment,
            Summary = summary,
            Quality = quality,
            Metrics = new WorldGenerationMetrics(
                topologyMs,
                macroMs,
                coastMs,
                geologyMs,
                hydrologyMs,
                climateMs,
                resourcesMs,
                environmentMs,
                Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds)
        };
    }

    private static Plate[] BuildPlates(ulong seed)
    {
        var count = 9 + (int)(SeedMixer.Combine(seed, 17) % 5UL);
        var continentalCount = 3 + (int)(SeedMixer.Combine(seed, 23) % 3UL);
        var continentalScores = new (float Score, int Index)[count];

        for (var i = 0; i < count; i++)
            continentalScores[i] = (Hash01(i + 11, 71, seed), i);

        Array.Sort(continentalScores, static (a, b) => b.Score.CompareTo(a.Score));
        var continental = new bool[count];
        for (var i = 0; i < continentalCount; i++)
            continental[continentalScores[i].Index] = true;

        var plates = new Plate[count];
        const float goldenAngle = 2.39996323f;
        var rotation = Hash01(31, 47, seed) * MathF.PI * 2f;

        for (var i = 0; i < count; i++)
        {
            var radial01 = (i + 0.55f) / count;
            var radius = MathF.Sqrt(radial01) * 0.79f;
            var angle = rotation + i * goldenAngle + SignedHash(seed, 100 + i) * 0.16f;
            var x = MathF.Cos(angle) * radius + SignedHash(seed, 200 + i) * 0.055f;
            var y = MathF.Sin(angle) * radius + SignedHash(seed, 300 + i) * 0.055f;

            var motionAngle = Hash01(i + 101, 131, SeedMixer.Combine(seed, 41)) * MathF.PI * 2f;
            var speed = 0.35f + Hash01(i + 151, 181, SeedMixer.Combine(seed, 43)) * 0.65f;
            var crust = continental[i]
                ? 0.72f + Hash01(i + 211, 227, seed) * 0.24f
                : -0.72f - Hash01(i + 229, 241, seed) * 0.18f;
            var volcanism = Hash01(i + 251, 269, SeedMixer.Combine(seed, 47));

            plates[i] = new Plate(
                x,
                y,
                MathF.Cos(motionAngle) * speed,
                MathF.Sin(motionAngle) * speed,
                continental[i],
                crust,
                volcanism);
        }

        return plates;
    }

    private static void GenerateMacroGeography(
        WorldGenerationSettings settings,
        CellId[] ids,
        Plate[] plates,
        ulong macroSeed,
        float[] elevation,
        int[] province,
        float[] continentalness,
        float[] convergence,
        float[] divergence,
        float[] tectonicUplift)
    {
        var landBias = settings.LandAmount switch
        {
            WorldLandAmount.Low => -0.09f,
            WorldLandAmount.High => 0.09f,
            _ => 0f
        };

        for (var i = 0; i < ids.Length; i++)
        {
            var (x0, y0) = NormalizedWorldPosition(ids[i], settings.Radius);

            // Domain warp affects plate borders and coast shape, but not the underlying
            // RNG streams for geology/hydrology.
            var warpX = (Fbm(x0 * 1.7f + 3.1f, y0 * 1.7f - 5.4f, SeedMixer.Combine(macroSeed, 101), 3) - 0.5f) * 0.24f;
            var warpY = (Fbm(x0 * 1.7f - 4.7f, y0 * 1.7f + 2.8f, SeedMixer.Combine(macroSeed, 103), 3) - 0.5f) * 0.24f;
            var x = x0 + warpX;
            var y = y0 + warpY;

            FindNearestPlates(plates, x, y, out var first, out var second, out var d1, out var d2);
            province[i] = first;

            var p = plates[first];
            var s = plates[second];
            var gap = Math.Max(0f, MathF.Sqrt(d2) - MathF.Sqrt(d1));
            var boundaryStrength = MathF.Exp(-gap * 17f);

            var nx = s.X - p.X;
            var ny = s.Y - p.Y;
            var nLength = MathF.Sqrt(nx * nx + ny * ny);
            if (nLength > 0.0001f)
            {
                nx /= nLength;
                ny /= nLength;
            }

            var relative = (p.MotionX - s.MotionX) * nx + (p.MotionY - s.MotionY) * ny;
            convergence[i] = Math.Clamp(relative, 0f, 1.5f) * boundaryStrength;
            divergence[i] = Math.Clamp(-relative, 0f, 1.5f) * boundaryStrength;

            var continentalBlend = p.Crust;
            if (boundaryStrength > 0.25f)
                continentalBlend = Lerp(continentalBlend, s.Crust, boundaryStrength * 0.32f);

            var broad = Fbm(x0 * 1.25f + 7f, y0 * 1.25f - 11f, SeedMixer.Combine(macroSeed, 107), 4) - 0.5f;
            var regional = Fbm(x0 * 3.3f - 13f, y0 * 3.3f + 17f, SeedMixer.Combine(macroSeed, 109), 3) - 0.5f;
            var local = Fbm(x0 * 8.5f + 19f, y0 * 8.5f - 23f, SeedMixer.Combine(macroSeed, 113), 2) - 0.5f;
            var coastStyle = 0.58f + Hash01(first + 307, 313, SeedMixer.Combine(macroSeed, 127)) * 0.78f;

            // Divergent continental boundaries can open large rift-like lowlands.
            var rift = p.Continental && s.Continental ? divergence[i] * 0.24f : 0f;

            // Oceanic convergent borders receive a small arc potential. Whether they
            // become islands is decided later, after the global land mask.
            var arc = !p.Continental && !s.Continental
                ? convergence[i] * (0.10f + (p.Volcanism + s.Volcanism) * 0.08f)
                : 0f;

            var radial = MathF.Sqrt(x0 * x0 + y0 * y0);
            var edgeOcean = MathF.Pow(Math.Clamp((radial - 0.73f) / 0.27f, 0f, 1f), 2.2f) * 1.65f;

            var value =
                continentalBlend * 0.90f +
                broad * 0.54f +
                regional * (0.22f + coastStyle * 0.055f) +
                local * (0.045f + coastStyle * 0.070f) +
                arc -
                rift -
                edgeOcean +
                landBias;

            continentalness[i] = Math.Clamp(value, -1.5f, 1.5f);
            elevation[i] = value * 2600f;
            tectonicUplift[i] = convergence[i] * 0.25f;
        }
    }

    private static void AddContinentalShelfIslands(
        WorldGenerationSettings settings,
        CellId[] ids,
        WorldTopology topology,
        Plate[] plates,
        ulong seed,
        float[] elevation,
        int[] province)
    {
        var distanceToLand = DistanceToLand(topology, elevation);

        for (var i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] >= 0f)
                continue;

            var distance = distanceToLand[i];
            if (distance is < 2 or > 5)
                continue;

            var plate = plates[province[i]];
            if (!plate.Continental)
                continue;

            var (x, y) = NormalizedWorldPosition(ids[i], settings.Radius);
            var regional = Fbm(
                x * 3.4f + 101f,
                y * 3.4f - 103f,
                SeedMixer.Combine(seed, 151),
                3);
            var fragment = Fbm(
                x * 10.5f - 107f,
                y * 10.5f + 109f,
                SeedMixer.Combine(seed, 157),
                2);

            // Continental fragments are deliberately sparse and constrained to
            // the shelf. Connected-component cleanup removes single-cell noise,
            // leaving larger coastal islands and small archipelagos.
            if (regional < 0.67f || fragment < 0.60f)
                continue;

            var strength =
                (regional - 0.67f) * 950f +
                (fragment - 0.60f) * 620f;
            elevation[i] = Math.Clamp(18f + strength, 18f, 620f);
        }
    }

    private static int[] DistanceToLand(WorldTopology topology, float[] elevation)
    {
        var distance = new int[elevation.Length];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();

        for (var i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] < 0f)
                continue;
            distance[i] = 0;
            queue.Enqueue(i);
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            var next = distance[at] + 1;
            var neighbors = topology.GetNeighborIndices(at);

            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (distance[ni] <= next)
                    continue;
                distance[ni] = next;
                queue.Enqueue(ni);
            }
        }

        return distance;
    }

    private static void ApplyTectonicRelief(
        WorldGenerationSettings settings,
        CellId[] ids,
        WorldTopology topology,
        Plate[] plates,
        ulong seed,
        float[] elevation,
        int[] province,
        float[] convergence,
        float[] divergence,
        float[] uplift,
        float[] geothermal)
    {
        var activity = settings.Geology switch
        {
            GeologicalActivity.Calm => 0.65f,
            GeologicalActivity.Active => 1.35f,
            _ => 1f
        };

        var reliefScratch = new float[elevation.Length];

        for (var i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] < 0f)
            {
                reliefScratch[i] = elevation[i];
                continue;
            }

            var plate = plates[province[i]];
            var (x, y) = NormalizedWorldPosition(ids[i], settings.Radius);
            var ridgeNoise = 0.72f + Fbm(x * 7.5f, y * 7.5f, SeedMixer.Combine(seed, 211), 3) * 0.56f;

            var convergentUplift = MathF.Pow(Math.Clamp(convergence[i], 0f, 1f), 0.72f) * 2600f * activity * ridgeNoise;
            var volcanicUplift = convergence[i] * plate.Volcanism * 650f * activity;
            var riftValley = divergence[i] * 520f * activity;

            // Stable continental interiors receive broad plateaus/plains rather than
            // uniform high-frequency relief.
            var interior = 1f - Math.Clamp(convergence[i] + divergence[i], 0f, 1f);
            var plateauField = Fbm(x * 1.8f + 31f, y * 1.8f - 37f, SeedMixer.Combine(seed, 223), 3);
            var plateau = interior * Math.Max(0f, plateauField - 0.62f) * 900f;
            var plainRelaxation = interior * Math.Max(0f, 0.52f - plateauField) * 260f;

            reliefScratch[i] = Math.Clamp(
                elevation[i] +
                convergentUplift +
                volcanicUplift +
                plateau -
                riftValley -
                plainRelaxation,
                8f,
                6500f);

            uplift[i] = Math.Clamp((convergentUplift + volcanicUplift) / 3200f, 0f, 1f);
            geothermal[i] = Math.Clamp(
                plates[province[i]].Volcanism * 0.20f +
                convergence[i] * 0.45f +
                divergence[i] * 0.18f,
                0f,
                1f);
        }

        Array.Copy(reliefScratch, elevation, elevation.Length);

        // One low-cost thermal-style relaxation pass turns isolated spikes into
        // peaks + foothills without erasing mountain chains.
        var smoothed = new float[elevation.Length];
        for (var i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] < 0f)
            {
                smoothed[i] = elevation[i];
                continue;
            }

            var neighbors = topology.GetNeighborIndices(i);
            var neighborAverage = 0f;
            for (var n = 0; n < neighbors.Length; n++)
                neighborAverage += elevation[neighbors[n]];
            neighborAverage = neighbors.Length == 0 ? elevation[i] : neighborAverage / neighbors.Length;

            var delta = elevation[i] - neighborAverage;
            smoothed[i] = delta > 850f
                ? elevation[i] - Math.Min(260f, (delta - 850f) * 0.26f)
                : elevation[i];
        }
        Array.Copy(smoothed, elevation, elevation.Length);
    }

    private static void AddIslandArcs(
        WorldGenerationSettings settings,
        CellId[] ids,
        WorldTopology topology,
        Plate[] plates,
        ulong seed,
        float[] elevation,
        int[] province,
        float[] convergence,
        float[] uplift,
        float[] geothermal)
    {
        var activity = settings.Geology switch
        {
            GeologicalActivity.Calm => 0.7f,
            GeologicalActivity.Active => 1.25f,
            _ => 1f
        };

        var candidate = new bool[elevation.Length];
        for (var i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] >= 0f)
                continue;

            var p = plates[province[i]];
            if (p.Continental || convergence[i] < 0.18f)
                continue;

            var (x, y) = NormalizedWorldPosition(ids[i], settings.Radius);
            var chain = Fbm(x * 12f + 43f, y * 12f - 41f, SeedMixer.Combine(seed, 301), 2);
            if (chain < 0.62f)
                continue;

            var strength = convergence[i] * (0.55f + p.Volcanism * 0.55f) * activity;
            if (strength < 0.22f)
                continue;

            candidate[i] = true;
        }

        // Grow only short deterministic chains, never a second pseudo-continent.
        var grown = (bool[])candidate.Clone();
        for (var i = 0; i < candidate.Length; i++)
        {
            if (!candidate[i])
                continue;

            var neighbors = topology.GetNeighborIndices(i);
            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (elevation[ni] >= 0f || Hash01(i + n, ni + 401, seed) < 0.46f)
                    continue;
                grown[ni] = true;
            }
        }

        for (var i = 0; i < grown.Length; i++)
        {
            if (!grown[i])
                continue;

            var local = Hash01(i + 503, province[i] + 509, seed);
            elevation[i] = 25f + local * 780f;
            uplift[i] = Math.Max(uplift[i], 0.45f + local * 0.45f);
            geothermal[i] = Math.Max(geothermal[i], 0.55f + local * 0.35f);
        }
    }

    private static void RelaxExtremeSlopes(WorldTopology topology, float[] elevation, int passes)
    {
        var scratch = new float[elevation.Length];

        for (var pass = 0; pass < passes; pass++)
        {
            for (var i = 0; i < elevation.Length; i++)
            {
                if (elevation[i] < 0f)
                {
                    scratch[i] = elevation[i];
                    continue;
                }

                var neighbors = topology.GetNeighborIndices(i);
                var minNeighbor = float.MaxValue;
                var maxNeighbor = float.MinValue;
                for (var n = 0; n < neighbors.Length; n++)
                {
                    minNeighbor = Math.Min(minNeighbor, elevation[neighbors[n]]);
                    maxNeighbor = Math.Max(maxNeighbor, elevation[neighbors[n]]);
                }

                if (neighbors.Length == 0)
                {
                    scratch[i] = elevation[i];
                    continue;
                }

                var aboveLowest = elevation[i] - minNeighbor;
                scratch[i] = aboveLowest > 1650f
                    ? elevation[i] - Math.Min(220f, (aboveLowest - 1650f) * 0.18f)
                    : elevation[i];

                // Preserve broad plains: low-relief cells should not accumulate noise.
                if (maxNeighbor - minNeighbor < 180f)
                {
                    var sum = elevation[i];
                    for (var n = 0; n < neighbors.Length; n++)
                        sum += elevation[neighbors[n]];
                    scratch[i] = Lerp(scratch[i], sum / (neighbors.Length + 1), 0.28f);
                }
            }

            Array.Copy(scratch, elevation, elevation.Length);
        }
    }

    private static void CleanupLandMask(
        WorldTopology topology,
        float[] elevation,
        bool preserveSmallIslands = false)
    {
        var land = new bool[elevation.Length];
        for (var i = 0; i < land.Length; i++)
            land[i] = elevation[i] >= 0f;

        var visited = new bool[land.Length];
        var queue = new Queue<int>();
        var component = new List<int>();

        var minimumIsland = preserveSmallIslands
            ? Math.Max(2, land.Length / 8000)
            : Math.Max(4, land.Length / 3000);

        for (var i = 0; i < land.Length; i++)
        {
            if (!land[i] || visited[i])
                continue;

            component.Clear();
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                component.Add(at);
                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var ni = neighbors[n];
                    if (visited[ni] || !land[ni])
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            if (component.Count >= minimumIsland)
                continue;

            foreach (var index in component)
            {
                land[index] = false;
                elevation[index] = -Math.Max(45f, Math.Abs(elevation[index]));
            }
        }

        Array.Fill(visited, false);
        var maximumNoiseHole = Math.Max(8, elevation.Length / 900);

        for (var i = 0; i < land.Length; i++)
        {
            if (land[i] || visited[i])
                continue;

            component.Clear();
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;
            var touchesBoundary = false;

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                component.Add(at);
                if (topology.GetNeighborIndices(at).Length < 6)
                    touchesBoundary = true;

                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var ni = neighbors[n];
                    if (visited[ni] || land[ni])
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            if (touchesBoundary || component.Count > maximumNoiseHole)
                continue;

            foreach (var index in component)
            {
                land[index] = true;
                elevation[index] = Math.Max(18f, Math.Abs(elevation[index]) * 0.22f);
            }
        }
    }

    private static void BuildCoastDistance(
        WorldTopology topology,
        float[] elevation,
        int[] coastDistance)
    {
        Array.Fill(coastDistance, int.MaxValue);
        var queue = new Queue<int>();

        for (var i = 0; i < elevation.Length; i++)
        {
            var land = elevation[i] >= 0f;
            var neighbors = topology.GetNeighborIndices(i);
            var isCoast = false;

            for (var n = 0; n < neighbors.Length; n++)
            {
                if ((elevation[neighbors[n]] >= 0f) != land)
                {
                    isCoast = true;
                    break;
                }
            }

            if (!isCoast)
                continue;

            coastDistance[i] = 0;
            queue.Enqueue(i);
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            var next = coastDistance[at] + 1;
            var neighbors = topology.GetNeighborIndices(at);

            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (coastDistance[ni] <= next)
                    continue;
                coastDistance[ni] = next;
                queue.Enqueue(ni);
            }
        }
    }

    private static void ApplyOceanBathymetry(
        CellId[] ids,
        WorldGenerationSettings settings,
        ulong seed,
        float[] elevation,
        float[] water,
        int[] coastDistance)
    {
        for (var i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] >= 0f)
            {
                water[i] = 0f;
                var inland = coastDistance[i] == int.MaxValue ? settings.Radius : coastDistance[i];
                elevation[i] = Math.Clamp(
                    elevation[i] + Math.Min(420f, inland * 22f),
                    6f,
                    6500f);
                continue;
            }

            var d = coastDistance[i] == int.MaxValue ? settings.Radius : coastDistance[i];
            var (x, y) = NormalizedWorldPosition(ids[i], settings.Radius);
            var basin = Fbm(x * 1.9f + 61f, y * 1.9f - 67f, SeedMixer.Combine(seed, 401), 3);
            var shelf = d <= 2
                ? 22f + d * 55f
                : 130f + MathF.Pow(d - 1, 1.30f) * 38f;
            var depth = Math.Clamp(shelf + basin * Math.Min(1400f, d * 62f), 18f, 6000f);
            elevation[i] = -depth;
            water[i] = depth;
        }
    }

    private static void GenerateGeologyAndSubstrate(
        WorldGenerationSettings settings,
        CellId[] ids,
        Plate[] plates,
        ulong seed,
        float[] elevation,
        float[] water,
        int[] province,
        float[] uplift,
        float[] geothermal,
        int[] coastDistance,
        float[] minerals,
        SubstrateKind[] substrate)
    {
        for (var i = 0; i < elevation.Length; i++)
        {
            var (x, y) = NormalizedWorldPosition(ids[i], settings.Radius);
            var provinceNoise = Fbm(x * 2.6f + 71f, y * 2.6f - 73f, SeedMixer.Combine(seed, 501), 3);
            var plate = plates[province[i]];

            minerals[i] = Math.Clamp(
                0.18f +
                provinceNoise * 0.42f +
                uplift[i] * 0.26f +
                geothermal[i] * 0.18f,
                0f,
                1f);

            if (water[i] > 0.1f)
            {
                substrate[i] = SubstrateKind.Sediment;
                continue;
            }

            if (geothermal[i] > 0.72f && plate.Volcanism > 0.62f)
                substrate[i] = SubstrateKind.Basalt;
            else if (elevation[i] > 1900f || uplift[i] > 0.72f)
                substrate[i] = SubstrateKind.BareRock;
            else if (coastDistance[i] <= 1 && elevation[i] < 180f)
                substrate[i] = SubstrateKind.Sand;
            else
                substrate[i] = SubstrateKind.MineralRegolith;
        }
    }

    private static HydrologyResult BuildHydrology(
        WorldTopology topology,
        float[] elevation)
    {
        var count = elevation.Length;
        var filled = (float[])elevation.Clone();
        var drainage = new int[count];
        var accumulation = new float[count];
        var slope = new float[count];
        var lakes = new bool[count];
        var rivers = new bool[count];
        var basin = new int[count];
        var riverLength = new int[count];
        var riverWidth = new float[count];
        var visited = new bool[count];
        Array.Fill(drainage, -1);
        Array.Fill(basin, -1);

        var queue = new PriorityQueue<int, float>();

        // All ocean cells are valid outlets. Priority flood then resolves every
        // inland depression deterministically on the hex topology.
        for (var i = 0; i < count; i++)
        {
            if (elevation[i] >= 0f)
                continue;
            visited[i] = true;
            queue.Enqueue(i, filled[i]);
        }

        if (queue.Count == 0)
            throw new InvalidOperationException("Priority flood requires at least one ocean outlet.");

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            var neighbors = topology.GetNeighborIndices(at);

            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (visited[ni])
                    continue;

                visited[ni] = true;
                var candidate = Math.Max(elevation[ni], filled[at] + PriorityFloodEpsilon);
                filled[ni] = candidate;
                drainage[ni] = at;
                queue.Enqueue(ni, candidate);
            }
        }

        var depression = new float[count];
        for (var i = 0; i < count; i++)
            if (elevation[i] >= 0f)
                depression[i] = Math.Max(0f, filled[i] - elevation[i]);

        MarkLakeBasins(topology, elevation, filled, depression, lakes);

        // Tiny depressions are filled into the terrain itself; significant basins
        // remain as lake floors and drain through their priority-flood spill path.
        for (var i = 0; i < count; i++)
        {
            if (elevation[i] < 0f || lakes[i])
                continue;
            if (depression[i] > 0f)
                elevation[i] = filled[i];
        }

        var order = Enumerable.Range(0, count).ToArray();
        Array.Sort(order, (a, b) => filled[b].CompareTo(filled[a]));

        for (var i = 0; i < count; i++)
            accumulation[i] = elevation[i] >= 0f ? 1f : 0f;

        var upstreamLength = new int[count];
        Array.Fill(upstreamLength, 1);

        foreach (var i in order)
        {
            var parent = drainage[i];
            if (parent < 0)
                continue;

            accumulation[parent] += accumulation[i];
            if (elevation[i] >= 0f)
                upstreamLength[parent] = Math.Max(upstreamLength[parent], upstreamLength[i] + 1);
        }

        // Basin ids are assigned from mouths toward upstream cells.
        var ascending = (int[])order.Clone();
        Array.Reverse(ascending);
        var nextBasin = 0;

        foreach (var i in ascending)
        {
            if (elevation[i] < 0f)
                continue;

            var parent = drainage[i];
            if (parent < 0 || elevation[parent] < 0f)
            {
                basin[i] = nextBasin++;
            }
            else
            {
                basin[i] = basin[parent];
                if (basin[i] < 0)
                    basin[i] = nextBasin++;
            }
        }

        var riverThreshold = Math.Max(28f, count * 0.00115f);
        for (var i = 0; i < count; i++)
        {
            if (elevation[i] < 0f || lakes[i] || drainage[i] < 0)
                continue;

            if (accumulation[i] >= riverThreshold && upstreamLength[i] >= 5)
            {
                rivers[i] = true;
                riverLength[i] = upstreamLength[i];
                riverWidth[i] = Math.Clamp(
                    0.65f + MathF.Sqrt(accumulation[i] / riverThreshold) * 0.72f,
                    0.65f,
                    4.5f);
            }
        }

        for (var i = 0; i < count; i++)
        {
            var parent = drainage[i];
            if (parent < 0)
            {
                slope[i] = 0f;
                continue;
            }

            var sourceSurface = lakes[i] ? filled[i] : elevation[i];
            var targetSurface = lakes[parent] ? filled[parent] : elevation[parent];
            slope[i] = Math.Max(0f, sourceSurface - targetSurface);
        }

        return new HydrologyResult
        {
            Drainage = drainage,
            Accumulation = accumulation,
            Slope = slope,
            Rivers = rivers,
            Lakes = lakes,
            BasinId = basin,
            RiverLength = riverLength,
            RiverWidth = riverWidth,
            FilledElevation = filled
        };
    }

    private static void MarkLakeBasins(
        WorldTopology topology,
        float[] elevation,
        float[] filled,
        float[] depression,
        bool[] lakes)
    {
        var visited = new bool[elevation.Length];
        var queue = new Queue<int>();
        var component = new List<int>();

        for (var i = 0; i < elevation.Length; i++)
        {
            if (visited[i] || elevation[i] < 0f || depression[i] < 4f)
                continue;

            component.Clear();
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;
            var maxDepth = 0f;
            var maxElevation = float.MinValue;

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                component.Add(at);
                maxDepth = Math.Max(maxDepth, depression[at]);
                maxElevation = Math.Max(maxElevation, elevation[at]);

                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var ni = neighbors[n];
                    if (visited[ni] || elevation[ni] < 0f || depression[ni] < 4f)
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            var keep =
                component.Count >= 3 ||
                maxDepth >= 34f ||
                (component.Count >= 2 && maxElevation > 700f && maxDepth >= 16f);

            if (!keep)
                continue;

            foreach (var cell in component)
                lakes[cell] = true;
        }
    }

    private static void CarveMainRiverValleys(
        WorldTopology topology,
        float[] elevation,
        HydrologyResult hydro,
        ulong seed)
    {
        for (var i = 0; i < elevation.Length; i++)
        {
            if (!hydro.Rivers[i] || elevation[i] <= 5f)
                continue;

            var carve = Math.Clamp(
                5f + MathF.Log10(1f + hydro.Accumulation[i]) * 12f,
                5f,
                52f);
            elevation[i] = Math.Max(3f, elevation[i] - carve);

            // A very light one-ring valley halo avoids a river drawn over an
            // untouched plateau while preserving broad relief.
            var neighbors = topology.GetNeighborIndices(i);
            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (elevation[ni] <= 0f)
                    continue;
                var factor = 0.10f + Hash01(i + n, ni + 607, seed) * 0.07f;
                elevation[ni] = Math.Max(3f, elevation[ni] - carve * factor);
            }
        }
    }

    private static void ApplyLakeWaterDepth(
        float[] elevation,
        float[] water,
        HydrologyResult hydro)
    {
        for (var i = 0; i < elevation.Length; i++)
        {
            if (!hydro.Lakes[i])
                continue;

            water[i] = Math.Max(
                1.5f,
                hydro.FilledElevation[i] - elevation[i]);
        }
    }

    private static void ApplyRiverWaterDepth(float[] water, HydrologyResult hydro)
    {
        for (var i = 0; i < water.Length; i++)
        {
            if (!hydro.Rivers[i])
                continue;

            water[i] = Math.Clamp(0.55f + hydro.RiverWidth[i] * 0.58f, 0.55f, 4.2f);
        }
    }

    private static void FinalizeHydrologicSubstrate(
        float[] water,
        HydrologyResult hydro,
        SubstrateKind[] substrate)
    {
        for (var i = 0; i < water.Length; i++)
        {
            if (hydro.Rivers[i] || hydro.Lakes[i])
                substrate[i] = SubstrateKind.Sediment;
        }
    }

    private static void GenerateInitialClimate(
        WorldGenerationSettings settings,
        WorldTopology topology,
        CellId[] ids,
        ulong seed,
        float[] elevation,
        float[] water,
        HydrologyResult hydro,
        float[] uplift,
        float[] temperature,
        float[] humidity)
    {
        var climateOffset = settings.Climate switch
        {
            WorldClimate.Cold => -9f,
            WorldClimate.Warm => 7f,
            _ => 0f
        };

        var distanceToWater = DistanceToWater(topology, water);
        var windMoisture = new float[elevation.Length];
        var order = Enumerable.Range(0, elevation.Length).ToArray();
        Array.Sort(order, (a, b) =>
        {
            var qa = ids[a].Q;
            var qb = ids[b].Q;
            return qa != qb ? qa.CompareTo(qb) : ids[a].R.CompareTo(ids[b].R);
        });

        for (var i = 0; i < elevation.Length; i++)
        {
            var (_, y) = NormalizedWorldPosition(ids[i], settings.Radius);
            var latitude = Math.Clamp(Math.Abs(y), 0f, 1f);
            var regional = (Fbm(
                ids[i].Q / (float)settings.Radius * 2.1f + 79f,
                ids[i].R / (float)settings.Radius * 2.1f - 83f,
                SeedMixer.Combine(seed, 701),
                3) - 0.5f) * 4.5f;

            temperature[i] = Math.Clamp(
                30f +
                climateOffset -
                latitude * 29f -
                Math.Max(0f, elevation[i]) * 0.0062f +
                regional,
                -55f,
                48f);
        }

        foreach (var i in order)
        {
            var distance = distanceToWater[i] == int.MaxValue
                ? settings.Radius
                : distanceToWater[i];

            var maritime = water[i] > 0.1f
                ? 1f
                : MathF.Exp(-distance / Math.Max(7f, settings.Radius * 0.17f));

            var incoming = Math.Max(windMoisture[i], maritime * 0.82f);
            var orographicLoss = Math.Clamp(
                uplift[i] * 0.32f + Math.Max(0f, hydro.Slope[i] - 240f) / 2600f,
                0f,
                0.52f);

            var riverMoisture = hydro.Rivers[i]
                ? Math.Clamp(0.08f + hydro.RiverWidth[i] * 0.025f, 0.08f, 0.18f)
                : 0f;

            humidity[i] = water[i] > 0.1f
                ? 1f
                : Math.Clamp(
                    0.14f +
                    incoming * 0.68f +
                    riverMoisture -
                    orographicLoss,
                    0f,
                    1f);

            var outgoing = Math.Clamp(
                Math.Max(incoming, humidity[i]) * (1f - orographicLoss * 0.72f),
                0f,
                1f);

            var neighbors = topology.GetNeighborIndices(i);
            var deltaQ = topology.GetNeighborDeltaQ(i);
            for (var n = 0; n < neighbors.Length; n++)
            {
                if (deltaQ[n] <= 0)
                    continue;
                var ni = neighbors[n];
                windMoisture[ni] = Math.Max(windMoisture[ni], outgoing * 0.94f);
            }
        }
    }

    private static void GenerateResources(
        float[] humidity,
        HydrologyResult hydro,
        float[] minerals,
        SubstrateKind[] substrate,
        float[] nutrients)
    {
        for (var i = 0; i < nutrients.Length; i++)
        {
            var sediment = substrate[i] == SubstrateKind.Sediment ? 0.20f : 0f;
            var alluvial = hydro.Rivers[i] || hydro.Lakes[i]
                ? Math.Min(0.18f, MathF.Log10(1f + hydro.Accumulation[i]) * 0.055f)
                : 0f;

            nutrients[i] = Math.Clamp(
                minerals[i] * 0.44f +
                humidity[i] * 0.22f +
                sediment +
                alluvial,
                0f,
                1f);
        }
    }

    private static WorldGenerationSummary Summarize(
        WorldTopology topology,
        GeneratedWorldCell[] cells)
    {
        var land = 0;
        var mountains = 0;
        var rivers = 0;
        var lakes = 0;
        var minE = float.MaxValue;
        var maxE = float.MinValue;
        var maxW = 0f;
        var minT = float.MaxValue;
        var maxT = float.MinValue;
        var minH = float.MaxValue;
        var maxH = float.MinValue;

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell.ElevationMeters >= 0f)
                land++;
            if (cell.ElevationMeters > 1500f && cell.Slope > 160f)
                mountains++;
            if (cell.IsRiver)
                rivers++;
            if (cell.IsLake)
                lakes++;

            minE = Math.Min(minE, cell.ElevationMeters);
            maxE = Math.Max(maxE, cell.ElevationMeters);
            maxW = Math.Max(maxW, cell.WaterDepthMeters);
            minT = Math.Min(minT, cell.TemperatureCelsius);
            maxT = Math.Max(maxT, cell.TemperatureCelsius);
            minH = Math.Min(minH, cell.Humidity);
            maxH = Math.Max(maxH, cell.Humidity);
        }

        var landSizes = ComponentSizes(topology, cells, static c => c.ElevationMeters >= 0f);
        var largest = landSizes.Count == 0 ? 0 : landSizes[0];
        var islandLimit = Math.Max(4, cells.Length / 250);
        var islands = landSizes.Count(size => size < islandLimit);

        return new WorldGenerationSummary(
            cells.Length,
            land / (float)Math.Max(1, cells.Length),
            landSizes.Count,
            largest,
            islands,
            mountains,
            rivers,
            lakes,
            minE,
            maxE,
            maxW,
            minT,
            maxT,
            minH,
            maxH);
    }

    private static WorldGenerationQuality AnalyzeQuality(
        WorldTopology topology,
        GeneratedWorldCell[] cells,
        HydrologyResult hydro)
    {
        var landSizes = ComponentSizes(topology, cells, static c => c.ElevationMeters >= 0f);
        var landCellCount = Math.Max(1, landSizes.Sum());
        var continentThreshold = Math.Max(80, (int)MathF.Round(landCellCount * 0.035f));
        var continentCount = landSizes.Count(size => size >= continentThreshold);
        var secondLargest = landSizes.Count > 1 ? landSizes[1] : 0;
        var tinyIslandThreshold = Math.Max(4, cells.Length / 1800);
        var tinyIslands = landSizes.Count(size => size <= tinyIslandThreshold);

        var inlandWaterComponents = CountInlandWaterComponents(topology, cells);
        var coastlineEdges = 0;
        var boundaryCells = 0;
        var boundaryOcean = 0;
        var slopeSum = 0.0;

        for (var i = 0; i < cells.Length; i++)
        {
            slopeSum += cells[i].Slope;
            var neighbors = topology.GetNeighborIndices(i);
            if (neighbors.Length < 6)
            {
                boundaryCells++;
                if (cells[i].ElevationMeters < 0f)
                    boundaryOcean++;
            }

            if (cells[i].ElevationMeters < 0f)
                continue;

            for (var n = 0; n < neighbors.Length; n++)
                if (cells[neighbors[n]].ElevationMeters < 0f)
                    coastlineEdges++;
        }

        var landCells = Math.Max(1, cells.Count(c => c.ElevationMeters >= 0f));
        var coastlineComplexity = coastlineEdges / MathF.Sqrt(landCells);

        var mountainRanges = CountComponents(
            topology,
            cells.Length,
            i => cells[i].ElevationMeters > 1250f &&
                 (cells[i].Slope > 120f || cells[i].TectonicUplift > 0.38f),
            minimumSize: 4);

        var riverComponents = CountComponents(
            topology,
            cells.Length,
            i => hydro.Rivers[i],
            minimumSize: 3);

        var lakeSizes = ComponentSizes(
            topology,
            cells,
            static c => c.IsLake);

        var tributaries = 0;
        for (var i = 0; i < cells.Length; i++)
        {
            if (!hydro.Rivers[i])
                continue;
            var upstream = 0;
            var neighbors = topology.GetNeighborIndices(i);
            for (var n = 0; n < neighbors.Length; n++)
                if (hydro.Rivers[neighbors[n]] && hydro.Drainage[neighbors[n]] == i)
                    upstream++;
            if (upstream >= 2)
                tributaries++;
        }

        var basinIds = new HashSet<int>();
        for (var i = 0; i < hydro.BasinId.Length; i++)
            if (hydro.BasinId[i] >= 0)
                basinIds.Add(hydro.BasinId[i]);

        var longestRiver = 0;
        var riverCells = 0;
        for (var i = 0; i < hydro.Rivers.Length; i++)
        {
            if (!hydro.Rivers[i])
                continue;
            riverCells++;
            longestRiver = Math.Max(longestRiver, hydro.RiverLength[i]);
        }

        return new WorldGenerationQuality(
            ContinentCount: continentCount,
            SecondLargestContinentCells: secondLargest,
            TinyIslandCount: tinyIslands,
            InlandWaterComponents: inlandWaterComponents,
            CoastlineEdges: coastlineEdges,
            CoastlineComplexity: coastlineComplexity,
            MountainRangeCount: mountainRanges,
            RiverCount: riverComponents,
            RiverTotalLength: riverCells,
            LongestRiver: longestRiver,
            TributaryCount: tributaries,
            LakeCount: lakeSizes.Count,
            LargestLakeCells: lakeSizes.Count == 0 ? 0 : lakeSizes[0],
            DrainageBasinCount: basinIds.Count,
            BoundaryOceanRatio: boundaryCells == 0 ? 1f : boundaryOcean / (float)boundaryCells,
            MeanSlope: (float)(slopeSum / Math.Max(1, cells.Length)));
    }

    private static List<int> ComponentSizes(
        WorldTopology topology,
        GeneratedWorldCell[] cells,
        Func<GeneratedWorldCell, bool> include)
    {
        var visited = new bool[cells.Length];
        var queue = new Queue<int>();
        var sizes = new List<int>();

        for (var i = 0; i < cells.Length; i++)
        {
            if (visited[i] || !include(cells[i]))
                continue;

            var size = 0;
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                size++;
                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var ni = neighbors[n];
                    if (visited[ni] || !include(cells[ni]))
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            sizes.Add(size);
        }

        sizes.Sort(static (a, b) => b.CompareTo(a));
        return sizes;
    }

    private static int CountComponents(
        WorldTopology topology,
        int count,
        Func<int, bool> include,
        int minimumSize)
    {
        var visited = new bool[count];
        var queue = new Queue<int>();
        var components = 0;

        for (var i = 0; i < count; i++)
        {
            if (visited[i] || !include(i))
                continue;

            var size = 0;
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                size++;
                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var ni = neighbors[n];
                    if (visited[ni] || !include(ni))
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            if (size >= minimumSize)
                components++;
        }

        return components;
    }

    private static int CountInlandWaterComponents(
        WorldTopology topology,
        GeneratedWorldCell[] cells)
    {
        var visited = new bool[cells.Length];
        var queue = new Queue<int>();
        var count = 0;

        for (var i = 0; i < cells.Length; i++)
        {
            if (visited[i] || cells[i].ElevationMeters >= 0f)
                continue;

            var touchesBoundary = false;
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                if (topology.GetNeighborIndices(at).Length < 6)
                    touchesBoundary = true;

                var neighbors = topology.GetNeighborIndices(at);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    var ni = neighbors[n];
                    if (visited[ni] || cells[ni].ElevationMeters >= 0f)
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            if (!touchesBoundary)
                count++;
        }

        return count;
    }

    private static int[] DistanceToWater(WorldTopology topology, float[] water)
    {
        var distance = new int[water.Length];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();

        for (var i = 0; i < water.Length; i++)
        {
            if (water[i] <= 0.1f)
                continue;
            distance[i] = 0;
            queue.Enqueue(i);
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            var next = distance[at] + 1;
            var neighbors = topology.GetNeighborIndices(at);

            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (distance[ni] <= next)
                    continue;
                distance[ni] = next;
                queue.Enqueue(ni);
            }
        }

        return distance;
    }

    private static float ChooseSeaLevel(
        WorldGenerationSettings settings,
        float[] elevation)
    {
        var copy = (float[])elevation.Clone();
        Array.Sort(copy);

        var targetLand = settings.LandAmount switch
        {
            WorldLandAmount.Low => 0.28f,
            WorldLandAmount.High => 0.52f,
            _ => 0.40f
        };

        var index = Math.Clamp(
            (int)((1f - targetLand) * (copy.Length - 1)),
            0,
            copy.Length - 1);

        return copy[index];
    }

    private static CellId[] BuildCells(int radius)
    {
        var cells = new List<CellId>(1 + 3 * radius * (radius + 1));

        for (var q = -radius; q <= radius; q++)
        {
            var minR = Math.Max(-radius, -q - radius);
            var maxR = Math.Min(radius, -q + radius);
            for (var r = minR; r <= maxR; r++)
                cells.Add(CellId.FromAxial(q, r));
        }

        return cells.ToArray();
    }

    private static WorldTopology BuildTopology(CellId[] ids)
    {
        var set = new HashSet<CellId>(ids);
        var builder = new WorldTopologyBuilder();

        foreach (var id in ids)
        {
            var neighbors = new List<CellId>(6);
            foreach (var direction in Directions)
            {
                var neighbor = CellId.FromAxial(id.Q + direction.Q, id.R + direction.R);
                if (set.Contains(neighbor))
                    neighbors.Add(neighbor);
            }
            builder.Add(id, neighbors);
        }

        return builder.Build();
    }

    private static void FindNearestPlates(
        Plate[] plates,
        float x,
        float y,
        out int first,
        out int second,
        out float firstDistance,
        out float secondDistance)
    {
        first = 0;
        second = 0;
        firstDistance = float.MaxValue;
        secondDistance = float.MaxValue;

        for (var i = 0; i < plates.Length; i++)
        {
            var dx = x - plates[i].X;
            var dy = y - plates[i].Y;
            var distance = dx * dx + dy * dy;

            if (distance < firstDistance)
            {
                second = first;
                secondDistance = firstDistance;
                first = i;
                firstDistance = distance;
            }
            else if (distance < secondDistance)
            {
                second = i;
                secondDistance = distance;
            }
        }
    }

    private static (float X, float Y) NormalizedWorldPosition(CellId id, int radius)
    {
        var x = (id.Q + id.R * 0.5f) / radius;
        var y = id.R * 0.8660254f / radius;
        return (x, y);
    }

    private static float SignedHash(ulong seed, int channel)
    {
        return Hash01(channel * 17 + 3, channel * 31 + 7, seed) * 2f - 1f;
    }

    private static float Fbm(float x, float y, ulong seed, int octaves)
    {
        var value = 0f;
        var amplitude = 0.5f;
        var frequency = 1f;
        var total = 0f;

        for (var octave = 0; octave < octaves; octave++)
        {
            value += ValueNoise(
                x * frequency,
                y * frequency,
                SeedMixer.Combine(seed, (ulong)octave + 1)) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return total <= 0f ? 0f : value / total;
    }

    private static float ValueNoise(float x, float y, ulong seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var tx = Smooth(x - x0);
        var ty = Smooth(y - y0);

        var a = Hash01(x0, y0, seed);
        var b = Hash01(x0 + 1, y0, seed);
        var c = Hash01(x0, y0 + 1, seed);
        var d = Hash01(x0 + 1, y0 + 1, seed);

        return Lerp(
            Lerp(a, b, tx),
            Lerp(c, d, tx),
            ty);
    }

    private static float Hash01(int x, int y, ulong seed)
    {
        var h = SeedMixer.Combine(
            seed,
            unchecked(((ulong)(uint)x << 32) | (uint)y));
        return (float)(h >> 40) * (1f / 16777215f);
    }

    private static float Smooth(float value) => value * value * (3f - 2f * value);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
