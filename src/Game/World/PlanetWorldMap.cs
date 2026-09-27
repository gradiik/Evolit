using System;
using System.Collections.Generic;
using Evolit.Core;
using Evolit.Save;

namespace Evolit.Game;

public sealed class PlanetWorldCell
{
    public required CellId Id { get; init; }
    public required int Index { get; init; }
    public required CoreVector3 Direction { get; init; }
    public required HexTerrainType Terrain { get; init; }
    public required HexWaterKind WaterKind { get; init; }
    public float ElevationMeters { get; init; }
    public float WaterDepthMeters { get; init; }
    public float Humidity { get; init; }
    public float TemperatureCelsius { get; init; }
    public float PressureKPa { get; init; }
    public float MovementCost { get; init; }
    public float MovementSpeedMultiplier { get; init; }
    public float VisualVariation { get; init; }
    public float FlowAccumulation { get; init; }
    public float Slope { get; init; }
    public float MineralPotential { get; init; }
    public float NutrientPotential { get; init; }
    public float GeothermalPotential { get; init; }
    public SubstrateKind Substrate { get; init; }
    public int ProvinceId { get; init; }
    public float Continentalness { get; init; }
    public float TectonicUplift { get; init; }
    public int CoastDistance { get; init; }
    public int BasinId { get; init; }
    public int RiverLength { get; init; }
    public float RiverWidth { get; init; }

    public bool IsWater => WaterKind != HexWaterKind.None;
}

public sealed class PlanetWorldMap
{
    private const int BucketResolution = 18;
    private readonly Dictionary<CellId, PlanetWorldCell> _byId;
    private readonly Dictionary<(int X, int Y, int Z), List<int>> _buckets = new();

    public PlanetWorldMap(
        string seed,
        string sizeName,
        int frequency,
        WorldTopology topology,
        PlanetSurfaceGeometry geometry,
        EnvironmentStore environment,
        IReadOnlyList<PlanetWorldCell> cells)
    {
        Seed = seed;
        SizeName = sizeName;
        Frequency = frequency;
        InitialTopology = topology;
        Geometry = geometry;
        InitialEnvironment = environment;
        Cells = cells;
        _byId = new Dictionary<CellId, PlanetWorldCell>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            _byId.Add(cell.Id, cell);
            var bucket = BucketFor(cell.Direction);
            if (!_buckets.TryGetValue(bucket, out var list))
            {
                list = new List<int>();
                _buckets.Add(bucket, list);
            }
            list.Add(i);
        }
    }

    public string Seed { get; }
    public string SizeName { get; }
    public int Frequency { get; }
    public WorldTopology InitialTopology { get; }
    public PlanetSurfaceGeometry Geometry { get; }
    public EnvironmentStore InitialEnvironment { get; }
    public IReadOnlyList<PlanetWorldCell> Cells { get; }

    public bool TryGetCell(CellId id, out PlanetWorldCell cell) => _byId.TryGetValue(id, out cell!);

    public ReadOnlySpan<CoreVector3> GetPolygon(PlanetWorldCell cell) => Geometry.GetPolygon(cell.Index);

    public PlanetWorldCell FindNearestCell(CoreVector3 direction)
    {
        var target = direction.Normalized();
        var bucket = BucketFor(target);
        var bestIndex = -1;
        var bestDot = float.MinValue;

        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
        {
            var key = (bucket.X + dx, bucket.Y + dy, bucket.Z + dz);
            if (!_buckets.TryGetValue(key, out var candidates))
                continue;
            foreach (var index in candidates)
            {
                var dot = CoreVector3.Dot(target, Cells[index].Direction);
                if (dot <= bestDot)
                    continue;
                bestDot = dot;
                bestIndex = index;
            }
        }

        if (bestIndex >= 0)
            return Cells[bestIndex];

        for (var i = 0; i < Cells.Count; i++)
        {
            var dot = CoreVector3.Dot(target, Cells[i].Direction);
            if (dot <= bestDot)
                continue;
            bestDot = dot;
            bestIndex = i;
        }
        return Cells[Math.Max(0, bestIndex)];
    }

    public PlanetWorldCell FindNearestLandCell(CoreVector3 desired)
    {
        var direction = desired.Normalized();
        PlanetWorldCell? best = null;
        var bestDot = float.MinValue;
        foreach (var cell in Cells)
        {
            if (cell.IsWater || cell.Terrain == HexTerrainType.Mountain)
                continue;
            var dot = CoreVector3.Dot(direction, cell.Direction);
            if (dot <= bestDot)
                continue;
            bestDot = dot;
            best = cell;
        }
        return best ?? Cells[0];
    }

    private static (int X, int Y, int Z) BucketFor(CoreVector3 direction)
    {
        static int Quantize(float value) => Math.Clamp(
            (int)MathF.Floor((value + 1f) * 0.5f * BucketResolution),
            0,
            BucketResolution - 1);
        return (Quantize(direction.X), Quantize(direction.Y), Quantize(direction.Z));
    }
}

