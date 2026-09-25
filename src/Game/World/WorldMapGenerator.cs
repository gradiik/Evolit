using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Evolit.Game;

public static class WorldMapGenerator
{
    private const float SeaLevel = 0f;
    private const float HexSize = 42f;

    public static WorldMap Generate(string seed, string sizeName)
    {
        // Roughly triple the playable area of the 0.0.5 baseline without
        // tripling the linear radius (which would create ~9x as many cells).
        var radius = sizeName switch
        {
            "Маленький" => 28,
            "Большой" => 49,
            _ => 38
        };

        var seedValue = StableHash(seed);
        var cells = CreateBaseCells(radius, seedValue);
        var lookup = cells.ToDictionary(cell => cell.Coord);

        CarveLakes(cells, lookup, radius, seedValue);
        ClassifyWaterBodies(cells, lookup, radius);
        EnsureInlandLakes(cells, lookup, radius, seedValue);
        CreateRivers(cells, lookup, radius, seedValue);
        FinalizeTerrain(cells, lookup);

        return new WorldMap(seed, sizeName, radius, HexSize, cells);
    }

    private static List<WorldHexCell> CreateBaseCells(int radius, uint seed)
    {
        var cells = new List<WorldHexCell>(1 + 3 * radius * (radius + 1));

        for (var q = -radius; q <= radius; q++)
        {
            var rMin = Math.Max(-radius, -q - radius);
            var rMax = Math.Min(radius, -q + radius);

            for (var r = rMin; r <= rMax; r++)
            {
                var coord = new HexCoord(q, r);
                var center = WorldMap.HexToWorld(coord, HexSize);
                var radial = coord.DistanceTo(new HexCoord(0, 0)) / (float)radius;

                var macro = Fbm(
                    q * 0.095f + 13.7f,
                    r * 0.095f - 7.9f,
                    seed,
                    4);
                var regional = Fbm(
                    q * 0.045f - 31.2f,
                    r * 0.045f + 19.4f,
                    seed ^ 0xA53C91u,
                    3);
                var ridgeNoise = Fbm(
                    q * 0.13f + 4.1f,
                    r * 0.13f + 17.8f,
                    seed ^ 0x71D4B7u,
                    3);
                var ridge = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);

                var islandFalloff = 0.50f - radial * 0.82f;
                var elevation = islandFalloff
                    + (macro - 0.5f) * 0.66f
                    + (regional - 0.5f) * 0.30f
                    + Mathf.Max(0f, ridge - 0.66f) * 0.35f;

                if (radial > 0.86f)
                    elevation -= (radial - 0.86f) * 1.75f;

                var humidity = Mathf.Clamp(
                    Fbm(
                        q * 0.070f + 91.3f,
                        r * 0.070f - 44.8f,
                        seed ^ 0x33F10Du,
                        4),
                    0f,
                    1f);

                var temperatureNoise =
                    Fbm(
                        q * 0.045f - 73.5f,
                        r * 0.045f + 28.7f,
                        seed ^ 0x6E71A3u,
                        3) - 0.5f;
                var latitude = Mathf.Clamp(
                    Mathf.Abs(center.Y) / Math.Max(1f, radius * HexSize * 1.5f),
                    0f,
                    1f);
                var clampedElevation = Mathf.Clamp(elevation, -0.85f, 0.95f);
                var elevationMeters = clampedElevation >= 0f
                    ? clampedElevation * 3200f
                    : clampedElevation * 700f;
                var temperature = 29f
                    - latitude * 17f
                    - Math.Max(0f, elevationMeters) * 0.0062f
                    + temperatureNoise * 8f;

                cells.Add(new WorldHexCell
                {
                    Coord = coord,
                    WorldCenter = center,
                    Elevation = clampedElevation,
                    ElevationMeters = elevationMeters,
                    Humidity = humidity,
                    TemperatureCelsius = temperature,
                    PressureKPa = AtmosphericPressure(elevationMeters),
                    Terrain = HexTerrainType.Grassland,
                    WaterKind = HexWaterKind.None,
                    WaterDepth = 0,
                    MovementCost = 1f,
                    MovementSpeedMultiplier = 1f,
                    VisualVariation = Hash01(q, r, seed ^ 0xC18A57u)
                });
            }
        }

