using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Evolit.Core;

public enum WorldLandAmount : byte { Low, Normal, High }
public enum WorldClimate : byte { Cold, Temperate, Warm }
public enum GeologicalActivity : byte { Calm, Normal, Active }

public static class WorldGenerationScale
{
    public const int SmallRadius = 56;
    public const int MediumRadius = 76;
    public const int LargeRadius = 98;

    public const int LegacySmallRadius = 28;
    public const int LegacyMediumRadius = 38;
    public const int LegacyLargeRadius = 49;
}


public readonly record struct WorldGenerationSettings(
    string Seed,
    int Radius,
    WorldLandAmount LandAmount = WorldLandAmount.Normal,
    WorldClimate Climate = WorldClimate.Temperate,
    GeologicalActivity Geology = GeologicalActivity.Normal);

public readonly record struct GeneratedWorldCell(
    CellId Id,
    float ElevationMeters,
    float WaterDepthMeters,
    float TemperatureCelsius,
    float Humidity,
    float PressureKPa,
    float MineralPotential,
    float NutrientPotential,
    float GeothermalPotential,
    SubstrateKind Substrate,
    int DrainageTarget,
    float FlowAccumulation,
    float Slope,
    bool IsRiver,
    bool IsLake);

public readonly record struct WorldGenerationMetrics(
    double TopologyMs,
    double MacroElevationMs,
    double CoastBathymetryMs,
    double GeologyMs,
    double HydrologyMs,
    double ClimateMs,
    double ResourcesMs,
    double EnvironmentBuildMs,
    double TotalMs);

public sealed class GeneratedWorld
{
    public required WorldGenerationSettings Settings { get; init; }
    public required WorldTopology Topology { get; init; }
    public required GeneratedWorldCell[] Cells { get; init; }
    public required EnvironmentStore Environment { get; init; }
    public required WorldGenerationSummary Summary { get; init; }
    public required WorldGenerationMetrics Metrics { get; init; }
}

public readonly record struct WorldGenerationSummary(
    int Cells, float LandRatio, int LandComponents, int LargestContinentCells,
    int IslandCount, int MountainCells, int RiverCells, int LakeCells,
    float MinElevationMeters, float MaxElevationMeters, float MaxWaterDepthMeters,
    float MinTemperatureCelsius, float MaxTemperatureCelsius,
    float MinHumidity, float MaxHumidity);

public static class ProceduralWorldGenerator
{
    private static readonly (int Q, int R)[] Directions =
    [
        (1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1)
    ];

