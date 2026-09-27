using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Evolit.Core;

public static class PlanetGenerationScale
{
    public const int SmallFrequency = 31;
    public const int MediumFrequency = 42;
    public const int LargeFrequency = 54;

    public static int FrequencyForSize(string sizeName) => sizeName switch
    {
        "Маленький" => SmallFrequency,
        "Большой" => LargeFrequency,
        _ => MediumFrequency
    };

    public static int CellCountForFrequency(int frequency) => 10 * frequency * frequency + 2;
}

public sealed class PlanetSurfaceGeometry
{
    public required CoreVector3[] Centers { get; init; }
    public required int[] PolygonOffsets { get; init; }
    public required CoreVector3[] PolygonVertices { get; init; }

    public int Count => Centers.Length;

    public ReadOnlySpan<CoreVector3> GetPolygon(int index)
    {
        var start = PolygonOffsets[index];
        return PolygonVertices.AsSpan(start, PolygonOffsets[index + 1] - start);
    }
}

public readonly record struct PlanetGenerationSettings(
    string Seed,
    int Frequency,
    WorldLandAmount LandAmount = WorldLandAmount.Normal,
    WorldClimate Climate = WorldClimate.Temperate,
    GeologicalActivity Geology = GeologicalActivity.Normal);

public sealed class GeneratedPlanetWorld
{
    public required PlanetGenerationSettings Settings { get; init; }
    public required WorldTopology Topology { get; init; }
    public required PlanetSurfaceGeometry Geometry { get; init; }
    public required GeneratedWorldCell[] Cells { get; init; }
    public required EnvironmentStore Environment { get; init; }
    public required WorldGenerationSummary Summary { get; init; }
    public required WorldGenerationMetrics Metrics { get; init; }
}

public static class PlanetTopologyFactory
{
    private static readonly CoreVector3[] BaseVertices = CreateBaseVertices();
    private static readonly int[,] Faces =
    {
        { 0, 11, 5 }, { 0, 5, 1 }, { 0, 1, 7 }, { 0, 7, 10 }, { 0, 10, 11 },
        { 1, 5, 9 }, { 5, 11, 4 }, { 11, 10, 2 }, { 10, 7, 6 }, { 7, 1, 8 },
        { 3, 9, 4 }, { 3, 4, 2 }, { 3, 2, 6 }, { 3, 6, 8 }, { 3, 8, 9 },
        { 4, 9, 5 }, { 2, 4, 11 }, { 6, 2, 10 }, { 8, 6, 7 }, { 9, 8, 1 }
    };

