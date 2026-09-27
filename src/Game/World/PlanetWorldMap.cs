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
    public float LocalReliefMeters { get; init; }
    public float MineralPotential { get; init; }
    public float NutrientPotential { get; init; }
    public float GeothermalPotential { get; init; }
    public SubstrateKind Substrate { get; init; }
    public int ProvinceId { get; init; }
    public int GeologicalRegionId { get; init; } = -1;
    public int MacroplateId { get; init; } = -1;
    public float PlateBoundaryStrength { get; init; }
    public float Continentalness { get; init; }
    public float TectonicUplift { get; init; }
    public int CoastDistance { get; init; }
    public int BasinId { get; init; }
    public int RiverLength { get; init; }
    public float RiverWidth { get; init; }
    public int DrainageTarget { get; init; } = -1;
    public int StreamOrder { get; init; }
    public int UpstreamBranches { get; init; }
    public int RiverDirection { get; init; } = -1;

    public bool IsWater => WaterKind != HexWaterKind.None;
}

public sealed class PlanetWorldMap
{
    private const int BucketResolution = 18;
    private readonly Dictionary<CellId, PlanetWorldCell> _byId;
    private readonly Dictionary<(int X, int Y, int Z), List<int>> _buckets = new();
    private readonly float[] _cornerTerrainElevationMeters;
    private readonly float[] _cornerSurfaceElevationMeters;

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
        _cornerTerrainElevationMeters = BuildCornerElevations(geometry, cells, static cell => cell.ElevationMeters);
        _cornerSurfaceElevationMeters = BuildCornerElevations(geometry, cells, VisibleSurfaceElevationMeters);
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

    public bool TryGetCell(int index, out PlanetWorldCell cell)
    {
        if ((uint)index >= (uint)Cells.Count)
        {
            cell = null!;
            return false;
        }
        cell = Cells[index];
        return true;
    }

    public ReadOnlySpan<CoreVector3> GetPolygon(PlanetWorldCell cell) => Geometry.GetPolygon(cell.Index);

    public ReadOnlySpan<float> GetPolygonTerrainElevationMeters(PlanetWorldCell cell)
    {
        var start = Geometry.PolygonOffsets[cell.Index];
        var length = Geometry.PolygonOffsets[cell.Index + 1] - start;
        return _cornerTerrainElevationMeters.AsSpan(start, length);
    }

    public ReadOnlySpan<float> GetPolygonSurfaceElevationMeters(PlanetWorldCell cell)
    {
        var start = Geometry.PolygonOffsets[cell.Index];
        var length = Geometry.PolygonOffsets[cell.Index + 1] - start;
        return _cornerSurfaceElevationMeters.AsSpan(start, length);
    }

    public static float VisibleSurfaceElevationMeters(PlanetWorldCell cell) => cell.WaterKind switch
    {
        HexWaterKind.Ocean => 0f,
        HexWaterKind.Lake => cell.ElevationMeters + Math.Max(0f, cell.WaterDepthMeters),
        _ => cell.ElevationMeters
    };

    public PlanetWorldCell FindNearestCell(CoreVector3 direction)
    {
        var target = direction.Normalized();
        var bucket = BucketFor(target);
        var bestIndex = -1;
        var bestDot = float.MinValue;

        // Always inspect the full local neighbourhood before accepting a
        // candidate. Stopping at the first non-empty bucket can select the
        // wrong cell when the true nearest centre lies just across a bucket
        // boundary.
        const int localSearchRadius = 2;
        for (var dx = -localSearchRadius; dx <= localSearchRadius; dx++)
        for (var dy = -localSearchRadius; dy <= localSearchRadius; dy++)
        for (var dz = -localSearchRadius; dz <= localSearchRadius; dz++)
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

    private static float[] BuildCornerElevations(
        PlanetSurfaceGeometry geometry,
        IReadOnlyList<PlanetWorldCell> cells,
        Func<PlanetWorldCell, float> valueSelector)
    {
        var aggregates = new Dictionary<CornerKey, (double Sum, int Count)>();
        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            var value = valueSelector(cells[cellIndex]);
            var polygon = geometry.GetPolygon(cellIndex);
            for (var p = 0; p < polygon.Length; p++)
            {
                var key = CornerKey.From(polygon[p]);
                if (aggregates.TryGetValue(key, out var aggregate))
                    aggregates[key] = (aggregate.Sum + value, aggregate.Count + 1);
                else
                    aggregates.Add(key, (value, 1));
            }
        }

        var result = new float[geometry.PolygonVertices.Length];
        for (var i = 0; i < geometry.PolygonVertices.Length; i++)
        {
            var aggregate = aggregates[CornerKey.From(geometry.PolygonVertices[i])];
            result[i] = (float)(aggregate.Sum / Math.Max(1, aggregate.Count));
        }
        return result;
    }

