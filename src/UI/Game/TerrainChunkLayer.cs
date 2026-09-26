using System;
using Evolit.Game;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

internal enum TerrainLayerKind
{
    Base,
    Detail,
    Fine
}

internal sealed class TerrainChunkSet
{
    public Rect2 Bounds { get; }
    public int CellCount { get; }
    public TerrainChunkLayer Base { get; }
    public TerrainChunkLayer Detail { get; }
    public TerrainChunkLayer Fine { get; }

    public TerrainChunkSet(
        Rect2 bounds,
        int cellCount,
        TerrainChunkLayer baseLayer,
        TerrainChunkLayer detail,
        TerrainChunkLayer fine)
    {
        Bounds = bounds;
        CellCount = cellCount;
        Base = baseLayer;
        Detail = detail;
        Fine = fine;
    }
}

internal sealed partial class TerrainChunkLayer : Control
{
    private static readonly Vector2[] UnitHex =
    [
        new Vector2(0.8660254f, 0.5f),
        new Vector2(0f, 1f),
        new Vector2(-0.8660254f, 0.5f),
        new Vector2(-0.8660254f, -0.5f),
        new Vector2(0f, -1f),
        new Vector2(0.8660254f, -0.5f)
    ];

    private WorldHexCell[] _cells = Array.Empty<WorldHexCell>();
    private Rect2 _worldBounds;
    private float _hexSize;
    private GraphicsQualityProfile _quality = GraphicsQualityProfile.From(AppSettings.Default());
    private TerrainLayerKind _kind;
    private GenerationDebugMode _debugMode;
    private readonly Vector2[] _hexPoints = new Vector2[6];
    private readonly Vector2[] _hexOutline = new Vector2[7];

    public int EstimatedCommands { get; private set; }

    public void Configure(
        WorldHexCell[] cells,
        Rect2 worldBounds,
        float hexSize,
        GraphicsQualityProfile quality,
        TerrainLayerKind kind)
    {
        _cells = cells;
        _worldBounds = worldBounds;
        _hexSize = hexSize;
        _quality = quality;
        _kind = kind;
        EstimatedCommands = EstimateCommands();

        Position = worldBounds.Position;
        Size = worldBounds.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        QueueRedraw();
    }