    public static (WorldTopology Topology, PlanetSurfaceGeometry Geometry) Build(int frequency)
    {
        if (frequency < 1)
            throw new ArgumentOutOfRangeException(nameof(frequency));

        var positions = new List<CoreVector3>(PlanetGenerationScale.CellCountForFrequency(frequency));
        var vertexLookup = new Dictionary<VertexKey, int>();
        var adjacency = new List<HashSet<int>>();
        var incidentTriangles = new List<List<int>>();
        var triangles = new List<Triangle>(20 * frequency * frequency);

        int AddVertex(CoreVector3 value)
        {
            var normal = value.Normalized();
            var key = VertexKey.From(normal);
            if (vertexLookup.TryGetValue(key, out var existing))
                return existing;

            var index = positions.Count;
            positions.Add(normal);
            vertexLookup.Add(key, index);
            adjacency.Add(new HashSet<int>());
            incidentTriangles.Add(new List<int>(6));
            return index;
        }

        void AddTriangle(int a, int b, int c)
        {
            var va = positions[a];
            var vb = positions[b];
            var vc = positions[c];
            var normal = CoreVector3.Cross(vb - va, vc - va);
            var center = (va + vb + vc).Normalized();
            if (CoreVector3.Dot(normal, center) < 0f)
                (b, c) = (c, b);

            var triangleIndex = triangles.Count;
            triangles.Add(new Triangle(a, b, c, center));
            incidentTriangles[a].Add(triangleIndex);
            incidentTriangles[b].Add(triangleIndex);
            incidentTriangles[c].Add(triangleIndex);
            AddEdge(a, b);
            AddEdge(b, c);
            AddEdge(c, a);
        }

        void AddEdge(int a, int b)
        {
            adjacency[a].Add(b);
            adjacency[b].Add(a);
        }

        for (var face = 0; face < Faces.GetLength(0); face++)
        {
            var a = BaseVertices[Faces[face, 0]];
            var b = BaseVertices[Faces[face, 1]];
            var c = BaseVertices[Faces[face, 2]];
            var grid = new Dictionary<(int I, int J), int>((frequency + 1) * (frequency + 2) / 2);

            for (var i = 0; i <= frequency; i++)
            {
                for (var j = 0; j <= frequency - i; j++)
                {
                    var k = frequency - i - j;
                    var point = (a * k + b * i + c * j) / frequency;
                    grid[(i, j)] = AddVertex(point);
                }
            }

            for (var i = 0; i < frequency; i++)
            {
                for (var j = 0; j < frequency - i; j++)
                {
                    AddTriangle(grid[(i, j)], grid[(i + 1, j)], grid[(i, j + 1)]);
                    if (j < frequency - i - 1)
                        AddTriangle(grid[(i + 1, j)], grid[(i + 1, j + 1)], grid[(i, j + 1)]);
                }
            }
        }

        var expected = PlanetGenerationScale.CellCountForFrequency(frequency);
        if (positions.Count != expected)
            throw new InvalidOperationException($"Geodesic topology produced {positions.Count} cells; expected {expected}.");

        var ids = new CellId[positions.Count];
        var neighborOffsets = new int[positions.Count + 1];
        var flatNeighbors = new List<CellId>(positions.Count * 6);
        var polygonOffsets = new int[positions.Count + 1];
        var polygonVertices = new List<CoreVector3>(positions.Count * 6);
        var pentagons = 0;

        for (var index = 0; index < positions.Count; index++)
        {
            ids[index] = CellId.FromPlanetIndex(index);
            var center = positions[index];
            var tangentA = TangentA(center);
            var tangentB = CoreVector3.Cross(center, tangentA).Normalized();

            var neighbors = new List<int>(adjacency[index]);
            neighbors.Sort((left, right) =>
                TangentAngle(positions[left], tangentA, tangentB).CompareTo(
                    TangentAngle(positions[right], tangentA, tangentB)));

            if (neighbors.Count == 5)
                pentagons++;
            else if (neighbors.Count != 6)
                throw new InvalidOperationException($"Planet cell {index} has invalid degree {neighbors.Count}.");

            neighborOffsets[index] = flatNeighbors.Count;
            foreach (var neighbor in neighbors)
                flatNeighbors.Add(CellId.FromPlanetIndex(neighbor));

            var corners = incidentTriangles[index];
            corners.Sort((left, right) =>
                TangentAngle(triangles[left].Center, tangentA, tangentB).CompareTo(
                    TangentAngle(triangles[right].Center, tangentA, tangentB)));
            polygonOffsets[index] = polygonVertices.Count;
            foreach (var triangleIndex in corners)
                polygonVertices.Add(triangles[triangleIndex].Center);
        }

        neighborOffsets[^1] = flatNeighbors.Count;
        polygonOffsets[^1] = polygonVertices.Count;

        if (pentagons != 12)
            throw new InvalidOperationException($"Geodesic topology must contain exactly 12 pentagons; got {pentagons}.");

        var directions = positions.ToArray();
        var topology = new WorldTopology(
            ids,
            neighborOffsets,
            flatNeighbors.ToArray(),
            WorldTopologyKind.Planet,
            directions);
        var geometry = new PlanetSurfaceGeometry
        {
            Centers = directions,
            PolygonOffsets = polygonOffsets,
            PolygonVertices = polygonVertices.ToArray()
        };
        return (topology, geometry);
    }