public static class PlanetWorldMapGenerator
{
    public static PlanetWorldMap Generate(
        string seed,
        string sizeName,
        WorldLandAmount landAmount,
        WorldClimate climate,
        GeologicalActivity geology)
    {
        var frequency = PlanetGenerationScale.FrequencyForSize(sizeName);
        var generated = PlanetWorldGenerator.Generate(new PlanetGenerationSettings(
            seed,
            frequency,
            landAmount,
            climate,
            geology));
        var cells = new PlanetWorldCell[generated.Cells.Length];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = FromGenerated(generated.Cells[i], generated.Geometry.Centers[i], i, seed);
        return new PlanetWorldMap(
            seed,
            sizeName,
            frequency,
            generated.Topology,
            generated.Geometry,
            generated.Environment,
            cells);
    }

    public static PlanetWorldMap Restore(string seed, string sizeName, PlanetWorldMapSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var frequency = state.Frequency > 0
            ? state.Frequency
            : PlanetGenerationScale.FrequencyForSize(sizeName);
        var (topology, geometry) = PlanetTopologyFactory.Build(frequency);
        if (state.Cells.Count != topology.Count)
            throw new InvalidOperationException(
                $"Saved planet contains {state.Cells.Count} cells but topology expects {topology.Count}.");

        var savedById = new Dictionary<long, PlanetWorldCellSaveState>(state.Cells.Count);
        foreach (var saved in state.Cells)
            savedById[saved.Id] = saved;

        var environment = new EnvironmentStore(topology);
        var cells = new PlanetWorldCell[topology.Count];
        for (var i = 0; i < topology.Count; i++)
        {
            var id = topology.GetCellId(i);
            if (!savedById.TryGetValue(id.Value, out var saved))
                throw new InvalidOperationException($"Saved planet is missing cell {id}.");

            var terrain = Enum.IsDefined(typeof(HexTerrainType), saved.Terrain)
                ? (HexTerrainType)saved.Terrain : HexTerrainType.Grassland;
            var water = Enum.IsDefined(typeof(HexWaterKind), saved.WaterKind)
                ? (HexWaterKind)saved.WaterKind : HexWaterKind.None;
            cells[i] = new PlanetWorldCell
            {
                Id = id,
                Index = i,
                Direction = geometry.Centers[i],
                Terrain = terrain,
                WaterKind = water,
                ElevationMeters = saved.ElevationMeters,
                WaterDepthMeters = Math.Max(0f, saved.WaterDepthMeters),
                Humidity = Math.Clamp(saved.Humidity, 0f, 1f),
                TemperatureCelsius = saved.TemperatureCelsius,
                PressureKPa = Math.Max(0f, saved.PressureKPa),
                MovementCost = Math.Max(0.01f, saved.MovementCost),
                MovementSpeedMultiplier = Math.Max(0f, saved.MovementSpeedMultiplier),
                VisualVariation = Math.Clamp(saved.VisualVariation, 0f, 1f),
                FlowAccumulation = Math.Max(0f, saved.FlowAccumulation),
                Slope = Math.Max(0f, saved.Slope),
                MineralPotential = Math.Clamp(saved.MineralPotential, 0f, 1f),
                NutrientPotential = Math.Clamp(saved.NutrientPotential, 0f, 1f),
                GeothermalPotential = Math.Clamp(saved.GeothermalPotential, 0f, 1f),
                Substrate = Enum.IsDefined((SubstrateKind)saved.Substrate) ? (SubstrateKind)saved.Substrate : SubstrateKind.Unknown,
                ProvinceId = saved.ProvinceId,
                Continentalness = saved.Continentalness,
                TectonicUplift = saved.TectonicUplift,
                CoastDistance = saved.CoastDistance,
                BasinId = saved.BasinId,
                RiverLength = saved.RiverLength,
                RiverWidth = saved.RiverWidth
            };
            environment.SetInitial(id, ToEnvironment(cells[i]));
        }

        return new PlanetWorldMap(seed, sizeName, frequency, topology, geometry, environment, cells);
    }