        return cells;
    }

    private static void CarveLakes(
        IReadOnlyList<WorldHexCell> cells,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup,
        int radius,
        uint seed)
    {
        var targetCount = radius switch
        {
            <= 28 => 3,
            >= 49 => 6,
            _ => 4
        };

        var candidates = cells
            .Where(cell =>
                cell.Coord.DistanceTo(new HexCoord(0, 0)) < radius * 0.64f
                && cell.Elevation > 0.015f
                && cell.Elevation < 0.30f
                && cell.Humidity > 0.38f
                && cell.Coord.Neighbors().Count(coord =>
                    lookup.TryGetValue(coord, out var neighbor)
                    && neighbor.Elevation >= cell.Elevation) >= 4)
            .OrderBy(cell =>
                cell.Elevation * 1.6f
                - cell.Humidity * 0.35f
                + Hash01(cell.Coord.Q, cell.Coord.R, seed ^ 0x8F27C1u) * 0.16f)
            .ToList();

        var centers = new List<HexCoord>();
        foreach (var candidate in candidates)
        {
            if (centers.Count >= targetCount)
                break;

            if (centers.Any(center => center.DistanceTo(candidate.Coord) < 8))
                continue;

            var basin = CarveIrregularBasin(
                candidate,
                lookup,
                seed ^ (uint)(centers.Count * 0x45D9F3B),
                markAsLake: false);

            if (basin.Count < 4)
                continue;

            centers.Add(candidate.Coord);
        }
    }

    private static List<WorldHexCell> CarveIrregularBasin(
        WorldHexCell center,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup,
        uint seed,
        bool markAsLake)
    {
        var desiredCells = 6 + (int)MathF.Round(
            Hash01(center.Coord.Q, center.Coord.R, seed ^ 0xD3A14Bu) * 12f);
        var basin = new List<WorldHexCell>(desiredCells);
        var visited = new HashSet<HexCoord> { center.Coord };
        var frontier = new PriorityQueue<WorldHexCell, float>();
        frontier.Enqueue(center, center.Elevation);

        while (basin.Count < desiredCells
               && frontier.TryDequeue(out var current, out _))
        {
            if (markAsLake && current.WaterKind == HexWaterKind.Ocean)
                continue;

            var distance = center.Coord.DistanceTo(current.Coord);
            if (distance > 5)
                continue;

            var depthNoise = Hash01(
                current.Coord.Q,
                current.Coord.R,
                seed ^ 0xB7412Du);
            var centerWeight = 1f - Mathf.Clamp(distance / 5f, 0f, 1f);
            var targetElevation =
                -0.025f
                - centerWeight * 0.070f
                - depthNoise * 0.030f;

            current.Elevation = Math.Min(current.Elevation, targetElevation);
            if (markAsLake)
                current.WaterKind = HexWaterKind.Lake;
            basin.Add(current);

            foreach (var coord in current.Coord.Neighbors())
            {
                if (!visited.Add(coord)
                    || !lookup.TryGetValue(coord, out var neighbor)
                    || neighbor.WaterKind == HexWaterKind.Ocean)
                {
                    continue;
                }

                var nextDistance = center.Coord.DistanceTo(coord);
                if (nextDistance > 5
                    || (nextDistance > 1
                        && neighbor.Elevation > center.Elevation + 0.24f))
                {
                    continue;
                }

                var irregularity =
                    (Hash01(coord.Q, coord.R, seed ^ 0x51AF91u) - 0.5f)
                    * 0.22f;
                var priority =
                    neighbor.Elevation * 1.8f
                    + nextDistance * 0.045f
                    + irregularity;
                frontier.Enqueue(neighbor, priority);
            }
        }

        return basin;
    }

    private static void ClassifyWaterBodies(
        IReadOnlyList<WorldHexCell> cells,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup,
        int radius)
    {
        foreach (var cell in cells)
        {
            if (cell.Elevation < SeaLevel)
                cell.WaterKind = HexWaterKind.Lake;
        }

        var queue = new Queue<WorldHexCell>();
        var visited = new HashSet<HexCoord>();

        foreach (var cell in cells)
        {
            if (cell.WaterKind == HexWaterKind.None
                || cell.Coord.DistanceTo(new HexCoord(0, 0)) < radius - 1)
            {
                continue;
            }

            cell.WaterKind = HexWaterKind.Ocean;
            queue.Enqueue(cell);
            visited.Add(cell.Coord);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var coord in current.Coord.Neighbors())
            {
                if (!lookup.TryGetValue(coord, out var neighbor)
                    || neighbor.WaterKind == HexWaterKind.None
                    || !visited.Add(coord))
                {
                    continue;
                }

                neighbor.WaterKind = HexWaterKind.Ocean;
                queue.Enqueue(neighbor);
            }
        }
    }

    private static void EnsureInlandLakes(
        IReadOnlyList<WorldHexCell> cells,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup,
        int radius,
        uint seed)
    {
        var targetCount = radius switch
        {
            <= 28 => 3,
            >= 49 => 6,
            _ => 4
        };

        var existing = FindLakeComponents(cells, lookup);
        if (existing.Count >= targetCount)
            return;

        var occupiedCenters = existing
            .Where(component => component.Count > 0)
            .Select(component => component[component.Count / 2].Coord)
            .ToList();

        var candidates = cells
            .Where(cell =>
                cell.WaterKind == HexWaterKind.None
                && cell.Coord.DistanceTo(new HexCoord(0, 0)) < radius * 0.62f
                && cell.Elevation < 0.26f
                && cell.Humidity > 0.34f
                && !cell.Coord.Neighbors().Any(coord =>
                    lookup.TryGetValue(coord, out var neighbor)
                    && neighbor.WaterKind == HexWaterKind.Ocean))
            .OrderBy(cell =>
                cell.Elevation
                - cell.Humidity * 0.28f
                + Hash01(cell.Coord.Q, cell.Coord.R, seed ^ 0x4B119Du) * 0.20f)
            .ToList();

        foreach (var candidate in candidates)
        {
            if (existing.Count >= targetCount)
                break;

            if (occupiedCenters.Any(center => center.DistanceTo(candidate.Coord) < 8))
                continue;

            var component = CarveIrregularBasin(
                candidate,
                lookup,
                seed ^ (uint)(existing.Count * 0x9E3779B),
                markAsLake: true);

            if (component.Count < 4)
                continue;

            existing.Add(component);
            occupiedCenters.Add(candidate.Coord);
        }
    }

    private static void CreateRivers(
        IReadOnlyList<WorldHexCell> cells,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup,
        int radius,
        uint seed)
    {
        var targetCount = radius switch
        {
            <= 28 => 4,
            >= 49 => 8,
            _ => 6
        };

        var candidates = cells
            .Where(cell =>
                cell.WaterKind == HexWaterKind.None
                && cell.Elevation > 0.25f
                && cell.Humidity > 0.42f)
            .OrderByDescending(cell =>
                cell.Elevation * 0.58f
                + cell.Humidity * 0.32f
                + Hash01(cell.Coord.Q, cell.Coord.R, seed ^ 0x9A731Fu) * 0.10f)
            .ToList();

        var sources = new List<HexCoord>();
        foreach (var source in candidates)
        {
            if (sources.Count >= targetCount)
                break;

            if (sources.Any(existing => existing.DistanceTo(source.Coord) < 7))
                continue;

            var path = FindPathToWater(source.Coord, lookup);
            if (path.Count < 4)
                continue;

            var riverCells = 0;
            foreach (var coord in path)
            {
                if (!lookup.TryGetValue(coord, out var cell))
                    continue;

                if (cell.WaterKind != HexWaterKind.None)
                    break;

                cell.WaterKind = HexWaterKind.River;
                riverCells++;
            }

            if (riverCells < 3)
                continue;

            sources.Add(source.Coord);
        }
    }

    private static List<List<WorldHexCell>> FindLakeComponents(
        IReadOnlyList<WorldHexCell> cells,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup)
    {
        var result = new List<List<WorldHexCell>>();
        var visited = new HashSet<HexCoord>();

        foreach (var cell in cells)
        {
            if (cell.WaterKind != HexWaterKind.Lake || !visited.Add(cell.Coord))
                continue;

            var component = new List<WorldHexCell>();
            var queue = new Queue<WorldHexCell>();
            queue.Enqueue(cell);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                foreach (var coord in current.Coord.Neighbors())
                {
                    if (!lookup.TryGetValue(coord, out var neighbor)
                        || neighbor.WaterKind != HexWaterKind.Lake
                        || !visited.Add(coord))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            result.Add(component);
        }

        return result;
    }

    private static List<HexCoord> FindPathToWater(
        HexCoord source,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup)
    {
        var queue = new PriorityQueue<HexCoord, float>();
        var costs = new Dictionary<HexCoord, float> { [source] = 0f };
        var previous = new Dictionary<HexCoord, HexCoord>();
        queue.Enqueue(source, 0f);

        HexCoord? goal = null;

        while (queue.TryDequeue(out var currentCoord, out var currentPriority))
        {
            if (!costs.TryGetValue(currentCoord, out var knownCost)
                || currentPriority > knownCost + 0.0001f
                || !lookup.TryGetValue(currentCoord, out var current))
            {
                continue;
            }

            if (currentCoord != source
                && current.WaterKind != HexWaterKind.None)
            {
                goal = currentCoord;
                break;
            }

            foreach (var nextCoord in currentCoord.Neighbors())
            {
                if (!lookup.TryGetValue(nextCoord, out var next))
                    continue;

                var uphill = Math.Max(0f, next.Elevation - current.Elevation);
                var downhill = Math.Max(0f, current.Elevation - next.Elevation);
                var ridgePenalty = next.Elevation > 0.68f
                    ? 5.0f
                    : next.Elevation > 0.50f
                        ? 1.6f
                        : 0f;
                var waterBonus =
                    next.WaterKind != HexWaterKind.None ? -0.75f : 0f;
                var downhillBonus = Math.Min(0.45f, downhill * 2.5f);
                var stepCost = Math.Max(
                    0.12f,
                    1f
                    + uphill * 36f
                    + ridgePenalty
                    - downhillBonus
                    + waterBonus);
                var nextCost = knownCost + stepCost;

                if (costs.TryGetValue(nextCoord, out var oldCost)
                    && oldCost <= nextCost)
                {
                    continue;
                }

                costs[nextCoord] = nextCost;
                previous[nextCoord] = currentCoord;
                queue.Enqueue(nextCoord, nextCost);
            }
        }

        if (!goal.HasValue)
            return [];

        var path = new List<HexCoord> { goal.Value };
        var cursor = goal.Value;

        while (cursor != source)
        {
            if (!previous.TryGetValue(cursor, out var parent))
                return [];

            cursor = parent;
            path.Add(cursor);
        }

        path.Reverse();
        return path;
    }

    private static void FinalizeTerrain(
        IReadOnlyList<WorldHexCell> cells,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup)
    {
        foreach (var cell in cells)
        {
            cell.Elevation = Mathf.Clamp(cell.Elevation, -0.85f, 0.95f);
            cell.ElevationMeters = cell.Elevation >= 0f
                ? cell.Elevation * 3200f
                : cell.Elevation * 700f;

            if (cell.WaterKind == HexWaterKind.Ocean)
            {
                cell.WaterDepth = Math.Max(0.04f, -cell.Elevation);
                cell.WaterDepthMeters = Math.Max(4f, cell.WaterDepth * 850f);
                cell.Terrain = cell.WaterDepth > 0.24f
                    ? HexTerrainType.DeepWater
                    : HexTerrainType.ShallowWater;
            }
            else if (cell.WaterKind == HexWaterKind.Lake)
            {
                cell.WaterDepth = Math.Max(0.05f, -cell.Elevation + 0.03f);
                cell.WaterDepthMeters = 4f + cell.WaterDepth * 120f;
                cell.Terrain = HexTerrainType.Lake;
            }
            else if (cell.WaterKind == HexWaterKind.River)
            {
                cell.WaterDepth = 0.08f;
                cell.WaterDepthMeters = 2.5f + cell.WaterDepth * 18f;
                cell.Terrain = HexTerrainType.River;
            }
            else
            {
                cell.WaterDepth = 0f;
                cell.WaterDepthMeters = 0f;

                var waterNeighbors = cell.Coord.Neighbors()
                    .Count(coord =>
                        lookup.TryGetValue(coord, out var neighbor)
                        && neighbor.IsWater);
                var touchesWater = waterNeighbors > 0;
                var nearWater = touchesWater || cell.Coord.Neighbors()
                    .Any(coord =>
                        lookup.TryGetValue(coord, out var neighbor)
                        && neighbor.Coord.Neighbors().Any(second =>
                            lookup.TryGetValue(second, out var secondNeighbor)
                            && secondNeighbor.IsWater));

                if (touchesWater)
                    cell.Humidity = Mathf.Clamp(cell.Humidity + 0.18f, 0f, 1f);
                else if (nearWater)
                    cell.Humidity = Mathf.Clamp(cell.Humidity + 0.07f, 0f, 1f);

                var primaryBeach =
                    touchesWater
                    && cell.Elevation < 0.18f
                    && (cell.VisualVariation > 0.18f
                        || cell.Humidity < 0.68f);
                var secondaryBeach =
                    !touchesWater
                    && nearWater
                    && cell.Elevation < 0.11f
                    && cell.Humidity < 0.52f
                    && cell.VisualVariation > 0.72f;

                if (cell.Elevation > 0.47f)
                    cell.Terrain = HexTerrainType.Mountain;
                else if (cell.Elevation > 0.33f)
                    cell.Terrain = HexTerrainType.Rocky;
                else if (primaryBeach || secondaryBeach)
                    cell.Terrain = HexTerrainType.Sand;
                else if (cell.Humidity < 0.30f
                         && cell.TemperatureCelsius >= 23f
                         && cell.Elevation < 0.36f)
                    cell.Terrain = HexTerrainType.Desert;
                else if (cell.Humidity < 0.20f)
                    cell.Terrain = HexTerrainType.Rocky;
                else
                    cell.Terrain = HexTerrainType.Grassland;
            }

            if (cell.IsWater)
                cell.Humidity = 1f;

            cell.PressureKPa = AmbientPressure(cell);
            cell.MovementCost = WorldMovementRules.BaseMovementCost(cell);
            cell.MovementSpeedMultiplier =
                WorldMovementRules.SpeedMultiplier(cell, DemoEntityKind.Creature);
        }
    }

    private static float AtmosphericPressure(float elevationMeters)
    {
        var altitude = Math.Max(0f, elevationMeters);
        return 101.325f * MathF.Exp(-altitude / 8434.5f);
    }

    private static float AmbientPressure(WorldHexCell cell)
    {
        var atmosphere = AtmosphericPressure(cell.ElevationMeters);
        if (!cell.IsWater)
            return atmosphere;

        return atmosphere + Math.Max(0f, cell.WaterDepthMeters) * 9.80665f;
    }

    private static float Fbm(float x, float y, uint seed, int octaves)
    {
        var value = 0f;
        var amplitude = 0.5f;
        var frequency = 1f;
        var totalAmplitude = 0f;

        for (var octave = 0; octave < octaves; octave++)
        {
            value += ValueNoise(x * frequency, y * frequency, seed + (uint)(octave * 977)) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return totalAmplitude <= 0 ? 0.5f : value / totalAmplitude;
    }

    private static float ValueNoise(float x, float y, uint seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var tx = Smooth(x - x0);
        var ty = Smooth(y - y0);

        var a = Hash01(x0, y0, seed);
        var b = Hash01(x0 + 1, y0, seed);
        var c = Hash01(x0, y0 + 1, seed);
        var d = Hash01(x0 + 1, y0 + 1, seed);

        var top = Mathf.Lerp(a, b, tx);
        var bottom = Mathf.Lerp(c, d, tx);
        return Mathf.Lerp(top, bottom, ty);
    }

    private static float Smooth(float value)
    {
        return value * value * (3f - 2f * value);
    }

    private static float Hash01(int x, int y, uint seed)
    {
        unchecked
        {
            var hash = seed;
            hash ^= (uint)x * 0x9E3779B9u;
            hash = (hash << 13) | (hash >> 19);
            hash ^= (uint)y * 0x85EBCA6Bu;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;

            foreach (var character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash == 0 ? 1u : hash;
        }
    }
}