    private static CoreVector3[] CreateBaseVertices()
    {
        var phi = (1f + MathF.Sqrt(5f)) * 0.5f;
        CoreVector3[] result =
        [
            new(-1, phi, 0), new(1, phi, 0), new(-1, -phi, 0), new(1, -phi, 0),
            new(0, -1, phi), new(0, 1, phi), new(0, -1, -phi), new(0, 1, -phi),
            new(phi, 0, -1), new(phi, 0, 1), new(-phi, 0, -1), new(-phi, 0, 1)
        ];
        for (var i = 0; i < result.Length; i++)
            result[i] = result[i].Normalized();
        return result;
    }

    private static CoreVector3 TangentA(CoreVector3 normal)
    {
        var reference = Math.Abs(normal.Y) < 0.9f ? CoreVector3.UnitY : CoreVector3.UnitX;
        return CoreVector3.Cross(reference, normal).Normalized();
    }

    private static float TangentAngle(CoreVector3 point, CoreVector3 tangentA, CoreVector3 tangentB)
    {
        return MathF.Atan2(CoreVector3.Dot(point, tangentB), CoreVector3.Dot(point, tangentA));
    }

    private readonly record struct Triangle(int A, int B, int C, CoreVector3 Center);
    private readonly record struct VertexKey(long X, long Y, long Z)
    {
        public static VertexKey From(CoreVector3 value) => new(
            (long)MathF.Round(value.X * 100_000_000f),
            (long)MathF.Round(value.Y * 100_000_000f),
            (long)MathF.Round(value.Z * 100_000_000f));
    }
}

