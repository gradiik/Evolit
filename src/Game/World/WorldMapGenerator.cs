using System;
using System.Collections.Generic;
using Evolit.Core;
using Evolit.Save;
using Godot;

namespace Evolit.Game;

public static class WorldMapGenerator
{
    private const float HexSize = 42f;

    public static WorldMap Generate(
        string seed,
        string sizeName,
        WorldLandAmount landAmount = WorldLandAmount.Normal,
        WorldClimate climate = WorldClimate.Temperate,
        GeologicalActivity geology = GeologicalActivity.Normal,
        bool legacyScale = false)
    {
        var radius = RadiusFor(sizeName, legacyScale);
        var generated = ProceduralWorldGenerator.Generate(
            new WorldGenerationSettings(seed, radius, landAmount, climate, geology));

        var cells = new List<WorldHexCell>(generated.Cells.Length);
        foreach (var source in generated.Cells)
            cells.Add(FromGenerated(source, seed));

        return new WorldMap(
            seed,
            sizeName,
            radius,
            HexSize,
            cells,
            generated.Topology,
            generated.Environment,
            generated.Presentation);
    }

    public static WorldMap Restore(string seed, string sizeName, WorldMapSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Cells.Count == 0)
            throw new InvalidOperationException("Saved world map has no cells.");

        var hexSize = state.HexSize > 0f ? state.HexSize : HexSize;
        var radius = state.Radius > 0 ? state.Radius : RadiusFor(sizeName);
        var savedByCoord = new Dictionary<HexCoord, WorldHexCellSaveState>(state.Cells.Count);
        foreach (var saved in state.Cells)
            savedByCoord[new HexCoord(saved.Q, saved.R)] = saved;

        var cells = new List<WorldHexCell>(state.Cells.Count);
        foreach (var saved in state.Cells)
        {
            var coord = new HexCoord(saved.Q, saved.R);
            var waterKind = Enum.IsDefined(typeof(HexWaterKind), saved.WaterKind)
                ? (HexWaterKind)saved.WaterKind : HexWaterKind.None;
            var substrate = saved.Substrate is >= 0 and <= byte.MaxValue && Enum.IsDefined((SubstrateKind)saved.Substrate)
                ? (SubstrateKind)saved.Substrate : SubstrateKind.Unknown;
            var localSlope = Math.Max(0f, saved.Slope);
            var localRelief = saved.LocalReliefMeters > 0f
                ? saved.LocalReliefMeters
                : DeriveSavedLocalRelief(saved, savedByCoord);
            var terrain = ClassifyTerrain(
                saved.ElevationMeters,
                Math.Max(0f, saved.WaterDepthMeters),
                saved.TemperatureCelsius,
                saved.Humidity,
                substrate,
                waterKind,
                localSlope,
                localRelief,
                saved.TectonicUplift,
                saved.CoastDistance);

            cells.Add(new WorldHexCell
            {
                Coord = coord,
                WorldCenter = WorldMap.HexToWorld(coord, hexSize),
                Terrain = terrain,
                WaterKind = waterKind,
                Elevation = saved.Elevation,
                ElevationMeters = saved.ElevationMeters,
                WaterDepth = Math.Max(0f, saved.WaterDepth),
                WaterDepthMeters = Math.Max(0f, saved.WaterDepthMeters),
                Humidity = Math.Clamp(saved.Humidity, 0f, 1f),
                TemperatureCelsius = saved.TemperatureCelsius,
                PressureKPa = Math.Max(0f, saved.PressureKPa),
                MovementCost = Math.Max(0.01f, saved.MovementCost),
                MovementSpeedMultiplier = Math.Max(0.01f, saved.MovementSpeedMultiplier),
                VisualVariation = Math.Clamp(saved.VisualVariation, 0f, 1f),
                FlowAccumulation = Math.Max(0f, saved.FlowAccumulation),
                Slope = localSlope,
                LocalReliefMeters = localRelief,
                MineralPotential = Math.Clamp(saved.MineralPotential, 0f, 1f),
                NutrientPotential = Math.Clamp(saved.NutrientPotential, 0f, 1f),
                GeothermalPotential = Math.Clamp(saved.GeothermalPotential, 0f, 1f),
                Substrate = substrate,
                ProvinceId = saved.ProvinceId,
                Continentalness = saved.Continentalness,
                TectonicUplift = saved.TectonicUplift,
                CoastDistance = saved.CoastDistance,
                BasinId = saved.BasinId,
                RiverLength = saved.RiverLength,
                RiverWidth = saved.RiverWidth,
                StreamOrder = saved.StreamOrder,
                UpstreamBranches = saved.UpstreamBranches,
                RiverDirection = saved.RiverDirection,
                GeologicalRegionId = saved.GeologicalRegionId,
                MacroplateId = saved.MacroplateId,
                PlateBoundaryStrength = Math.Clamp(saved.PlateBoundaryStrength, 0f, 1f)
            });
        }