    private readonly record struct CornerKey(long X, long Y, long Z)
    {
        public static CornerKey From(CoreVector3 value) => new(
            (long)MathF.Round(value.X * 100_000_000f),
            (long)MathF.Round(value.Y * 100_000_000f),
            (long)MathF.Round(value.Z * 100_000_000f));
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
        var savedElevation = new float[topology.Count];
        for (var i = 0; i < topology.Count; i++)
        {
            var id = topology.GetCellId(i);
            if (savedById.TryGetValue(id.Value, out var saved))
                savedElevation[i] = saved.ElevationMeters;
        }
        var (derivedSlope, derivedRelief) = WorldTerrainMetrics.Compute(topology, savedElevation);
        var cells = new PlanetWorldCell[topology.Count];
        for (var i = 0; i < topology.Count; i++)
        {
            var id = topology.GetCellId(i);
            if (!savedById.TryGetValue(id.Value, out var saved))
                throw new InvalidOperationException($"Saved planet is missing cell {id}.");

            var water = Enum.IsDefined(typeof(HexWaterKind), saved.WaterKind)
                ? (HexWaterKind)saved.WaterKind : HexWaterKind.None;
            var substrate = Enum.IsDefined((SubstrateKind)saved.Substrate)
                ? (SubstrateKind)saved.Substrate : SubstrateKind.Unknown;
            var localSlope = saved.Slope > 0f ? saved.Slope : derivedSlope[i];
            var localRelief = saved.LocalReliefMeters > 0f
                ? saved.LocalReliefMeters
                : derivedRelief[i];
            var terrain = ClassifyTerrain(
                saved.ElevationMeters,
                Math.Max(0f, saved.WaterDepthMeters),
                saved.TemperatureCelsius,
                saved.Humidity,
                substrate,
                water,
                localSlope,
                localRelief,
                saved.TectonicUplift);

            var drainageTarget = saved.DrainageTarget is >= 0 && saved.DrainageTarget < topology.Count
                ? saved.DrainageTarget
                : -1;
            if (drainageTarget >= 0 && NeighborOrdinal(topology, i, drainageTarget) < 0)
                drainageTarget = -1;
            if (drainageTarget < 0 && water == HexWaterKind.River)
                drainageTarget = InferLegacyDrainageTarget(i, saved, topology, savedById);

            var degree = topology.GetNeighborIndices(i).Length;
            var riverDirection = saved.RiverDirection is >= 0 && saved.RiverDirection < degree
                ? saved.RiverDirection
                : -1;
            if (riverDirection < 0 && drainageTarget >= 0)
                riverDirection = NeighborOrdinal(topology, i, drainageTarget);

            var streamOrder = saved.StreamOrder > 0
                ? Math.Clamp(saved.StreamOrder, 1, 8)
                : water == HexWaterKind.River
                    ? Math.Clamp(1 + (int)MathF.Log2(1f + Math.Max(0f, saved.FlowAccumulation) / 20f), 1, 8)
                    : 0;

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
                MovementCost = water == HexWaterKind.River ? 1.6f : Math.Max(0.01f, saved.MovementCost),
                MovementSpeedMultiplier = water == HexWaterKind.River
                    ? Math.Clamp(1f / 1.6f, 0.22f, 1.2f)
                    : Math.Max(0f, saved.MovementSpeedMultiplier),
                VisualVariation = Math.Clamp(saved.VisualVariation, 0f, 1f),
                FlowAccumulation = Math.Max(0f, saved.FlowAccumulation),
                Slope = Math.Max(0f, localSlope),
                LocalReliefMeters = Math.Max(0f, localRelief),
                MineralPotential = Math.Clamp(saved.MineralPotential, 0f, 1f),
                NutrientPotential = Math.Clamp(saved.NutrientPotential, 0f, 1f),
                GeothermalPotential = Math.Clamp(saved.GeothermalPotential, 0f, 1f),
                Substrate = substrate,
                ProvinceId = saved.ProvinceId,
                GeologicalRegionId = saved.GeologicalRegionId >= 0 ? saved.GeologicalRegionId : saved.ProvinceId,
                MacroplateId = saved.MacroplateId >= 0 ? saved.MacroplateId : saved.ProvinceId,
                PlateBoundaryStrength = Math.Clamp(saved.PlateBoundaryStrength, 0f, 1f),
                Continentalness = saved.Continentalness,
                TectonicUplift = saved.TectonicUplift,
                CoastDistance = saved.CoastDistance,
                BasinId = saved.BasinId,
                RiverLength = saved.RiverLength,
                RiverWidth = saved.RiverWidth,
                DrainageTarget = drainageTarget,
                StreamOrder = streamOrder,
                UpstreamBranches = Math.Max(0, saved.UpstreamBranches),
                RiverDirection = riverDirection
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
        var terrain = ClassifyTerrain(
            source.ElevationMeters,
            source.WaterDepthMeters,
            source.TemperatureCelsius,
            source.Humidity,
            source.Substrate,
            water,
            source.Slope,
            source.LocalReliefMeters,
            source.TectonicUplift);
        var cost = MovementCost(terrain, water, source.ElevationMeters);
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
            LocalReliefMeters = source.LocalReliefMeters,
            MineralPotential = source.MineralPotential,
            NutrientPotential = source.NutrientPotential,
            GeothermalPotential = source.GeothermalPotential,
            Substrate = source.Substrate,
            ProvinceId = source.ProvinceId,
            GeologicalRegionId = source.GeologicalRegionId,
            MacroplateId = source.MacroplateId,
            PlateBoundaryStrength = source.PlateBoundaryStrength,
            Continentalness = source.Continentalness,
            TectonicUplift = source.TectonicUplift,
            CoastDistance = source.CoastDistance,
            BasinId = source.BasinId,
            RiverLength = source.RiverLength,
            RiverWidth = source.RiverWidth,
            DrainageTarget = source.DrainageTarget,
            StreamOrder = source.StreamOrder,
            UpstreamBranches = source.UpstreamBranches,
            RiverDirection = source.RiverDirection
        };
    }