public static class PlanetWorldGenerator
{
    public static GeneratedPlanetWorld Generate(PlanetGenerationSettings settings)
    {
        if (settings.Frequency < 2)
            throw new ArgumentOutOfRangeException(nameof(settings.Frequency));

        var totalStarted = Stopwatch.GetTimestamp();
        var topologyStarted = Stopwatch.GetTimestamp();
        var (topology, geometry) = PlanetTopologyFactory.Build(settings.Frequency);
        var topologyMs = Stopwatch.GetElapsedTime(topologyStarted).TotalMilliseconds;
        var count = topology.Count;
        var seed = SeedMixer.FromString(settings.Seed ?? string.Empty);

        var elevationStarted = Stopwatch.GetTimestamp();
        var elevation = new float[count];
        var continentalness = new float[count];
        var uplift = new float[count];
        var province = new int[count];
        var plates = BuildPlates(settings, seed);
        var raw = new float[count];

        for (var i = 0; i < count; i++)
        {
            var p = geometry.Centers[i];
            var bestDot = -2f;
            var secondDot = -2f;
            var bestPlate = 0;
            for (var plateIndex = 0; plateIndex < plates.Length; plateIndex++)
            {
                var dot = CoreVector3.Dot(p, plates[plateIndex].Center);
                if (dot > bestDot)
                {
                    secondDot = bestDot;
                    bestDot = dot;
                    bestPlate = plateIndex;
                }
                else if (dot > secondDot)
                {
                    secondDot = dot;
                }
            }

            var plate = plates[bestPlate];
            var boundary = Math.Clamp(1f - (bestDot - secondDot) * 14f, 0f, 1f);
            var macro = Fbm(p * 1.35f, SeedMixer.Combine(seed, 101), 4);
            var regional = Fbm(p * 3.1f, SeedMixer.Combine(seed, 102), 3);
            var ridgeNoise = 1f - Math.Abs(Fbm(p * 5.4f, SeedMixer.Combine(seed, 103), 3));
            var geologyScale = settings.Geology switch
            {
                GeologicalActivity.Calm => 0.58f,
                GeologicalActivity.Active => 1.35f,
                _ => 1f
            };
            var plateBias = plate.Continental ? 0.30f : -0.22f;
            var tectonic = boundary * (0.35f + plate.Uplift * 0.65f) * geologyScale;
            province[i] = bestPlate;
            uplift[i] = Math.Clamp(tectonic, 0f, 1f);
            raw[i] = plateBias + macro * 0.72f + regional * 0.22f + tectonic * 0.24f + ridgeNoise * tectonic * 0.18f;
        }

        var targetLand = settings.LandAmount switch
        {
            WorldLandAmount.Low => 0.28f,
            WorldLandAmount.High => 0.52f,
            _ => 0.40f
        };
        var sortedRaw = (float[])raw.Clone();
        Array.Sort(sortedRaw);
        var thresholdIndex = Math.Clamp((int)MathF.Round((1f - targetLand) * (sortedRaw.Length - 1)), 0, sortedRaw.Length - 1);
        var seaThreshold = sortedRaw[thresholdIndex];

        for (var i = 0; i < count; i++)
        {
            var p = geometry.Centers[i];
            var c = raw[i] - seaThreshold;
            continentalness[i] = c;
            var detail = Fbm(p * 11.7f, SeedMixer.Combine(seed, 104), 3);
            if (c >= 0f)
            {
                var normalized = Math.Clamp(c / 0.9f, 0f, 1f);
                elevation[i] = normalized * 2350f + Math.Max(0f, uplift[i] - 0.30f) * 1550f + detail * 230f;
            }
            else
            {
                var normalized = Math.Clamp(-c / 0.9f, 0f, 1f);
                elevation[i] = -(90f + normalized * 2100f + Math.Max(0f, -detail) * 240f);
            }
            elevation[i] = Math.Clamp(elevation[i], -4300f, 3900f);
        }
        var macroElevationMs = Stopwatch.GetElapsedTime(elevationStarted).TotalMilliseconds;

        var coastStarted = Stopwatch.GetTimestamp();
        var coastDistance = ComputeCoastDistance(topology, elevation);
        for (var i = 0; i < count; i++)
        {
            if (elevation[i] >= 0f)
                continue;
            var deepening = Math.Min(1900f, Math.Max(0, coastDistance[i] - 1) * 95f);
            elevation[i] = Math.Max(-5200f, elevation[i] - deepening);
        }
        var coastBathymetryMs = Stopwatch.GetElapsedTime(coastStarted).TotalMilliseconds;

        var geologyStarted = Stopwatch.GetTimestamp();
        var slope = new float[count];
        var substrate = new SubstrateKind[count];
        var geothermal = new float[count];
        for (var i = 0; i < count; i++)
        {
            var neighbors = topology.GetNeighborIndices(i);
            var maxSlope = 0f;
            for (var n = 0; n < neighbors.Length; n++)
                maxSlope = Math.Max(maxSlope, Math.Abs(elevation[i] - elevation[neighbors[n]]));
            slope[i] = maxSlope;
            geothermal[i] = Math.Clamp(
                0.18f + uplift[i] * 0.62f + (Fbm(geometry.Centers[i] * 7.2f, SeedMixer.Combine(seed, 202), 2) + 1f) * 0.10f,
                0f,
                1f);
            substrate[i] = elevation[i] < 0f
                ? SubstrateKind.Sediment
                : uplift[i] > 0.78f && geothermal[i] > 0.68f
                    ? SubstrateKind.Basalt
                    : elevation[i] > 2200f || slope[i] > 620f
                        ? SubstrateKind.BareRock
                        : coastDistance[i] <= 1 && elevation[i] < 220f
                            ? SubstrateKind.Sand
                            : SubstrateKind.MineralRegolith;
        }
        var geologyMs = Stopwatch.GetElapsedTime(geologyStarted).TotalMilliseconds;

        var hydroStarted = Stopwatch.GetTimestamp();
        var drainage = new int[count];
        var accumulation = new float[count];
        var lakes = new bool[count];
        var rivers = new bool[count];
        var basin = new int[count];
        var riverLength = new int[count];
        var riverWidth = new float[count];
        var streamOrder = new int[count];
        var upstreamBranches = new int[count];
        var riverDirection = new int[count];
        var hydraulicSurface = (float[])elevation.Clone();
        var filledDepth = new float[count];
        Array.Fill(drainage, -1);
        Array.Fill(basin, -1);
        Array.Fill(riverDirection, -1);
        Array.Fill(accumulation, 1f);

        // Closed-sphere priority flood. Ocean cells are the only global sinks;
        // every land cell receives a deterministic downstream route through the
        // topology graph, so drainage never relies on a map edge.
        var flood = new PriorityQueue<int, float>();
        var flooded = new bool[count];
        for (var i = 0; i < count; i++)
        {
            if (elevation[i] >= 0f)
                continue;
            flooded[i] = true;
            flood.Enqueue(i, hydraulicSurface[i]);
        }

        while (flood.Count > 0)
        {
            var at = flood.Dequeue();
            var neighbors = topology.GetNeighborIndices(at);
            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (flooded[ni])
                    continue;

                flooded[ni] = true;
                var spillSurface = Math.Max(elevation[ni], hydraulicSurface[at] + 0.01f);
                hydraulicSurface[ni] = spillSurface;
                filledDepth[ni] = Math.Max(0f, spillSurface - elevation[ni]);
                drainage[ni] = at;

                var reverse = topology.GetNeighborIndices(ni);
                for (var r = 0; r < reverse.Length; r++)
                {
                    if (reverse[r] == at)
                    {
                        riverDirection[ni] = r;
                        break;
                    }
                }

                flood.Enqueue(ni, spillSurface);
            }
        }