        return new WorldMap(
            seed,
            sizeName,
            radius,
            hexSize,
            cells,
            presentation: RestorePresentation(state.Presentation));
    }

    private static WorldGenerationPresentation RestorePresentation(WorldGeographyPresentationSaveState? state)
    {
        if (state is null)
            return WorldGenerationPresentation.Empty;

        static WorldGeometrySegment Segment(WorldGeometrySegmentSaveState value) =>
            new(value.X1, value.Y1, value.X2, value.Y2, value.Strength, value.Width);

        var style = state.Style ?? new WorldGeographyStyleSaveState();
        return new WorldGenerationPresentation
        {
            GeologicalRegionCount = state.GeologicalRegionCount,
            MacroplateCount = state.MacroplateCount,
            FieldResolution = state.FieldResolution,
            Style = new WorldGeographyStyle(
                style.ContinentalFragmentation, style.CoastRoughness, style.CoastScale,
                style.PeninsulaStrength, style.BayStrength, style.IslandArcDensity,
                style.RiftStrength, style.MountainSharpness, style.MountainWidth,
                style.PlateauStrength, style.PlainSmoothness, style.RiverMeander,
                style.ShelfWidth, style.OceanBasinDepth),
            CoastSegments = state.CoastSegments?.ConvertAll(Segment).ToArray() ?? Array.Empty<WorldGeometrySegment>(),
            RidgeSegments = state.RidgeSegments?.ConvertAll(Segment).ToArray() ?? Array.Empty<WorldGeometrySegment>()
        };
    }

    public static WorldMap RestoreFromCore(string seed, string sizeName, CoreSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var topology = snapshot.Topology ?? throw new InvalidOperationException("Core snapshot has no topology.");
        var environment = snapshot.Environment ?? throw new InvalidOperationException("Core snapshot has no environment.");
        var ids = topology.Cells ?? Array.Empty<CellId>();
        if (ids.Length == 0 || environment.ElevationMeters.Length != ids.Length)
            throw new InvalidOperationException("Core snapshot cannot reconstruct a world map.");

        var radius = 0;
        for (var i = 0; i < ids.Length; i++)
            radius = Math.Max(radius, (Math.Abs(ids[i].Q) + Math.Abs(ids[i].R) + Math.Abs(-ids[i].Q - ids[i].R)) / 2);
        var cells = new List<WorldHexCell>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            var coord = new HexCoord(id.Q, id.R);
            var elevation = environment.ElevationMeters[i];
            var depth = environment.WaterDepthMeters[i];
            var runoff = environment.Runoff.Length == ids.Length ? environment.Runoff[i] : 0f;
            var waterKind = depth > 0.1f
                ? elevation < 0f ? HexWaterKind.Ocean : runoff > 0.002f && depth < 5f ? HexWaterKind.River : HexWaterKind.Lake
                : HexWaterKind.None;
            var substrate = environment.Substrate.Length == ids.Length
                ? environment.Substrate[i]
                : SubstrateKind.Unknown;
            var terrain = ClassifyTerrain(
                elevation,
                depth,
                environment.TemperatureCelsius[i],
                environment.Humidity[i],
                substrate,
                waterKind);
            var cell = new WorldHexCell
            {
                Coord = coord,
                WorldCenter = WorldMap.HexToWorld(coord, HexSize),
                Terrain = terrain,
                WaterKind = waterKind,
                ElevationMeters = elevation,
                Elevation = elevation >= 0f
                    ? Math.Clamp(elevation / 3400f, 0f, 1f)
                    : Math.Clamp(elevation / 3900f, -1f, 0f),
                WaterDepthMeters = depth,
                WaterDepth = NormalizeWater(depth, waterKind),
                Humidity = environment.Humidity[i],
                TemperatureCelsius = environment.TemperatureCelsius[i],
                PressureKPa = environment.PressureKPa[i],
                VisualVariation = VisualVariation(id, seed),
                FlowAccumulation = 0f,
                Slope = 0f,
                LocalReliefMeters = 0f,
                MineralPotential = environment.MineralPotential[i],
                NutrientPotential = environment.NutrientPotential[i],
                GeothermalPotential = environment.GeothermalPotential[i],
                Substrate = substrate,
                ProvinceId = -1,
                BasinId = -1
            };
            cell.MovementCost = WorldMovementRules.BaseMovementCost(cell);
            cell.MovementSpeedMultiplier = WorldMovementRules.SpeedMultiplier(cell, DemoEntityKind.Creature);
            cells.Add(cell);
        }

        return new WorldMap(seed, sizeName, radius, HexSize, cells);
    }

    private static WorldHexCell FromGenerated(GeneratedWorldCell source, string seed)
    {
        var coord = new HexCoord(source.Id.Q, source.Id.R);
        var waterKind = source.IsRiver
            ? HexWaterKind.River
            : source.IsLake
                ? HexWaterKind.Lake
                : source.WaterDepthMeters > 0.1f
                    ? HexWaterKind.Ocean
                    : HexWaterKind.None;
        var cell = new WorldHexCell
        {
            Coord = coord,
            WorldCenter = WorldMap.HexToWorld(coord, HexSize),
            Elevation = source.ElevationMeters >= 0f
                ? Math.Clamp(source.ElevationMeters / 3400f, 0f, 1f)
                : Math.Clamp(source.ElevationMeters / 3900f, -1f, 0f),
            ElevationMeters = source.ElevationMeters,
            WaterDepth = NormalizeWater(source.WaterDepthMeters, waterKind),
            WaterDepthMeters = source.WaterDepthMeters,
            WaterKind = waterKind,
            Humidity = source.Humidity,
            TemperatureCelsius = source.TemperatureCelsius,
            PressureKPa = source.PressureKPa,
            Terrain = ClassifyTerrain(source, waterKind),
            VisualVariation = VisualVariation(source.Id, seed),
            FlowAccumulation = source.FlowAccumulation,
            Slope = source.Slope,
            LocalReliefMeters = source.LocalReliefMeters,
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
            RiverWidth = source.RiverWidth,
            StreamOrder = source.StreamOrder,
            UpstreamBranches = source.UpstreamBranches,
            RiverDirection = source.RiverDirection,
            GeologicalRegionId = source.GeologicalRegionId,
            MacroplateId = source.MacroplateId,
            PlateBoundaryStrength = source.PlateBoundaryStrength
        };
        cell.MovementCost = WorldMovementRules.BaseMovementCost(cell);
        cell.MovementSpeedMultiplier = WorldMovementRules.SpeedMultiplier(cell, DemoEntityKind.Creature);
        return cell;
    }

    private static HexTerrainType ClassifyTerrain(GeneratedWorldCell cell, HexWaterKind water) =>
        ClassifyTerrain(
            cell.ElevationMeters,
            cell.WaterDepthMeters,
            cell.TemperatureCelsius,
            cell.Humidity,
            cell.Substrate,
            water,
            cell.Slope,
            cell.LocalReliefMeters,
            cell.TectonicUplift,
            cell.CoastDistance);

    private static HexTerrainType ClassifyTerrain(
        float elevationMeters,
        float waterDepthMeters,
        float temperature,
        float humidity,
        SubstrateKind substrate,
        HexWaterKind water,
        float slope = 0f,
        float localReliefMeters = 0f,
        float tectonicUplift = 0f,
        int coastDistance = -1) =>
        WorldTerrainClassifier.Classify(
            elevationMeters,
            waterDepthMeters,
            temperature,
            humidity,
            substrate,
            water,
            slope,
            localReliefMeters,
            tectonicUplift,
            coastDistance);

    private static float DeriveSavedLocalRelief(
        WorldHexCellSaveState source,
        IReadOnlyDictionary<HexCoord, WorldHexCellSaveState> savedByCoord)
    {
        var land = source.ElevationMeters >= 0f;
        var min = source.ElevationMeters;
        var max = source.ElevationMeters;

        for (var dq = -2; dq <= 2; dq++)
        {
            var minDr = Math.Max(-2, -dq - 2);
            var maxDr = Math.Min(2, -dq + 2);
            for (var dr = minDr; dr <= maxDr; dr++)
            {
                var coord = new HexCoord(source.Q + dq, source.R + dr);
                if (!savedByCoord.TryGetValue(coord, out var candidate) ||
                    (candidate.ElevationMeters >= 0f) != land)
                    continue;

                min = Math.Min(min, candidate.ElevationMeters);
                max = Math.Max(max, candidate.ElevationMeters);
            }
        }

        return Math.Max(0f, max - min);
    }

    private static float NormalizeWater(float depthMeters, HexWaterKind kind) => kind switch
    {
        HexWaterKind.River => Math.Clamp(depthMeters / 32f, 0.04f, 1f),
        HexWaterKind.Lake => Math.Clamp(depthMeters / 120f, 0.05f, 1f),
        HexWaterKind.Ocean => Math.Clamp(depthMeters / 850f, 0.04f, 1f),
        _ => 0f
    };

    private static int RadiusFor(string sizeName, bool legacyScale = false)
    {
        if (legacyScale)
        {
            return sizeName switch
            {
                "Маленький" => WorldGenerationScale.LegacySmallRadius,
                "Большой" => WorldGenerationScale.LegacyLargeRadius,
                "Очень большой" => WorldGenerationScale.HugeRadius,
                _ => WorldGenerationScale.LegacyMediumRadius
            };
        }

        // 0.0.8 quality pass: double linear world extent from the original
        // 28 / 38 / 49 radii. Cell counts grow by roughly 4x.
        return sizeName switch
        {
            "Маленький" => WorldGenerationScale.SmallRadius,
            "Большой" => WorldGenerationScale.LargeRadius,
            "Очень большой" => WorldGenerationScale.HugeRadius,
            _ => WorldGenerationScale.MediumRadius
        };
    }

    private static float VisualVariation(CellId id, string seed)
    {
        var mixed = SeedMixer.Combine(SeedMixer.FromString(seed), unchecked((ulong)id.Value));
        return (float)(mixed >> 40) * (1f / 16777215f);
    }
}