    public static GeneratedWorld Generate(WorldGenerationSettings settings)
    {
        if (settings.Radius < 4) throw new ArgumentOutOfRangeException(nameof(settings.Radius));
        var totalStart = Stopwatch.GetTimestamp();

        var stageStart = Stopwatch.GetTimestamp();
        var ids = BuildCells(settings.Radius);
        var topology = BuildTopology(ids);
        var topologyMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
        var index = new Dictionary<CellId,int>(ids.Length);
        for (var i=0;i<ids.Length;i++) index.Add(ids[i],i);

        var elevation = new float[ids.Length];
        var water = new float[ids.Length];
        var temperature = new float[ids.Length];
        var humidity = new float[ids.Length];
        var minerals = new float[ids.Length];
        var nutrients = new float[ids.Length];
        var geothermal = new float[ids.Length];
        var substrate = new SubstrateKind[ids.Length];
        var drainage = new int[ids.Length];
        var accumulation = new float[ids.Length];
        var slope = new float[ids.Length];
        var rivers = new bool[ids.Length];
        var lakes = new bool[ids.Length];
        Array.Fill(drainage,-1);

        var seed = SeedMixer.FromString(settings.Seed ?? string.Empty);

        stageStart = Stopwatch.GetTimestamp();
        GenerateElevation(settings, ids, seed, elevation);
        var macroElevationMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        var seaLevel = ChooseSeaLevel(settings, elevation);
        ApplyBathymetry(elevation, seaLevel, water);
        RefineCoasts(topology, ids, elevation, water);
        ApplyMountainBelts(settings, ids, seed, elevation);
        for (var i = 0; i < elevation.Length; i++)
            elevation[i] = Math.Clamp(elevation[i], -6000f, 6500f);
        ApplyBathymetry(elevation, 0f, water);
        var coastBathymetryMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        GenerateGeology(settings, ids, seed, elevation, minerals, geothermal, substrate);
        var geologyMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        GenerateHydrology(topology, ids, index, elevation, water, drainage, accumulation, slope);
        FillLakeBasins(topology, ids, elevation, water, drainage, accumulation);
        ClassifyHydrology(elevation, water, drainage, accumulation, rivers, lakes);
        FinalizeSubstrate(topology, ids, elevation, water, accumulation, geothermal, substrate);
        var hydrologyMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        GenerateClimate(settings, topology, ids, seed, elevation, water, accumulation, temperature, humidity);
        var climateMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        GenerateResources(accumulation, minerals, humidity, substrate, nutrients);
        var resourcesMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        var environment = new EnvironmentStore(topology);
        for (var i=0;i<ids.Length;i++)
        {
            var pressure = 101.325f * MathF.Exp(-Math.Max(-500f,elevation[i]) / 8434f);
            environment.SetInitial(ids[i], new EnvironmentCellState(
                elevation[i], water[i], temperature[i], humidity[i], Math.Clamp(pressure,20f,115f),
                water[i] > 100f ? 0.55f : 1f, minerals[i], nutrients[i], 0f, 0f, geothermal[i], substrate[i]));
        }

        var environmentBuildMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        var cells = new GeneratedWorldCell[ids.Length];
        for (var i=0;i<ids.Length;i++)
        {
            cells[i] = new GeneratedWorldCell(ids[i], elevation[i], water[i], temperature[i], humidity[i],
                Math.Clamp(101.325f*MathF.Exp(-Math.Max(-500f,elevation[i])/8434f),20f,115f),
                minerals[i], nutrients[i], geothermal[i], substrate[i], drainage[i], accumulation[i], slope[i], rivers[i], lakes[i]);
        }

        var summary = Summarize(topology, cells);
        var generated = new GeneratedWorld {
            Settings = settings,
            Topology = topology,
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
                Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds)
        };
        Validate(generated);
        return generated;
    }

    private static void Validate(GeneratedWorld world)
    {
        var summary = world.Summary;
        if (summary.Cells <= 0)
            throw new InvalidOperationException("World generation produced no cells.");
        if (summary.LandRatio is < 0.20f or > 0.75f)
            throw new InvalidOperationException($"Generated land ratio {summary.LandRatio:0.###} is outside safe bounds.");
        if (summary.LargestContinentCells <= 0 || summary.LargestContinentCells >= summary.Cells)
            throw new InvalidOperationException("Generated world must contain both coherent land and water.");
        var landCells = Math.Max(1, (int)MathF.Round(summary.Cells * summary.LandRatio));
        if (summary.LargestContinentCells < landCells * 0.16f)
            throw new InvalidOperationException("Generated land is too fragmented to contain a readable major landmass.");
        if (summary.IslandCount > Math.Max(16, summary.Cells / 600))
            throw new InvalidOperationException("Generated world contains an excessive number of tiny land components.");

        var boundaryCells = 0;
        var boundaryLand = 0;
        for (var i = 0; i < world.Cells.Length; i++)
        {
            if (world.Topology.GetNeighbors(world.Cells[i].Id).Length >= 6)
                continue;
            boundaryCells++;
            if (world.Cells[i].ElevationMeters >= 0f)
                boundaryLand++;
        }
        if (boundaryCells > 0 && boundaryLand > boundaryCells * 0.10f)
            throw new InvalidOperationException("Generated land reaches too much of the finite world boundary.");

        for (var i = 0; i < world.Cells.Length; i++)
        {
            var cell = world.Cells[i];
            if (!float.IsFinite(cell.ElevationMeters) ||
                !float.IsFinite(cell.WaterDepthMeters) || cell.WaterDepthMeters < 0f ||
                !float.IsFinite(cell.TemperatureCelsius) ||
                !float.IsFinite(cell.Humidity) || cell.Humidity is < 0f or > 1f ||
                !float.IsFinite(cell.PressureKPa) || cell.PressureKPa <= 0f ||
                cell.MineralPotential is < 0f or > 1f ||
                cell.NutrientPotential is < 0f or > 1f ||
                cell.GeothermalPotential is < 0f or > 1f ||
                cell.Substrate == SubstrateKind.Unknown)
                throw new InvalidOperationException($"Generated cell {cell.Id} violates physical bounds.");

            if (cell.DrainageTarget >= 0)
            {
                if (cell.DrainageTarget >= world.Cells.Length)
                    throw new InvalidOperationException($"Generated cell {cell.Id} has an invalid drainage target.");
                if (world.Cells[cell.DrainageTarget].ElevationMeters > cell.ElevationMeters + 0.001f)
                    throw new InvalidOperationException($"Generated drainage flows uphill from {cell.Id}.");
            }

            if (cell.IsLake)
            {
                if (cell.ElevationMeters < 0f)
                    throw new InvalidOperationException($"Generated lake {cell.Id} is below ocean level.");
                if (cell.DrainageTarget >= 0 && !world.Cells[cell.DrainageTarget].IsLake)
                    throw new InvalidOperationException($"Generated lake {cell.Id} does not drain within its basin.");
            }
        }
    }

    private static CellId[] BuildCells(int radius)
    {
        var list=new List<CellId>(1+3*radius*(radius+1));
        for(var q=-radius;q<=radius;q++)
        {
            var rMin=Math.Max(-radius,-q-radius);
            var rMax=Math.Min(radius,-q+radius);
            for(var r=rMin;r<=rMax;r++) list.Add(CellId.FromAxial(q,r));
        }
        return list.ToArray();
    }

    private static WorldTopology BuildTopology(CellId[] ids)
    {
        var set=new HashSet<CellId>(ids);
        var b=new WorldTopologyBuilder();
        foreach(var id in ids)
        {
            var n=new List<CellId>(6);
            foreach(var d in Directions)
            {
                var next=CellId.FromAxial(id.Q+d.Q,id.R+d.R);
                if(set.Contains(next)) n.Add(next);
            }
            b.Add(id,n);
        }
        return b.Build();
    }

    private static void GenerateElevation(WorldGenerationSettings s, CellId[] ids, ulong seed, float[] e)
    {
        var landBias = s.LandAmount switch
        {
            WorldLandAmount.Low => -0.10f,
            WorldLandAmount.High => 0.10f,
            _ => 0f
        };

        for (var i = 0; i < ids.Length; i++)
        {
            var (x, y) = NormalizedWorldPosition(ids[i], s.Radius);
            var radial = MathF.Sqrt(x * x + y * y);

            // Continental structure is intentionally much stronger than local noise.
            // This avoids the old "sponge / cheese" topology where every octave could
            // punch independent holes through the same landmass.
            var warpX = (Fbm(x * 1.8f + 4f, y * 1.8f - 3f, SeedMixer.Combine(seed, 8), 3) - 0.5f) * 0.30f;
            var warpY = (Fbm(x * 1.8f - 6f, y * 1.8f + 5f, SeedMixer.Combine(seed, 9), 3) - 0.5f) * 0.30f;
            var continental = ContinentalField(x + warpX, y + warpY, seed);
            var broad = Fbm(x * 1.5f + 9f, y * 1.5f - 7f, SeedMixer.Combine(seed, 12), 3) - 0.5f;
            var regional = Fbm(x * 4.0f - 5f, y * 4.0f + 3f, SeedMixer.Combine(seed, 13), 3) - 0.5f;
            var local = Fbm(x * 9.0f + 13f, y * 9.0f - 11f, SeedMixer.Combine(seed, 14), 2) - 0.5f;

            // Keep the finite hex boundary under deep ocean so the playable land does
            // not visually trace the outer hex shape.
            var edge = -MathF.Pow(Math.Clamp((radial - 0.72f) / 0.28f, 0f, 1f), 2.25f) * 1.35f;

            var normalized =
                continental * 0.92f +
                broad * 0.45f +
                regional * 0.38f +
                local * 0.12f +
                edge +
                landBias;

            e[i] = normalized >= 0f
                ? normalized * 3150f
                : normalized * 4300f;
        }
    }

    private static float ChooseSeaLevel(WorldGenerationSettings s,float[] elevation)
    {
        var copy=(float[])elevation.Clone(); Array.Sort(copy);
        var targetLand=s.LandAmount switch { WorldLandAmount.Low=>0.28f, WorldLandAmount.High=>0.52f, _=>0.40f };
        var idx=Math.Clamp((int)((1f-targetLand)*(copy.Length-1)),0,copy.Length-1);
        return copy[idx];
    }

    private static void ApplyBathymetry(float[] elevation,float sea,float[] water)
    {
        for(var i=0;i<elevation.Length;i++)
        {
            elevation[i]-=sea;
            water[i]=0f;
            if(elevation[i]<0)
            {
                var depth=-elevation[i];
                water[i]=depth<180f ? depth*0.65f+8f : depth*1.25f;
            }
        }
    }

    private static void RefineCoasts(WorldTopology topology, CellId[] ids, float[] e, float[] water)
    {
        var land = new bool[ids.Length];
        var next = new bool[ids.Length];
        for (var i = 0; i < ids.Length; i++)
            land[i] = e[i] >= 0f;

        // Morphological cleanup removes isolated holes and one-cell noise without
        // flattening medium/large bays, peninsulas or islands.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                var neighbors = topology.GetNeighbors(ids[i]);
                var landNeighbors = 0;
                for (var n = 0; n < neighbors.Length; n++)
                    if (topology.TryGetIndex(neighbors[n], out var ni) && land[ni])
                        landNeighbors++;

                var boundary = neighbors.Length < 6;
                next[i] = land[i];
                if (land[i] && landNeighbors <= 1)
                    next[i] = false;
                else if (!land[i] && !boundary && landNeighbors >= (pass == 0 ? 5 : 4))
                    next[i] = true;
            }
            (land, next) = (next, land);
        }

        CleanupComponents(topology, ids, land);

        // Smooth numeric elevation while preserving the cleaned land/water mask.
        var smoothed = new float[e.Length];
        for (var pass = 0; pass < 1; pass++)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                var neighbors = topology.GetNeighbors(ids[i]);
                var sum = e[i];
                var count = 1;
                for (var n = 0; n < neighbors.Length; n++)
                {
                    if (!topology.TryGetIndex(neighbors[n], out var ni))
                        continue;
                    sum += e[ni];
                    count++;
                }

                var value = e[i] * 0.82f + (sum / count) * 0.18f;
                smoothed[i] = land[i]
                    ? Math.Max(24f, value)
                    : Math.Min(-24f, value);
            }
            Array.Copy(smoothed, e, e.Length);
        }

        ApplyBathymetry(e, 0f, water);
    }

    private static void GenerateGeology(
        WorldGenerationSettings s,
        CellId[] ids,
        ulong seed,
        float[] e,
        float[] mineral,
        float[] geo,
        SubstrateKind[] sub)
    {
        var activity = s.Geology switch
        {
            GeologicalActivity.Calm => 0.65f,
            GeologicalActivity.Active => 1.35f,
            _ => 1f
        };

        for (var i = 0; i < ids.Length; i++)
        {
            var (x, y) = NormalizedWorldPosition(ids[i], s.Radius);
            var province = Fbm(x * 2.8f + 2f, y * 2.8f - 3f, SeedMixer.Combine(seed, 21), 3);
            var volcanic = Fbm(x * 5.0f + 17f, y * 5.0f - 31f, SeedMixer.Combine(seed, 22), 3);

            // Volcanic/basalt zones are intentionally sparse. The previous low
            // threshold produced large arbitrary grey blobs unrelated to relief.
            geo[i] = Math.Clamp(Math.Max(0f, volcanic - 0.70f) * 3.2f * activity, 0f, 1f);
            mineral[i] = Math.Clamp(
                0.18f +
                province * 0.48f +
                geo[i] * 0.26f +
                Math.Clamp(e[i] / 3500f, 0f, 1f) * 0.20f,
                0f,
                1f);

            if (geo[i] > 0.78f)
                sub[i] = SubstrateKind.Basalt;
            else if (e[i] > 1800f)
                sub[i] = SubstrateKind.BareRock;
            else
                sub[i] = SubstrateKind.MineralRegolith;
        }
    }

    private static void GenerateHydrology(WorldTopology topology,CellId[] ids,Dictionary<CellId,int> index,float[] e,float[] water,int[] drainage,float[] acc,float[] slope)
    {
        var order=new int[ids.Length];
        for(var i=0;i<ids.Length;i++){order[i]=i;acc[i]=water[i]>0?0f:1f;}
        Array.Sort(order,(a,b)=>e[b].CompareTo(e[a]));
        foreach(var i in order)
        {
            if(water[i]>0)continue;
            var ns=topology.GetNeighbors(ids[i]); var best=-1; var bestE=e[i];
            for(var n=0;n<ns.Length;n++)
            {
                var ni=index[ns[n]];
                if(e[ni]<bestE){bestE=e[ni];best=ni;}
            }
            drainage[i]=best;
            if(best>=0)
            {
                slope[i]=Math.Max(0f,e[i]-e[best]);
                acc[best]+=acc[i];
            }
        }
    }

    private static void FillLakeBasins(
        WorldTopology topology,
        CellId[] ids,
        float[] elevation,
        float[] water,
        int[] drainage,
        float[] accumulation)
    {
        var threshold = Math.Max(14f, elevation.Length * 0.0028f);

        for (var i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] < 0f || drainage[i] >= 0 || accumulation[i] < threshold)
                continue;

            var depth = Math.Clamp(5f + accumulation[i] * 0.075f, 5f, 48f);
            water[i] = Math.Max(water[i], depth);

            // Grow a basin beyond a single sink hex only into cells that actually
            // drain into that sink and sit close to its floor elevation.
            var neighbors = topology.GetNeighbors(ids[i]);
            for (var n = 0; n < neighbors.Length; n++)
            {
                if (!topology.TryGetIndex(neighbors[n], out var ni) ||
                    elevation[ni] < 0f ||
                    drainage[ni] != i ||
                    elevation[ni] > elevation[i] + 95f)
                {
                    continue;
                }

                var localDepth = Math.Clamp(
                    depth - Math.Max(0f, elevation[ni] - elevation[i]) * 0.18f,
                    1.5f,
                    depth);
                water[ni] = Math.Max(water[ni], localDepth);
            }
        }
    }

    private static void ClassifyHydrology(
        float[] elevation,
        float[] water,
        int[] drainage,
        float[] accumulation,
        bool[] rivers,
        bool[] lakes)
    {
        var riverThreshold = Math.Max(10f, elevation.Length * 0.0018f);
        for (var i = 0; i < elevation.Length; i++)
        {
            lakes[i] = water[i] > 0.1f && elevation[i] >= 0f;
            rivers[i] = !lakes[i] && elevation[i] >= 0f &&
                accumulation[i] >= riverThreshold && drainage[i] >= 0;
            if (rivers[i])
                water[i] = Math.Clamp(0.45f + accumulation[i] * 0.018f, 0.45f, 3.5f);
        }
    }

    private static void FinalizeSubstrate(
        WorldTopology topology,
        CellId[] ids,
        float[] elevation,
        float[] water,
        float[] accumulation,
        float[] geothermal,
        SubstrateKind[] substrate)
    {
        var sedimentThreshold = Math.Max(16f, elevation.Length * 0.0035f);
        for (var i = 0; i < elevation.Length; i++)
        {
            if (water[i] > 0.1f || accumulation[i] > sedimentThreshold)
            {
                substrate[i] = SubstrateKind.Sediment;
                continue;
            }

            var coastal = false;
            if (elevation[i] >= 0f && elevation[i] < 140f)
            {
                var neighbors = topology.GetNeighbors(ids[i]);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    if (topology.TryGetIndex(neighbors[n], out var ni) && elevation[ni] < 0f)
                    {
                        coastal = true;
                        break;
                    }
                }
            }

            if (geothermal[i] > 0.72f)
                substrate[i] = SubstrateKind.Basalt;
            else if (elevation[i] > 1800f)
                substrate[i] = SubstrateKind.BareRock;
            else if (coastal)
                substrate[i] = SubstrateKind.Sand;
            else
                substrate[i] = SubstrateKind.MineralRegolith;
        }
    }

    private static void GenerateClimate(
        WorldGenerationSettings s,
        WorldTopology topology,
        CellId[] ids,
        ulong seed,
        float[] e,
        float[] water,
        float[] acc,
        float[] temp,
        float[] hum)
    {
        var climateOffset = s.Climate switch
        {
            WorldClimate.Cold => -9f,
            WorldClimate.Warm => 7f,
            _ => 0f
        };

        var distanceToWater = DistanceToWater(topology, ids, water);
        var riverThreshold = Math.Max(10f, ids.Length * 0.0018f);

        for (var i = 0; i < ids.Length; i++)
        {
            var (_, y) = NormalizedWorldPosition(ids[i], s.Radius);
            var latitude = Math.Clamp(Math.Abs(y), 0f, 1f);
            var regionalNoise = (Fbm(
                ids[i].Q / (float)s.Radius * 2.2f + 5f,
                ids[i].R / (float)s.Radius * 2.2f - 4f,
                SeedMixer.Combine(seed, 31),
                3) - 0.5f) * 5f;

            temp[i] = Math.Clamp(
                30f + climateOffset - latitude * 29f - Math.Max(0f, e[i]) * 0.0062f + regionalNoise,
                -55f,
                48f);

            if (water[i] > 0.1f)
            {
                hum[i] = 1f;
                continue;
            }

            var distance = distanceToWater[i] == int.MaxValue ? s.Radius : distanceToWater[i];
            var maritime = MathF.Exp(-distance / Math.Max(7f, s.Radius * 0.16f));
            var regionalMoisture = Fbm(
                ids[i].Q / (float)s.Radius * 2.8f - 8f,
                ids[i].R / (float)s.Radius * 2.8f + 6f,
                SeedMixer.Combine(seed, 32),
                3);

            var riverMoisture = acc[i] >= riverThreshold
                ? Math.Clamp(0.08f + MathF.Log10(1f + acc[i]) * 0.04f, 0.08f, 0.18f)
                : 0f;

            // Simple deterministic rain-shadow foundation: prevailing moisture moves
            // roughly west -> east and loses strength immediately behind high terrain.
            var rainShadow = 0f;
            for (var step = 1; step <= 3; step++)
            {
                var upwind = CellId.FromAxial(ids[i].Q - step, ids[i].R);
                if (topology.TryGetIndex(upwind, out var ui) &&
                    e[ui] > 1050f &&
                    e[ui] > e[i] + 350f)
                {
                    rainShadow = Math.Max(rainShadow, 0.05f * (4 - step));
                }
            }

            hum[i] = Math.Clamp(
                0.16f +
                maritime * 0.48f +
                regionalMoisture * 0.20f +
                riverMoisture -
                rainShadow,
                0f,
                1f);
        }
    }

    private static void GenerateResources(float[] acc,float[] minerals,float[] humidity,SubstrateKind[] sub,float[] nutrients)
    {
        for(var i=0;i<nutrients.Length;i++)
        {
            var sediment=sub[i]==SubstrateKind.Sediment?0.18f:0f;
            nutrients[i]=Math.Clamp(minerals[i]*0.48f+humidity[i]*0.22f+Math.Min(0.15f,acc[i]*0.002f)+sediment,0f,1f);
        }
    }

    private static WorldGenerationSummary Summarize(WorldTopology topology,GeneratedWorldCell[] cells)
    {
        var land=0;var mountains=0;var rivers=0;var lakes=0;var minE=float.MaxValue;var maxE=float.MinValue;var maxW=0f;var minT=float.MaxValue;var maxT=float.MinValue;var minH=float.MaxValue;var maxH=float.MinValue;
        var visited=new bool[cells.Length];var components=0;var largest=0;var islands=0;
        for(var i=0;i<cells.Length;i++){var c=cells[i];if(c.ElevationMeters>=0f)land++;if(c.ElevationMeters>1700f)mountains++;if(c.IsRiver)rivers++;if(c.IsLake)lakes++;minE=Math.Min(minE,c.ElevationMeters);maxE=Math.Max(maxE,c.ElevationMeters);maxW=Math.Max(maxW,c.WaterDepthMeters);minT=Math.Min(minT,c.TemperatureCelsius);maxT=Math.Max(maxT,c.TemperatureCelsius);minH=Math.Min(minH,c.Humidity);maxH=Math.Max(maxH,c.Humidity);}
        for(var i=0;i<cells.Length;i++)
        {
            if(visited[i]||cells[i].ElevationMeters<0f)continue;
            components++;var count=0;var q=new Queue<int>();q.Enqueue(i);visited[i]=true;
            while(q.Count>0){var at=q.Dequeue();count++;var ns=topology.GetNeighbors(cells[at].Id);for(var n=0;n<ns.Length;n++)if(topology.TryGetIndex(ns[n],out var ni)&&!visited[ni]&&cells[ni].ElevationMeters>=0f){visited[ni]=true;q.Enqueue(ni);}}
            largest=Math.Max(largest,count);if(count<Math.Max(4,cells.Length/250))islands++;
        }
        return new WorldGenerationSummary(cells.Length,land/(float)cells.Length,components,largest,islands,mountains,rivers,lakes,minE,maxE,maxW,minT,maxT,minH,maxH);
    }

    private static void CleanupComponents(WorldTopology topology, CellId[] ids, bool[] land)
    {
        var visited = new bool[ids.Length];
        var component = new List<int>();
        var queue = new Queue<int>();

        // Remove only tiny isolated land noise. Real islands survive.
        var minLandComponent = Math.Max(4, ids.Length / 2200);
        for (var i = 0; i < ids.Length; i++)
        {
            if (visited[i] || !land[i])
                continue;

            component.Clear();
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                component.Add(at);
                var neighbors = topology.GetNeighbors(ids[at]);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    if (!topology.TryGetIndex(neighbors[n], out var ni) || visited[ni] || !land[ni])
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            if (component.Count < minLandComponent)
                foreach (var index in component)
                    land[index] = false;
        }

        Array.Fill(visited, false);
        var maxTinyInlandSea = Math.Max(14, ids.Length / 320);
        for (var i = 0; i < ids.Length; i++)
        {
            if (visited[i] || land[i])
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
                var neighbors = topology.GetNeighbors(ids[at]);
                if (neighbors.Length < 6)
                    touchesBoundary = true;

                for (var n = 0; n < neighbors.Length; n++)
                {
                    if (!topology.TryGetIndex(neighbors[n], out var ni) || visited[ni] || land[ni])
                        continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            if (!touchesBoundary && component.Count <= maxTinyInlandSea)
                foreach (var index in component)
                    land[index] = true;
        }
    }

    private static void ApplyMountainBelts(
        WorldGenerationSettings settings,
        CellId[] ids,
        ulong seed,
        float[] elevation)
    {
        var beltCount = settings.Geology switch
        {
            GeologicalActivity.Calm => 2,
            GeologicalActivity.Active => 4,
            _ => 3
        };
        var activity = settings.Geology switch
        {
            GeologicalActivity.Calm => 0.72f,
            GeologicalActivity.Active => 1.28f,
            _ => 1f
        };

        for (var i = 0; i < ids.Length; i++)
        {
            if (elevation[i] <= 0f)
                continue;

            var (x, y) = NormalizedWorldPosition(ids[i], settings.Radius);
            var uplift = 0f;

            for (var belt = 0; belt < beltCount; belt++)
            {
                var beltSeed = SeedMixer.Combine(seed, (ulong)(100 + belt));
                var cx = SignedHash(beltSeed, 1) * 0.45f;
                var cy = SignedHash(beltSeed, 2) * 0.42f;
                var angle = Hash01(belt + 17, 29, beltSeed) * MathF.PI;
                var halfLength = 0.28f + Hash01(belt + 31, 47, beltSeed) * 0.32f;
                var width = 0.045f + Hash01(belt + 59, 71, beltSeed) * 0.045f;

                var dx = MathF.Cos(angle) * halfLength;
                var dy = MathF.Sin(angle) * halfLength;
                var distance = DistanceToSegment(x, y, cx - dx, cy - dy, cx + dx, cy + dy);
                var core = MathF.Exp(-MathF.Pow(distance / width, 2f));
                var halo = MathF.Exp(-MathF.Pow(distance / (width * 2.5f), 2f)) * 0.38f;
                uplift = Math.Max(uplift, core + halo);
            }

            if (uplift <= 0.02f)
                continue;

            var roughness = 0.78f + Fbm(x * 9f, y * 9f, SeedMixer.Combine(seed, 155), 2) * 0.40f;
            elevation[i] += uplift * roughness * 1750f * activity;
        }
    }

    private static int[] DistanceToWater(WorldTopology topology, CellId[] ids, float[] water)
    {
        var distance = new int[ids.Length];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();

        for (var i = 0; i < ids.Length; i++)
        {
            if (water[i] <= 0.1f)
                continue;
            distance[i] = 0;
            queue.Enqueue(i);
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            var nextDistance = distance[at] + 1;
            var neighbors = topology.GetNeighbors(ids[at]);
            for (var n = 0; n < neighbors.Length; n++)
            {
                if (!topology.TryGetIndex(neighbors[n], out var ni) || distance[ni] <= nextDistance)
                    continue;
                distance[ni] = nextDistance;
                queue.Enqueue(ni);
            }
        }

        return distance;
    }

    private static float ContinentalField(float x, float y, ulong seed)
    {
        // Distribute 3-4 macro anchors around the inner world instead of placing
        // every continent independently. This prevents most seeds from collapsing
        // into a single overlapping "blob", while domain warping keeps the result
        // from looking like perfect ellipses.
        var count = 3 + (int)(SeedMixer.Combine(seed, 701) % 2UL);
        var layoutSeed = SeedMixer.Combine(seed, 702);
        var baseAngle = Hash01(3, 5, layoutSeed) * MathF.PI * 2f;
        var ringRadius = 0.38f + Hash01(7, 11, SeedMixer.Combine(seed, 703)) * 0.12f;
        var strongest = -2f;

        for (var i = 0; i < count; i++)
        {
            var continentSeed = SeedMixer.Combine(seed, (ulong)(720 + i));
            var positionAngle =
                baseAngle +
                i * (MathF.PI * 2f / count) +
                SignedHash(continentSeed, 3) * 0.22f;
            var centerRadius = ringRadius + SignedHash(continentSeed, 4) * 0.05f;
            var cx = MathF.Cos(positionAngle) * centerRadius + SignedHash(continentSeed, 1) * 0.08f;
            var cy = MathF.Sin(positionAngle) * centerRadius + SignedHash(continentSeed, 2) * 0.08f;

            var angle = Hash01(i + 3, 13, continentSeed) * MathF.PI;
            var axisA = 0.28f + Hash01(i + 7, 19, continentSeed) * 0.16f;
            var axisB = 0.18f + Hash01(i + 11, 23, continentSeed) * 0.12f;

            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var px = x - cx;
            var py = y - cy;
            var rx = px * cos + py * sin;
            var ry = -px * sin + py * cos;
            var elliptical = MathF.Sqrt(
                (rx * rx) / (axisA * axisA) +
                (ry * ry) / (axisB * axisB));

            strongest = Math.Max(strongest, 0.52f - elliptical);
        }

        return strongest;
    }

    private static (float X, float Y) NormalizedWorldPosition(CellId id, int radius)
    {
        var x = (id.Q + id.R * 0.5f) / radius;
        var y = id.R * 0.8660254f / radius;
        return (x, y);
    }

    private static float SignedHash(ulong seed, int channel)
    {
        var value = Hash01(channel * 17 + 3, channel * 31 + 7, seed);
        return value * 2f - 1f;
    }

    private static float DistanceToSegment(
        float px, float py,
        float ax, float ay,
        float bx, float by)
    {
        var abx = bx - ax;
        var aby = by - ay;
        var lengthSq = abx * abx + aby * aby;
        if (lengthSq <= 0.000001f)
            return MathF.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));

        var t = Math.Clamp(((px - ax) * abx + (py - ay) * aby) / lengthSq, 0f, 1f);
        var dx = px - (ax + abx * t);
        var dy = py - (ay + aby * t);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float Fbm(float x,float y,ulong seed,int octaves)
    {
        var value=0f;var amp=0.5f;var freq=1f;var total=0f;
        for(var o=0;o<octaves;o++){value+=ValueNoise(x*freq,y*freq,SeedMixer.Combine(seed,(ulong)o+1))*amp;total+=amp;amp*=0.5f;freq*=2f;}
        return value/total;
    }
    private static float ValueNoise(float x,float y,ulong seed)
    {
        var x0=(int)MathF.Floor(x);var y0=(int)MathF.Floor(y);var tx=Smooth(x-x0);var ty=Smooth(y-y0);
        var a=Hash01(x0,y0,seed);var b=Hash01(x0+1,y0,seed);var c=Hash01(x0,y0+1,seed);var d=Hash01(x0+1,y0+1,seed);
        return Lerp(Lerp(a,b,tx),Lerp(c,d,tx),ty);
    }
    private static float Hash01(int x,int y,ulong seed)
    {
        var h=SeedMixer.Combine(seed,unchecked((ulong)(uint)x<<32|(uint)y));
        return (float)(h>>40)*(1f/16777215f);
    }
    private static float Smooth(float v)=>v*v*(3f-2f*v);
    private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
}
