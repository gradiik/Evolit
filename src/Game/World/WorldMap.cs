using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Evolit.Game;

public enum HexTerrainType
{
    DeepWater,
    ShallowWater,
    Lake,
    River,
    Sand,
    Desert,
    Grassland,
    Rocky,
    Mountain
}

public enum HexWaterKind
{
    None,
    Ocean,
    Lake,
    River
}

public readonly record struct HexCoord(int Q, int R)
{
    private static readonly HexCoord[] Directions =
    [
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    ];

    public int S => -Q - R;

    public int DistanceTo(HexCoord other)
    {
        return (Math.Abs(Q - other.Q)
            + Math.Abs(R - other.R)
            + Math.Abs(S - other.S)) / 2;
    }

    public IEnumerable<HexCoord> Neighbors()
    {
        foreach (var direction in Directions)
            yield return new HexCoord(Q + direction.Q, R + direction.R);
    }
}

public sealed class WorldHexCell
{
    public HexCoord Coord { get; init; }
    public Vector2 WorldCenter { get; init; }
    public HexTerrainType Terrain { get; set; }
    public HexWaterKind WaterKind { get; set; }
    public float Elevation { get; set; }
    public float WaterDepth { get; set; }
    public float Humidity { get; set; }
    public float TemperatureCelsius { get; set; }
    public float ElevationMeters { get; set; }
    public float PressureKPa { get; set; }
    public float MovementCost { get; set; }
    public float MovementSpeedMultiplier { get; set; }
    public float VisualVariation { get; init; }

    public bool IsWater => WaterKind != HexWaterKind.None;
}

public sealed class WorldMap
{
    private readonly Dictionary<HexCoord, WorldHexCell> _lookup;

    public WorldMap(
        string seed,
        string sizeName,
        int radius,
        float hexSize,
        IReadOnlyList<WorldHexCell> cells)
    {
        Seed = seed;
        SizeName = sizeName;
        Radius = radius;
        HexSize = hexSize;
        Cells = cells;
        _lookup = cells.ToDictionary(cell => cell.Coord);
        Bounds = CalculateBounds(cells, hexSize);
    }

    public string Seed { get; }
    public string SizeName { get; }
    public int Radius { get; }
    public float HexSize { get; }
    public IReadOnlyList<WorldHexCell> Cells { get; }
    public Rect2 Bounds { get; }

    public bool TryGetCell(HexCoord coord, out WorldHexCell cell)
    {
        return _lookup.TryGetValue(coord, out cell!);
    }

    public WorldHexCell? GetCellAtWorld(Vector2 worldPosition)
    {
        var coord = WorldToHex(worldPosition, HexSize);
        return _lookup.GetValueOrDefault(coord);
    }

    public Vector2 FindNearestLandPosition(Vector2 desired)
    {
        WorldHexCell? best = null;
        var bestDistance = float.MaxValue;

        foreach (var cell in Cells)
        {
            if (cell.IsWater || cell.Terrain == HexTerrainType.Mountain)
                continue;

            var distance = desired.DistanceSquaredTo(cell.WorldCenter);
            if (distance >= bestDistance)
                continue;

            best = cell;
            bestDistance = distance;
        }

        return best?.WorldCenter ?? Vector2.Zero;
    }

    public static Vector2 HexToWorld(HexCoord coord, float hexSize)
    {
        var x = hexSize * Mathf.Sqrt(3f) * (coord.Q + coord.R * 0.5f);
        var y = hexSize * 1.5f * coord.R;
        return new Vector2(x, y);
    }

    public static HexCoord WorldToHex(Vector2 point, float hexSize)
    {
        var q = (Mathf.Sqrt(3f) / 3f * point.X - point.Y / 3f) / hexSize;
        var r = (2f / 3f * point.Y) / hexSize;
        return RoundAxial(q, r);
    }

    private static HexCoord RoundAxial(float q, float r)
    {
        var x = q;
        var z = r;
        var y = -x - z;

        var rx = Mathf.Round(x);
        var ry = Mathf.Round(y);
        var rz = Mathf.Round(z);

        var xDiff = Mathf.Abs(rx - x);
        var yDiff = Mathf.Abs(ry - y);
        var zDiff = Mathf.Abs(rz - z);

        if (xDiff > yDiff && xDiff > zDiff)
            rx = -ry - rz;
        else if (yDiff > zDiff)
            ry = -rx - rz;
        else
            rz = -rx - ry;

        return new HexCoord((int)rx, (int)rz);
    }

    private static Rect2 CalculateBounds(
        IReadOnlyList<WorldHexCell> cells,
        float hexSize)
    {
        if (cells.Count == 0)
            return new Rect2(-hexSize, -hexSize, hexSize * 2, hexSize * 2);

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        var horizontal = Mathf.Sqrt(3f) * hexSize * 0.5f;
        foreach (var cell in cells)
        {
            minX = Math.Min(minX, cell.WorldCenter.X - horizontal);
            maxX = Math.Max(maxX, cell.WorldCenter.X + horizontal);
            minY = Math.Min(minY, cell.WorldCenter.Y - hexSize);
            maxY = Math.Max(maxY, cell.WorldCenter.Y + hexSize);
        }

        return new Rect2(
            minX,
            minY,
            Math.Max(1f, maxX - minX),
            Math.Max(1f, maxY - minY));
    }
}
