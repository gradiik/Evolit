using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Evolit.Core;

public enum WorldLandAmount : byte { Low, Normal, High }
public enum WorldClimate : byte { Cold, Temperate, Warm }
public enum GeologicalActivity : byte { Calm, Normal, Active }

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
        var coastBathymetryMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        GenerateGeology(settings, ids, seed, elevation, water, minerals, geothermal, substrate);
        var geologyMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        GenerateHydrology(topology, ids, index, elevation, water, drainage, accumulation, slope);
        FillLakeBasins(elevation, water, drainage, accumulation);
        ClassifyHydrology(elevation, water, drainage, accumulation, rivers, lakes);
        FinalizeSubstrate(elevation, water, accumulation, geothermal, substrate);
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
        if (summary.IslandCount > Math.Max(12, summary.Cells / 50))
            throw new InvalidOperationException("Generated world contains an excessive number of tiny land components.");

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

            if (cell.IsLake && (cell.ElevationMeters < 0f || cell.DrainageTarget >= 0))
                throw new InvalidOperationException($"Generated lake {cell.Id} is not tied to a closed land basin.");
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
        var landBias=s.LandAmount switch { WorldLandAmount.Low=>-0.10f, WorldLandAmount.High=>0.10f, _=>0f };
        var activity=s.Geology switch { GeologicalActivity.Calm=>0.72f, GeologicalActivity.Active=>1.25f, _=>1f };
        for(var i=0;i<ids.Length;i++)
        {
            var q=ids[i].Q; var r=ids[i].R;
            var dist=HexDistance(q,r)/(float)s.Radius;
            var continent=Fbm(q*0.032f,r*0.032f,SeedMixer.Combine(seed,11),4);
            var regional=Fbm(q*0.075f,r*0.075f,SeedMixer.Combine(seed,12),3);
            var ridge=1f-MathF.Abs(Fbm(q*0.11f,r*0.11f,SeedMixer.Combine(seed,13),3)*2f-1f);
            var basin=Fbm(q*0.055f+41f,r*0.055f-27f,SeedMixer.Combine(seed,14),2);
            var edge=-MathF.Pow(Math.Clamp((dist-0.70f)/0.30f,0f,1f),1.5f)*0.55f;
            var normalized=(continent-0.50f)*1.15f+(regional-0.5f)*0.34f+edge+landBias;
            normalized += Math.Max(0f,ridge-0.70f)*0.85f*activity;
            normalized -= Math.Max(0f,basin-0.78f)*0.45f;
            e[i]=normalized>=0 ? normalized*3400f : normalized*3900f;
        }
    }

    private static float ChooseSeaLevel(WorldGenerationSettings s,float[] elevation)
    {
        var copy=(float[])elevation.Clone(); Array.Sort(copy);
        var targetLand=s.LandAmount switch { WorldLandAmount.Low=>0.32f, WorldLandAmount.High=>0.56f, _=>0.44f };
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

    private static void RefineCoasts(WorldTopology topology,CellId[] ids,float[] e,float[] water)
    {
        var next=(float[])e.Clone();
        for(var pass=0;pass<2;pass++)
        {
            for(var i=0;i<ids.Length;i++)
            {
                var ns=topology.GetNeighbors(ids[i]); var land=0; var sum=0f;
                for(var n=0;n<ns.Length;n++) if(topology.TryGetIndex(ns[n],out var ni)){if(e[ni]>=0)land++;sum+=e[ni];}
                if(ns.Length==0)continue;
                if((e[i]>=0&&land<=1)||(e[i]<0&&land>=5)) next[i]=e[i]*0.55f+(sum/ns.Length)*0.45f;
                else next[i]=e[i];
            }
            Array.Copy(next,e,e.Length);
        }
        ApplyBathymetry(e,0f,water);
    }

    private static void GenerateGeology(WorldGenerationSettings s,CellId[] ids,ulong seed,float[] e,float[] water,float[] mineral,float[] geo,SubstrateKind[] sub)
    {
        var activity=s.Geology switch { GeologicalActivity.Calm=>0.65f, GeologicalActivity.Active=>1.35f, _=>1f };
        for(var i=0;i<ids.Length;i++)
        {
            var q=ids[i].Q;var r=ids[i].R;
            var province=Fbm(q*0.05f,r*0.05f,SeedMixer.Combine(seed,21),3);
            var volcanic=Fbm(q*0.09f+17f,r*0.09f-31f,SeedMixer.Combine(seed,22),3);
            geo[i]=Math.Clamp(Math.Max(0f,volcanic-0.58f)*2.1f*activity,0f,1f);
            mineral[i]=Math.Clamp(0.18f+province*0.52f+geo[i]*0.30f+Math.Clamp(e[i]/3500f,0f,1f)*0.16f,0f,1f);
            if(geo[i]>0.72f) sub[i]=SubstrateKind.Basalt;
            else if(e[i]>1700f) sub[i]=SubstrateKind.BareRock;
            else if(e[i]<120f) sub[i]=SubstrateKind.Sand;
            else sub[i]=SubstrateKind.MineralRegolith;
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

    private static void FillLakeBasins(float[] elevation,float[] water,int[] drainage,float[] accumulation)
    {
        var threshold=Math.Max(10f,elevation.Length*0.004f);
        for(var i=0;i<elevation.Length;i++)
        {
            if(elevation[i] < 0f || drainage[i] >= 0 || accumulation[i] < threshold) continue;
            water[i]=Math.Clamp(4f+accumulation[i]*0.08f,4f,45f);
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
        var riverThreshold = Math.Max(12f, elevation.Length * 0.006f);
        for (var i = 0; i < elevation.Length; i++)
        {
            lakes[i] = water[i] > 0.1f && elevation[i] >= 0f && drainage[i] < 0;
            rivers[i] = !lakes[i] && elevation[i] >= 0f &&
                accumulation[i] >= riverThreshold && drainage[i] >= 0;
            if (rivers[i])
                water[i] = Math.Clamp(0.45f + accumulation[i] * 0.018f, 0.45f, 3.5f);
        }
    }

    private static void FinalizeSubstrate(
        float[] elevation,
        float[] water,
        float[] accumulation,
        float[] geothermal,
        SubstrateKind[] substrate)
    {
        for (var i = 0; i < elevation.Length; i++)
        {
            if (water[i] > 0.1f || accumulation[i] > Math.Max(18f, elevation.Length * 0.008f))
                substrate[i] = SubstrateKind.Sediment;
            else if (geothermal[i] > 0.72f)
                substrate[i] = SubstrateKind.Basalt;
            else if (elevation[i] > 1700f)
                substrate[i] = SubstrateKind.BareRock;
            else if (elevation[i] < 120f)
                substrate[i] = SubstrateKind.Sand;
            else
                substrate[i] = SubstrateKind.MineralRegolith;
        }
    }

    private static void GenerateClimate(WorldGenerationSettings s,WorldTopology topology,CellId[] ids,ulong seed,float[] e,float[] water,float[] acc,float[] temp,float[] hum)
    {
        var climateOffset=s.Climate switch { WorldClimate.Cold=>-9f, WorldClimate.Warm=>7f, _=>0f };
        for(var i=0;i<ids.Length;i++)
        {
            var latitude=Math.Clamp(Math.Abs(ids[i].R)/(float)s.Radius,0f,1f);
            var noise=(Fbm(ids[i].Q*0.045f,ids[i].R*0.045f,SeedMixer.Combine(seed,31),3)-0.5f)*5f;
            temp[i]=Math.Clamp(30f+climateOffset-latitude*28f-Math.Max(0f,e[i])*0.0062f+noise,-55f,48f);
            hum[i]=water[i]>0?1f:Math.Clamp(0.12f+Fbm(ids[i].Q*0.055f,ids[i].R*0.055f,SeedMixer.Combine(seed,32),3)*0.42f,0f,1f);
        }
        var scratch=new float[hum.Length];
        for(var pass=0;pass<8;pass++)
        {
            for(var i=0;i<ids.Length;i++)
            {
                if(water[i]>0){scratch[i]=1f;continue;}
                var ns=topology.GetNeighbors(ids[i]);var sum=hum[i];var count=1;
                for(var n=0;n<ns.Length;n++) if(topology.TryGetIndex(ns[n],out var ni)){sum+=hum[ni];count++;}
                var riverMoisture=acc[i]>12f?0.12f:0f;
                scratch[i]=Math.Clamp(sum/count*0.86f+riverMoisture,0f,1f);
            }
            (hum,scratch)=(scratch,hum);
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
        for(var i=0;i<cells.Length;i++){var c=cells[i];if(c.WaterDepthMeters<=0.1f)land++;if(c.ElevationMeters>1700f)mountains++;if(c.IsRiver)rivers++;if(c.IsLake)lakes++;minE=Math.Min(minE,c.ElevationMeters);maxE=Math.Max(maxE,c.ElevationMeters);maxW=Math.Max(maxW,c.WaterDepthMeters);minT=Math.Min(minT,c.TemperatureCelsius);maxT=Math.Max(maxT,c.TemperatureCelsius);minH=Math.Min(minH,c.Humidity);maxH=Math.Max(maxH,c.Humidity);}
        for(var i=0;i<cells.Length;i++)
        {
            if(visited[i]||cells[i].WaterDepthMeters>0.1f)continue;
            components++;var count=0;var q=new Queue<int>();q.Enqueue(i);visited[i]=true;
            while(q.Count>0){var at=q.Dequeue();count++;var ns=topology.GetNeighbors(cells[at].Id);for(var n=0;n<ns.Length;n++)if(topology.TryGetIndex(ns[n],out var ni)&&!visited[ni]&&cells[ni].WaterDepthMeters<=0.1f){visited[ni]=true;q.Enqueue(ni);}}
            largest=Math.Max(largest,count);if(count<Math.Max(4,cells.Length/250))islands++;
        }
        return new WorldGenerationSummary(cells.Length,land/(float)cells.Length,components,largest,islands,mountains,rivers,lakes,minE,maxE,maxW,minT,maxT,minH,maxH);
    }

    private static int HexDistance(int q,int r)=>(Math.Abs(q)+Math.Abs(r)+Math.Abs(-q-r))/2;
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