    private static PlanetWorldCell FromGenerated(GeneratedWorldCell source, CoreVector3 direction, int index, string seed)
    {
        var water = source.IsRiver
            ? HexWaterKind.River
            : source.IsLake
                ? HexWaterKind.Lake
                : source.ElevationMeters < 0f
                    ? HexWaterKind.Ocean
                    : HexWaterKind.None;
        var terrain = ClassifyTerrain(source, water);
        var cost = MovementCost(terrain, source.ElevationMeters);
        var mixed = SeedMixer.Combine(SeedMixer.FromString(seed), unchecked((ulong)source.Id.Value));
        return new PlanetWorldCell
        {
            Id = source.Id,
            Index = index,
            Direction = direction,
            Terrain = terrain,
            WaterKind = water,
            ElevationMeters = source.ElevationMeters,
            WaterDepthMeters = source.WaterDepthMeters,
            Humidity = source.Humidity,
            TemperatureCelsius = source.TemperatureCelsius,
            PressureKPa = source.PressureKPa,
            MovementCost = cost,
            MovementSpeedMultiplier = Math.Clamp(1f / Math.Max(0.35f, cost), 0.22f, 1.2f),
            VisualVariation = (float)(mixed >> 40) * (1f / 16_777_215f),
            FlowAccumulation = source.FlowAccumulation,
            Slope = source.Slope,
            MineralPotential = source.MineralPotential,
            NutrientPotential = source.NutrientPotential,
            GeothermalPotential = source.GeothermalPotential,
            Substrate = source.Substrate,
            ProvinceId = source.ProvinceId,
            Continentalness = source.Continentalness,
            TectonicUplift = source.TectonicUplift,
            CoastDistance = source.CoastDistance,
            BasinId = source.BasinId,
            RiverLength = source.RiverLength,
            RiverWidth = source.RiverWidth
        };
    }

    private static EnvironmentCellState ToEnvironment(PlanetWorldCell cell) => new(
        cell.ElevationMeters,
        cell.WaterDepthMeters,
        cell.TemperatureCelsius,
        cell.Humidity,
        cell.PressureKPa,
        cell.IsWater && cell.WaterDepthMeters > 100f ? 0.55f : 1f,
        cell.MineralPotential,
        cell.NutrientPotential,
        0f,
        0f,
        cell.GeothermalPotential,
        cell.Substrate);

    private static HexTerrainType ClassifyTerrain(GeneratedWorldCell cell, HexWaterKind water)
    {
        if (water == HexWaterKind.River) return HexTerrainType.River;
        if (water == HexWaterKind.Lake) return HexTerrainType.Lake;
        if (water == HexWaterKind.Ocean) return cell.WaterDepthMeters > 700f ? HexTerrainType.DeepWater : HexTerrainType.ShallowWater;
        if ((cell.ElevationMeters > 1500f && cell.Slope > 250f) || cell.ElevationMeters > 2650f)
            return HexTerrainType.Mountain;
        if (cell.Slope > 540f || cell.ElevationMeters > 1600f || cell.Substrate is SubstrateKind.BareRock or SubstrateKind.Basalt)
            return HexTerrainType.Rocky;
        if (cell.Substrate == SubstrateKind.Sand && cell.ElevationMeters < 220f)
            return HexTerrainType.Sand;
        if (cell.Humidity < 0.28f && cell.TemperatureCelsius > 18f)
            return HexTerrainType.Desert;
        return HexTerrainType.Grassland;
    }

    private static float MovementCost(HexTerrainType terrain, float elevationMeters)
    {
        var cost = terrain switch
        {
            HexTerrainType.DeepWater => 2.8f,
            HexTerrainType.ShallowWater => 2.0f,
            HexTerrainType.Lake => 2.3f,
            HexTerrainType.River => 1.6f,
            HexTerrainType.Sand => 1.18f,
            HexTerrainType.Desert => 1.32f,
            HexTerrainType.Grassland => 1.0f,
            HexTerrainType.Rocky => 1.45f,
            HexTerrainType.Mountain => 2.25f,
            _ => 1f
        };
        if (elevationMeters > 1450f)
            cost += Math.Clamp((elevationMeters - 1450f) / 6000f, 0f, 0.35f);
        return Math.Max(0.35f, cost);
    }
}