        for (var i = 0; i < count; i++)
        {
            if (elevation[i] < 0f)
                continue;
            lakes[i] = filledDepth[i] > 1.5f;
        }

        var descending = CreateIndexOrder(count);
        Array.Sort(descending, (a, b) =>
        {
            var surface = hydraulicSurface[b].CompareTo(hydraulicSurface[a]);
            return surface != 0 ? surface : a.CompareTo(b);
        });
        foreach (var i in descending)
        {
            var target = drainage[i];
            if (target >= 0)
                accumulation[target] += accumulation[i];
        }

        var riverThreshold = Math.Max(20f, count / 1250f);
        for (var i = 0; i < count; i++)
        {
            if (elevation[i] < 0f || lakes[i] || drainage[i] < 0 || accumulation[i] < riverThreshold)
                continue;
            rivers[i] = true;
            riverWidth[i] = Math.Clamp(0.45f + MathF.Log2(1f + accumulation[i] / riverThreshold) * 0.65f, 0.45f, 4.5f);
            streamOrder[i] = Math.Clamp(1 + (int)MathF.Log2(1f + accumulation[i] / riverThreshold), 1, 8);
        }

        var basinByRoot = new Dictionary<int, int>();
        var nextBasin = 0;
        for (var i = 0; i < count; i++)
        {
            if (elevation[i] < 0f)
                continue;
            var at = i;
            var guard = 0;
            while (drainage[at] >= 0 && elevation[at] >= 0f && guard++ < count)
                at = drainage[at];
            if (!basinByRoot.TryGetValue(at, out var id))
            {
                id = nextBasin++;
                basinByRoot.Add(at, id);
            }
            basin[i] = id;
        }

        // Length is distance to the next downstream non-river/ocean segment.
        // Iterate from low to high surface so the downstream length is known.
        for (var orderIndex = descending.Length - 1; orderIndex >= 0; orderIndex--)
        {
            var i = descending[orderIndex];
            if (!rivers[i])
                continue;
            var target = drainage[i];
            riverLength[i] = target >= 0 && rivers[target] ? riverLength[target] + 1 : 1;
            if (target >= 0 && rivers[target])
                upstreamBranches[target]++;
        }
        var hydrologyMs = Stopwatch.GetElapsedTime(hydroStarted).TotalMilliseconds;

