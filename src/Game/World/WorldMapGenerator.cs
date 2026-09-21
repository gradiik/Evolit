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
        CreateRivers(cells, lookup, seedValue);
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
                cell.Coord.DistanceTo(new HexCoord(0, 0)) < radius * 0.58f
                && cell.Elevation > 0.03f
                && cell.Elevation < 0.42f)
            .OrderBy(cell => Hash01(
                cell.Coord.Q,
                cell.Coord.R,
                seed ^ 0x8F27C1u))
            .ToList();

        var centers = new List<HexCoord>();
        foreach (var candidate in candidates)
        {
            if (centers.Count >= targetCount)
                break;

            if (centers.Any(center => center.DistanceTo(candidate.Coord) < 7))
                continue;

            centers.Add(candidate.Coord);
            var lakeRadius = Hash01(
                candidate.Coord.Q,
                candidate.Coord.R,
                seed ^ 0xD3A14Bu) > 0.55f
                ? 3
                : 2;

            foreach (var cell in cells)
            {
                var distance = candidate.Coord.DistanceTo(cell.Coord);
                if (distance > lakeRadius)
                    continue;

                var targetElevation = -0.10f + distance * 0.020f;
                cell.Elevation = Math.Min(cell.Elevation, targetElevation);
            }
        }
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
                && cell.Coord.DistanceTo(new HexCoord(0, 0)) < radius * 0.58f
                && !cell.Coord.Neighbors().Any(coord =>
                    lookup.TryGetValue(coord, out var neighbor)
                    && neighbor.WaterKind == HexWaterKind.Ocean))
            .OrderBy(cell => Hash01(
                cell.Coord.Q,
                cell.Coord.R,
                seed ^ 0x4B119Du))
            .ToList();

        foreach (var candidate in candidates)
        {
            if (existing.Count >= targetCount)
                break;

            if (occupiedCenters.Any(center => center.DistanceTo(candidate.Coord) < 7))
                continue;

            var component = new List<WorldHexCell>();
            foreach (var cell in cells)
            {
                var distance = candidate.Coord.DistanceTo(cell.Coord);
                if (distance > 2 || cell.WaterKind == HexWaterKind.Ocean)
                    continue;

                cell.WaterKind = HexWaterKind.Lake;
                cell.Elevation = Math.Min(cell.Elevation, -0.065f + distance * 0.018f);
                component.Add(cell);
            }

            if (component.Count == 0)
                continue;

            existing.Add(component);
            occupiedCenters.Add(candidate.Coord);
        }
    }

    private static void CreateRivers(
        IReadOnlyList<WorldHexCell> cells,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup,
        uint seed)
    {
        var components = FindLakeComponents(cells, lookup)
            .OrderByDescending(component => component.Count)
            .ThenBy(component => Hash01(
                component[0].Coord.Q,
                component[0].Coord.R,
                seed ^ 0x9A731Fu))
            .Take(3)
            .ToList();

        foreach (var component in components)
        {
            var path = FindPathToOcean(component, lookup);
            if (path.Count < 2)
                continue;

            foreach (var coord in path)
            {
                if (!lookup.TryGetValue(coord, out var cell)
                    || cell.WaterKind is HexWaterKind.Ocean or HexWaterKind.Lake)
                {
                    continue;
                }

                cell.WaterKind = HexWaterKind.River;
                cell.Elevation = Math.Min(cell.Elevation, -0.015f);
            }
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

    private static List<HexCoord> FindPathToOcean(
        IReadOnlyList<WorldHexCell> sourceLake,
        IReadOnlyDictionary<HexCoord, WorldHexCell> lookup)
    {
        var queue = new PriorityQueue<HexCoord, float>();
        var costs = new Dictionary<HexCoord, float>();
        var previous = new Dictionary<HexCoord, HexCoord>();
        var sourceCoords = sourceLake.Select(cell => cell.Coord).ToHashSet();

        foreach (var source in sourceLake)
        {
            costs[source.Coord] = 0f;
            queue.Enqueue(source.Coord, 0f);
        }

        HexCoord? goal = null;

        while (queue.TryDequeue(out var currentCoord, out var currentPriority))
        {
            if (!costs.TryGetValue(currentCoord, out var knownCost)
                || currentPriority > knownCost + 0.0001f
                || !lookup.TryGetValue(currentCoord, out var current))
            {
                continue;
            }

            if (!sourceCoords.Contains(currentCoord)
                && current.WaterKind == HexWaterKind.Ocean)
            {
                goal = currentCoord;
                break;
            }

            foreach (var nextCoord in currentCoord.Neighbors())
            {
                if (!lookup.TryGetValue(nextCoord, out var next))
                    continue;

                var uphill = Math.Max(0f, next.Elevation - current.Elevation);
                var terrainPenalty = next.Elevation > 0.58f
                    ? 4.5f
                    : next.Elevation > 0.40f
                        ? 1.4f
                        : 0f;
                var waterBonus = next.WaterKind == HexWaterKind.Ocean ? -0.55f : 0f;
                var stepCost = Math.Max(
                    0.15f,
                    1f + uphill * 12f + terrainPenalty + waterBonus);
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

        while (!sourceCoords.Contains(cursor))
        {
            if (!previous.TryGetValue(cursor, out var parent))
                break;

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
            if (cell.WaterKind == HexWaterKind.Ocean)
            {
                cell.WaterDepth = Math.Max(0.04f, -cell.Elevation);
                cell.Terrain = cell.WaterDepth > 0.24f
                    ? HexTerrainType.DeepWater
                    : HexTerrainType.ShallowWater;
            }
            else if (cell.WaterKind == HexWaterKind.Lake)
            {
                cell.WaterDepth = Math.Max(0.05f, -cell.Elevation + 0.03f);
                cell.Terrain = HexTerrainType.Lake;
            }
            else if (cell.WaterKind == HexWaterKind.River)
            {
                cell.WaterDepth = 0.08f;
                cell.Terrain = HexTerrainType.River;
            }
            else
            {
                cell.WaterDepth = 0f;
                var touchesWater = cell.Coord.Neighbors()
                    .Any(coord =>
                        lookup.TryGetValue(coord, out var neighbor)
                        && neighbor.IsWater);

                if (touchesWater)
                    cell.Humidity = Mathf.Clamp(cell.Humidity + 0.18f, 0f, 1f);

                if (cell.Elevation > 0.61f)
                    cell.Terrain = HexTerrainType.Mountain;
                else if (cell.Elevation > 0.41f)
                    cell.Terrain = HexTerrainType.Rocky;
                else if (touchesWater && cell.Elevation < 0.18f)
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

        var depthMeters = cell.Terrain switch
        {
            HexTerrainType.River => 2.5f + cell.WaterDepth * 18f,
            HexTerrainType.Lake => 6f + cell.WaterDepth * 120f,
            _ => cell.WaterDepth * 850f
        };
        return atmosphere + depthMeters * 9.80665f;
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
