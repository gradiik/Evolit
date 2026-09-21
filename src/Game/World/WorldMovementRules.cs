using System;
using Godot;

namespace Evolit.Game;

public static class WorldMovementRules
{
    public static float BaseMovementCost(WorldHexCell cell)
    {
        var baseCost = cell.Terrain switch
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
            _ => 1.0f
        };

        if (!cell.IsWater && cell.Elevation > 0.45f)
            baseCost += Mathf.Clamp((cell.Elevation - 0.45f) * 0.9f, 0f, 0.35f);

        return Math.Max(0.35f, baseCost);
    }

    public static float SpeedMultiplier(WorldHexCell? cell, DemoEntityKind kind)
    {
        if (cell is null || kind == DemoEntityKind.Plant)
            return kind == DemoEntityKind.Plant ? 0f : 1f;

        return Mathf.Clamp(1f / Math.Max(0.35f, cell.MovementCost), 0.22f, 1.2f);
    }
}