        var climateStarted = Stopwatch.GetTimestamp();
        var temperature = new float[count];
        var humidity = new float[count];
        var pressure = new float[count];
        var climateOffset = settings.Climate switch
        {
            WorldClimate.Cold => -10f,
            WorldClimate.Warm => 7f,
            _ => 0f
        };
        for (var i = 0; i < count; i++)
        {
            var p = geometry.Centers[i];
            var latitude = Math.Clamp(Math.Abs(p.Y), 0f, 1f);
            var tempNoise = Fbm(p * 4.4f, SeedMixer.Combine(seed, 301), 3) * 3.5f;
            temperature[i] = Math.Clamp(
                29f + climateOffset - latitude * 34f - Math.Max(0f, elevation[i]) * 0.0062f + tempNoise,
                -65f,
                52f);
            var coastMoisture = MathF.Exp(-Math.Max(0, coastDistance[i]) * 0.16f);
            var humidityNoise = Fbm(p * 5.7f, SeedMixer.Combine(seed, 302), 3) * 0.16f;
            humidity[i] = Math.Clamp(
                0.30f + coastMoisture * 0.42f + humidityNoise + (elevation[i] < 0f ? 0.25f : 0f),
                0f,
                1f);
            pressure[i] = Math.Max(20f, 101.325f * MathF.Exp(-Math.Max(-500f, elevation[i]) / 8434f));
        }
        var climateMs = Stopwatch.GetElapsedTime(climateStarted).TotalMilliseconds;

        var resourcesStarted = Stopwatch.GetTimestamp();
        var minerals = new float[count];
        var nutrients = new float[count];
        for (var i = 0; i < count; i++)
        {
            var p = geometry.Centers[i];
            minerals[i] = Math.Clamp(
                0.26f + uplift[i] * 0.30f + geothermal[i] * 0.20f + Fbm(p * 9.4f, SeedMixer.Combine(seed, 401), 2) * 0.18f,
                0f,
                1f);
            nutrients[i] = elevation[i] < 0f
                ? Math.Clamp(0.25f + humidity[i] * 0.30f, 0f, 1f)
                : Math.Clamp(0.12f + humidity[i] * 0.48f - Math.Clamp(elevation[i] / 5000f, 0f, 0.4f), 0f, 1f);
        }
        var resourcesMs = Stopwatch.GetElapsedTime(resourcesStarted).TotalMilliseconds;

        var environmentStarted = Stopwatch.GetTimestamp();
        var environment = new EnvironmentStore(topology);
        var cells = new GeneratedWorldCell[count];
        for (var i = 0; i < count; i++)
        {
            var ocean = elevation[i] < 0f;
            var lake = lakes[i];
            var river = rivers[i];
            var waterDepth = ocean
                ? -elevation[i]
                : lake
                    ? Math.Clamp(Math.Max(1.5f, filledDepth[i]), 1.5f, 160f)
                    : river
                        ? Math.Clamp(0.8f + riverWidth[i] * 0.7f, 0.8f, 5f)
                        : 0f;

            cells[i] = new GeneratedWorldCell(
                topology.GetCellId(i),
                elevation[i],
                waterDepth,
                temperature[i],
                humidity[i],
                pressure[i],
                minerals[i],
                nutrients[i],
                geothermal[i],
                substrate[i],
                drainage[i],
                accumulation[i],
                slope[i],
                river,
                lake,
                province[i],
                continentalness[i],
                uplift[i],
                coastDistance[i],
                basin[i],
                riverLength[i],
                riverWidth[i],
                streamOrder[i],
                upstreamBranches[i],
                riverDirection[i]);

            environment.SetInitial(topology.GetCellId(i), new EnvironmentCellState(
                elevation[i],
                waterDepth,
                temperature[i],
                humidity[i],
                pressure[i],
                ocean && waterDepth > 100f ? 0.55f : 1f,
                minerals[i],
                nutrients[i],
                0f,
                0f,
                geothermal[i],
                substrate[i]));
        }
        var environmentBuildMs = Stopwatch.GetElapsedTime(environmentStarted).TotalMilliseconds;
        var summary = Summarize(topology, cells);
        var totalMs = Stopwatch.GetElapsedTime(totalStarted).TotalMilliseconds;