    public void SetDebugMode(GenerationDebugMode mode)
    {
        if (_kind != TerrainLayerKind.Base || _debugMode == mode)
            return;
        _debugMode = mode;
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var cell in _cells)
        {
            var center = cell.WorldCenter - _worldBounds.Position;
            switch (_kind)
            {
                case TerrainLayerKind.Base:
                    DrawBase(cell, center);
                    break;
                case TerrainLayerKind.Detail:
                    DrawDetail(cell, center);
                    break;
                case TerrainLayerKind.Fine:
                    DrawFine(cell, center);
                    break;
            }
        }
    }

    private void DrawBase(WorldHexCell cell, Vector2 center)
    {
        FillHexPoints(center, _hexSize, _hexPoints);
        DrawColoredPolygon(_hexPoints, TerrainColor(cell, _debugMode));
    }

    private void DrawDetail(WorldHexCell cell, Vector2 center)
    {
        if (_quality.DetailLevel <= 0)
            return;

        switch (cell.Terrain)
        {
            case HexTerrainType.Mountain:
                FillHexPoints(center, _hexSize * 0.57f, _hexPoints);
                DrawColoredPolygon(_hexPoints, new Color(0.40f, 0.43f, 0.39f, 0.28f));
                break;
            case HexTerrainType.Rocky:
            {
                var offset = new Vector2(
                    (cell.VisualVariation - 0.5f) * _hexSize * 0.22f,
                    (0.5f - cell.VisualVariation) * _hexSize * 0.14f);
                DrawCircle(center + offset, _hexSize * 0.10f, new Color(0.58f, 0.58f, 0.50f, 0.20f));
                break;
            }
            case HexTerrainType.DeepWater:
            case HexTerrainType.ShallowWater:
            case HexTerrainType.Lake:
                if (_quality.WaterDetail > 0)
                {
                    DrawArc(
                        center,
                        _hexSize * 0.52f,
                        -0.7f,
                        1.7f,
                        10,
                        new Color(0.45f, 0.78f, 0.80f, 0.10f),
                        1.0f,
                        true);
                }
                break;
            case HexTerrainType.River:
                DrawCircle(center, _hexSize * 0.12f, new Color(0.42f, 0.82f, 0.79f, 0.26f));
                break;
        }
    }

    private void DrawFine(WorldHexCell cell, Vector2 center)
    {
        if (_quality.DetailLevel <= 0)
            return;

        FillHexPoints(center, _hexSize, _hexPoints);
        for (var i = 0; i < 6; i++)
            _hexOutline[i] = _hexPoints[i];
        _hexOutline[6] = _hexPoints[0];
        DrawPolyline(_hexOutline, new Color(0.02f, 0.09f, 0.10f, 0.30f), 0.9f, true);

        if (_quality.WaterDetail < 2
            || cell.Terrain is not (HexTerrainType.DeepWater or HexTerrainType.ShallowWater or HexTerrainType.Lake))
        {
            return;
        }

        DrawArc(
            center + new Vector2(_hexSize * 0.08f, -_hexSize * 0.06f),
            _hexSize * 0.34f,
            2.1f,
            4.8f,
            8,
            new Color(0.62f, 0.90f, 0.88f, 0.08f),
            0.8f,
            true);
    }

    private int EstimateCommands()
    {
        if (_kind == TerrainLayerKind.Base)
            return _cells.Length;

        var commands = 0;
        foreach (var cell in _cells)
        {
            if (_kind == TerrainLayerKind.Detail)
            {
                if (_quality.DetailLevel <= 0)
                    continue;

                if (cell.Terrain is HexTerrainType.Mountain or HexTerrainType.Rocky or HexTerrainType.River)
                    commands++;
                else if (_quality.WaterDetail > 0
                         && (cell.Terrain is HexTerrainType.DeepWater or HexTerrainType.ShallowWater or HexTerrainType.Lake))
                    commands++;
            }
            else
            {
                if (_quality.DetailLevel <= 0)
                    continue;

                commands++;
                if (_quality.WaterDetail >= 2
                    && (cell.Terrain is HexTerrainType.DeepWater or HexTerrainType.ShallowWater or HexTerrainType.Lake))
                    commands++;
            }
        }
        return commands;
    }

    private static void FillHexPoints(Vector2 center, float radius, Vector2[] target)
    {
        for (var i = 0; i < 6; i++)
            target[i] = center + UnitHex[i] * radius;
    }

    private static Color DebugColor(WorldHexCell cell, GenerationDebugMode mode)
    {
        static Color Ramp(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            return new Color(value, 0.28f + (1f - value) * 0.42f, 1f - value * 0.78f, 1f);
        }

        return mode switch
        {
            GenerationDebugMode.Elevation => Ramp((cell.ElevationMeters + 4000f) / 7500f),
            GenerationDebugMode.WaterDepth => cell.WaterDepthMeters > 0f
                ? new Color(0.05f, Math.Clamp(0.28f + cell.WaterDepthMeters / 6000f, 0.28f, 0.62f), 0.92f, 1f)
                : new Color(0.08f, 0.10f, 0.10f, 1f),
            GenerationDebugMode.FlowAccumulation => Ramp(MathF.Log10(1f + cell.FlowAccumulation) / 3f),
            GenerationDebugMode.Temperature => Ramp((cell.TemperatureCelsius + 40f) / 85f),
            GenerationDebugMode.Humidity => Ramp(cell.Humidity),
            GenerationDebugMode.Substrate => cell.Substrate switch
            {
                Evolit.Core.SubstrateKind.BareRock => new Color(0.48f, 0.48f, 0.50f),
                Evolit.Core.SubstrateKind.Basalt => new Color(0.24f, 0.24f, 0.28f),
                Evolit.Core.SubstrateKind.VolcanicAsh => new Color(0.36f, 0.30f, 0.30f),
                Evolit.Core.SubstrateKind.Sand => new Color(0.76f, 0.67f, 0.40f),
                Evolit.Core.SubstrateKind.Sediment => new Color(0.42f, 0.55f, 0.43f),
                Evolit.Core.SubstrateKind.IceSnow => new Color(0.78f, 0.92f, 1f),
                _ => new Color(0.42f, 0.52f, 0.40f)
            },
            GenerationDebugMode.Minerals => Ramp(cell.MineralPotential),
            GenerationDebugMode.Region => InitialRegionColor(cell),
            _ => new Color(0.23f, 0.38f, 0.22f)
        };
    }

    private static Color InitialRegionColor(WorldHexCell cell)
    {
        if (cell.WaterDepthMeters > 120f) return new Color(0.05f, 0.20f, 0.58f);
        if (cell.WaterDepthMeters > 2f) return new Color(0.10f, 0.46f, 0.72f);
        if (cell.Humidity > 0.75f && cell.WaterDepthMeters > 0f) return new Color(0.16f, 0.58f, 0.42f);
        if (cell.TemperatureCelsius < -8f) return new Color(0.78f, 0.92f, 1f);
        if (cell.Substrate is Evolit.Core.SubstrateKind.BareRock or Evolit.Core.SubstrateKind.Basalt)
            return new Color(0.46f, 0.45f, 0.43f);
        if (cell.Humidity < 0.22f) return new Color(0.78f, 0.58f, 0.28f);
        if (cell.TemperatureCelsius < 5f) return new Color(0.48f, 0.70f, 0.78f);
        if (cell.TemperatureCelsius > 23f && cell.Humidity > 0.68f) return new Color(0.15f, 0.72f, 0.50f);
        return cell.Humidity > 0.52f
            ? new Color(0.24f, 0.66f, 0.40f)
            : new Color(0.60f, 0.66f, 0.34f);
    }

    private static Color TerrainColor(WorldHexCell cell, GenerationDebugMode debugMode)
    {
        if (debugMode != GenerationDebugMode.None)
            return DebugColor(cell, debugMode);

        var baseColor = cell.Terrain switch
        {
            HexTerrainType.DeepWater => new Color(0.035f, 0.145f, 0.205f),
            HexTerrainType.ShallowWater => new Color(0.055f, 0.255f, 0.300f),
            HexTerrainType.Lake => new Color(0.060f, 0.305f, 0.325f),
            HexTerrainType.River => new Color(0.080f, 0.370f, 0.385f),
            HexTerrainType.Sand => new Color(0.49f, 0.45f, 0.29f),
            HexTerrainType.Desert => new Color(0.60f, 0.49f, 0.25f),
            HexTerrainType.Grassland => new Color(0.235f, 0.405f, 0.225f),
            HexTerrainType.Rocky => new Color(0.335f, 0.365f, 0.315f),
            HexTerrainType.Mountain => new Color(0.285f, 0.315f, 0.300f),
            _ => new Color(0.23f, 0.38f, 0.22f)
        };

        var variation = (cell.VisualVariation - 0.5f) * 0.10f;
        var elevationLight = cell.IsWater
            ? -Mathf.Clamp(cell.WaterDepth * 0.32f, 0f, 0.24f)
            : Mathf.Clamp(cell.Elevation * 0.20f, -0.06f, 0.18f);
        var factor = Mathf.Clamp(1f + variation + elevationLight, 0.68f, 1.22f);

        return new Color(
            Mathf.Clamp(baseColor.R * factor, 0f, 1f),
            Mathf.Clamp(baseColor.G * factor, 0f, 1f),
            Mathf.Clamp(baseColor.B * factor, 0f, 1f),
            1f);
    }
}