    private static int InferLegacyDrainageTarget(
        int index,
        PlanetWorldCellSaveState source,
        WorldTopology topology,
        IReadOnlyDictionary<long, PlanetWorldCellSaveState> savedById)
    {
        var sourceSurface = source.ElevationMeters +
            ((HexWaterKind)source.WaterKind == HexWaterKind.Lake ? Math.Max(0f, source.WaterDepthMeters) : 0f);
        var best = -1;
        var bestSurface = float.MaxValue;
        var neighbors = topology.GetNeighborIndices(index);

        for (var n = 0; n < neighbors.Length; n++)
        {
            var neighborIndex = neighbors[n];
            var neighborId = topology.GetCellId(neighborIndex).Value;
            if (!savedById.TryGetValue(neighborId, out var candidate))
                continue;

            var candidateWater = Enum.IsDefined(typeof(HexWaterKind), candidate.WaterKind)
                ? (HexWaterKind)candidate.WaterKind
                : HexWaterKind.None;
            var surface = candidate.ElevationMeters +
                (candidateWater == HexWaterKind.Lake ? Math.Max(0f, candidate.WaterDepthMeters) : 0f);

            if (surface < bestSurface ||
                (Math.Abs(surface - bestSurface) <= 0.001f && (best < 0 || neighborIndex < best)))
            {
                best = neighborIndex;
                bestSurface = surface;
            }
        }

        return best >= 0 && bestSurface <= sourceSurface + 2f ? best : -1;
    }

    private static int NeighborOrdinal(WorldTopology topology, int sourceIndex, int targetIndex)
    {
        var neighbors = topology.GetNeighborIndices(sourceIndex);
        for (var n = 0; n < neighbors.Length; n++)
            if (neighbors[n] == targetIndex)
                return n;
        return -1;
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

    private static HexTerrainType ClassifyTerrain(
        float elevationMeters,
        float waterDepthMeters,
        float temperature,
        float humidity,
        SubstrateKind substrate,
        HexWaterKind water,
        float slope,
        float localReliefMeters = 0f,
        float tectonicUplift = 0f) =>
        WorldTerrainClassifier.Classify(
            elevationMeters,
            waterDepthMeters,
            temperature,
            humidity,
            substrate,
            water,
            slope,
            localReliefMeters,
            tectonicUplift);

    private static float MovementCost(
        HexTerrainType terrain,
        HexWaterKind water,
        float elevationMeters)
    {
        if (water == HexWaterKind.River)
            return 1.6f;

        var cost = terrain switch
        {
            HexTerrainType.DeepWater => 2.8f,
            HexTerrainType.ShallowWater => 2.0f,
            HexTerrainType.Lake => 2.3f,
            HexTerrainType.Sand => 1.18f,
            HexTerrainType.Desert => 1.32f,
            HexTerrainType.Grassland => 1.0f,
            HexTerrainType.Highland => 1.18f,
            HexTerrainType.Rocky => 1.45f,
            HexTerrainType.Mountain => 2.25f,
            _ => 1f
        };
        if (elevationMeters > 1450f)
            cost += Math.Clamp((elevationMeters - 1450f) / 6000f, 0f, 0.35f);
        return Math.Max(0.35f, cost);
    }
}