        return new GeneratedPlanetWorld
        {
            Settings = settings,
            Topology = topology,
            Geometry = geometry,
            Cells = cells,
            Environment = environment,
            Summary = summary,
            Metrics = new WorldGenerationMetrics(
                topologyMs,
                macroElevationMs,
                coastBathymetryMs,
                geologyMs,
                hydrologyMs,
                climateMs,
                resourcesMs,
                environmentBuildMs,
                totalMs)
        };
    }

    private static Plate[] BuildPlates(PlanetGenerationSettings settings, ulong seed)
    {
        var count = settings.Frequency switch
        {
            <= PlanetGenerationScale.SmallFrequency => 12,
            >= PlanetGenerationScale.LargeFrequency => 20,
            _ => 16
        };
        var result = new Plate[count];
        var rng = new DeterministicRandom(SeedMixer.Combine(seed, 55));
        for (var i = 0; i < count; i++)
        {
            var z = rng.NextFloat() * 2f - 1f;
            var angle = rng.NextFloat() * MathF.PI * 2f;
            var radial = MathF.Sqrt(Math.Max(0f, 1f - z * z));
            result[i] = new Plate(
                new CoreVector3(radial * MathF.Cos(angle), z, radial * MathF.Sin(angle)),
                rng.NextFloat() > 0.43f,
                rng.NextFloat(),
                rng.NextFloat());
        }
        return result;
    }

    private static int[] ComputeCoastDistance(WorldTopology topology, float[] elevation)
    {
        var result = new int[elevation.Length];
        Array.Fill(result, int.MaxValue);
        var queue = new Queue<int>();
        for (var i = 0; i < elevation.Length; i++)
        {
            var land = elevation[i] >= 0f;
            var neighbors = topology.GetNeighborIndices(i);
            for (var n = 0; n < neighbors.Length; n++)
            {
                if ((elevation[neighbors[n]] >= 0f) == land)
                    continue;
                result[i] = 0;
                queue.Enqueue(i);
                break;
            }
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            var next = result[at] + 1;
            var neighbors = topology.GetNeighborIndices(at);
            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                if (result[ni] <= next)
                    continue;
                result[ni] = next;
                queue.Enqueue(ni);
            }
        }

        for (var i = 0; i < result.Length; i++)
            if (result[i] == int.MaxValue)
                result[i] = 0;
        return result;
    }

    private static WorldGenerationSummary Summarize(WorldTopology topology, GeneratedWorldCell[] cells)
    {
        var land = 0;
        var mountains = 0;
        var rivers = 0;
        var lakes = 0;
        var minElevation = float.MaxValue;
        var maxElevation = float.MinValue;
        var maxWater = 0f;
        var minTemperature = float.MaxValue;
        var maxTemperature = float.MinValue;
        var minHumidity = 1f;
        var maxHumidity = 0f;

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell.ElevationMeters >= 0f) land++;
            if (cell.ElevationMeters > 1850f && cell.Slope > 280f) mountains++;
            if (cell.IsRiver) rivers++;
            if (cell.IsLake) lakes++;
            minElevation = Math.Min(minElevation, cell.ElevationMeters);
            maxElevation = Math.Max(maxElevation, cell.ElevationMeters);
            maxWater = Math.Max(maxWater, cell.WaterDepthMeters);
            minTemperature = Math.Min(minTemperature, cell.TemperatureCelsius);
            maxTemperature = Math.Max(maxTemperature, cell.TemperatureCelsius);
            minHumidity = Math.Min(minHumidity, cell.Humidity);
            maxHumidity = Math.Max(maxHumidity, cell.Humidity);
        }

        var components = ComponentSizes(topology, cells);
        components.Sort((a, b) => b.CompareTo(a));
        var largest = components.Count == 0 ? 0 : components[0];
        var islandLimit = Math.Max(4, cells.Length / 250);
        var islands = 0;
        foreach (var size in components)
            if (size < islandLimit) islands++;

        return new WorldGenerationSummary(
            cells.Length,
            land / (float)Math.Max(1, cells.Length),
            components.Count,
            largest,
            islands,
            mountains,
            rivers,
            lakes,
            minElevation,
            maxElevation,
            maxWater,
            minTemperature,
            maxTemperature,
            minHumidity,
            maxHumidity);
    }

    private static List<int> ComponentSizes(WorldTopology topology, GeneratedWorldCell[] cells)
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
                    var ni = neighbors[n];
                    if (visited[ni] || cells[ni].ElevationMeters < 0f)
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }
            result.Add(size);
        }
        return result;
    }

    private static int[] CreateIndexOrder(int count)
    {
        var result = new int[count];
        for (var i = 0; i < count; i++) result[i] = i;
        return result;
    }

    private static float Fbm(CoreVector3 p, ulong seed, int octaves)
    {
        var amplitude = 0.5f;
        var frequency = 1f;
        var value = 0f;
        var normalization = 0f;
        for (var octave = 0; octave < octaves; octave++)
        {
            value += ValueNoise3D(p * frequency, SeedMixer.Combine(seed, (ulong)(octave + 1))) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.03f;
        }
        return normalization <= 0f ? 0f : value / normalization;
    }

    private static float ValueNoise3D(CoreVector3 p, ulong seed)
    {
        var x0 = (int)MathF.Floor(p.X);
        var y0 = (int)MathF.Floor(p.Y);
        var z0 = (int)MathF.Floor(p.Z);
        var tx = Smooth(p.X - x0);
        var ty = Smooth(p.Y - y0);
        var tz = Smooth(p.Z - z0);

        var c000 = HashSigned(x0, y0, z0, seed);
        var c100 = HashSigned(x0 + 1, y0, z0, seed);
        var c010 = HashSigned(x0, y0 + 1, z0, seed);
        var c110 = HashSigned(x0 + 1, y0 + 1, z0, seed);
        var c001 = HashSigned(x0, y0, z0 + 1, seed);
        var c101 = HashSigned(x0 + 1, y0, z0 + 1, seed);
        var c011 = HashSigned(x0, y0 + 1, z0 + 1, seed);
        var c111 = HashSigned(x0 + 1, y0 + 1, z0 + 1, seed);
        var x00 = Lerp(c000, c100, tx);
        var x10 = Lerp(c010, c110, tx);
        var x01 = Lerp(c001, c101, tx);
        var x11 = Lerp(c011, c111, tx);
        return Lerp(Lerp(x00, x10, ty), Lerp(x01, x11, ty), tz);
    }

    private static float HashSigned(int x, int y, int z, ulong seed)
    {
        var h = SeedMixer.Combine(seed, unchecked((ulong)(uint)x));
        h = SeedMixer.Combine(h, unchecked((ulong)(uint)y));
        h = SeedMixer.Combine(h, unchecked((ulong)(uint)z));
        return ((h >> 40) * (1f / 16_777_215f)) * 2f - 1f;
    }

    private static float Hash01(int a, int b, ulong seed)
    {
        var h = SeedMixer.Combine(seed, unchecked((ulong)(uint)a));
        h = SeedMixer.Combine(h, unchecked((ulong)(uint)b));
        return (h >> 40) * (1f / 16_777_215f);
    }

    private static float Smooth(float value) => value * value * (3f - 2f * value);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private readonly record struct Plate(CoreVector3 Center, bool Continental, float Uplift, float Volcanism);
}
