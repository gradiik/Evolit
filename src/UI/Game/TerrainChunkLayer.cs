using System;
using System.Collections.Generic;
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
    private ArrayMesh? _baseMesh;

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
        if (_kind == TerrainLayerKind.Base)
            _baseMesh = BuildBaseMesh();
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
        _baseMesh = BuildBaseMesh();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_kind == TerrainLayerKind.Base)
        {
            if (_baseMesh is not null)
                DrawMesh(_baseMesh, null, Transform2D.Identity, Colors.White);
            return;
        }

        foreach (var cell in _cells)
        {
            var center = cell.WorldCenter - _worldBounds.Position;
            switch (_kind)
            {
                case TerrainLayerKind.Detail:
                    DrawDetail(cell, center);
                    break;
                case TerrainLayerKind.Fine:
                    DrawFine(cell, center);
                    break;
            }
        }
    }

    private ArrayMesh BuildBaseMesh()
    {
        var vertices = new List<Vector2>(_cells.Length * 7);
        var colors = new List<Color>(_cells.Length * 7);
        var indices = new List<int>(_cells.Length * 18);

        foreach (var cell in _cells)
        {
            var center = cell.WorldCenter - _worldBounds.Position;
            var color = TerrainColor(cell, _debugMode);
            var baseIndex = vertices.Count;

            vertices.Add(center);
            colors.Add(color);
            for (var corner = 0; corner < 6; corner++)
            {
                vertices.Add(center + UnitHex[corner] * _hexSize);
                colors.Add(color);
            }

            for (var corner = 0; corner < 6; corner++)
            {
                indices.Add(baseIndex);
                indices.Add(baseIndex + 1 + corner);
                indices.Add(baseIndex + 1 + ((corner + 1) % 6));
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
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
                DrawCircle(center, _hexSize * 0.18f, new Color(0.48f, 0.88f, 0.86f, 0.34f));
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
            return _cells.Length == 0 ? 0 : 1;

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
            GenerationDebugMode.Continentalness => Ramp((cell.Continentalness + 1.5f) / 3f),
            GenerationDebugMode.Province => ProvinceColor(cell.ProvinceId),
            GenerationDebugMode.Elevation => Ramp((cell.ElevationMeters + 4000f) / 9000f),
            GenerationDebugMode.Slope => Ramp(cell.Slope / 1200f),
            GenerationDebugMode.CoastDistance => Ramp(Math.Clamp(cell.CoastDistance / 18f, 0f, 1f)),
            GenerationDebugMode.WaterDepth => cell.WaterDepthMeters > 0f
                ? new Color(0.05f, Math.Clamp(0.28f + cell.WaterDepthMeters / 6000f, 0.28f, 0.62f), 0.92f, 1f)
                : new Color(0.08f, 0.10f, 0.10f, 1f),
            GenerationDebugMode.FlowAccumulation => Ramp(MathF.Log10(1f + cell.FlowAccumulation) / 3.5f),
            GenerationDebugMode.Basin => BasinColor(cell.BasinId),
            GenerationDebugMode.TectonicUplift => Ramp(cell.TectonicUplift),
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

    private static Color ProvinceColor(int provinceId)
    {
        if (provinceId < 0)
            return new Color(0.12f, 0.14f, 0.16f);

        var x = unchecked((uint)provinceId * 2654435761u);
        var r = 0.25f + ((x & 0xFFu) / 255f) * 0.65f;
        var g = 0.25f + (((x >> 8) & 0xFFu) / 255f) * 0.65f;
        var b = 0.25f + (((x >> 16) & 0xFFu) / 255f) * 0.65f;
        return new Color(r, g, b);
    }

    private static Color BasinColor(int basinId)
    {
        if (basinId < 0)
            return new Color(0.06f, 0.16f, 0.24f);

        var x = unchecked((uint)basinId * 2246822519u + 3266489917u);
        var r = 0.22f + ((x & 0xFFu) / 255f) * 0.68f;
        var g = 0.22f + (((x >> 8) & 0xFFu) / 255f) * 0.68f;
        var b = 0.22f + (((x >> 16) & 0xFFu) / 255f) * 0.68f;
        return new Color(r, g, b);
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

    private static Color SurfaceClimateColor(WorldHexCell cell)
    {
        var moisture = Math.Clamp((cell.Humidity - 0.12f) / 0.62f, 0f, 1f);
        var dry = new Color(0.53f, 0.44f, 0.25f);
        var humid = new Color(0.20f, 0.43f, 0.24f);
        var cold = Math.Clamp((8f - cell.TemperatureCelsius) / 28f, 0f, 0.28f);

        var r = dry.R + (humid.R - dry.R) * moisture;
        var g = dry.G + (humid.G - dry.G) * moisture;
        var b = dry.B + (humid.B - dry.B) * moisture;
        var average = (r + g + b) / 3f;
        return new Color(
            r + (average - r) * cold,
            g + (average - g) * cold,
            b + (average - b) * cold,
            1f);
    }

    private static Color DeepWaterColor(WorldHexCell cell)
    {
        var depth = Math.Clamp(cell.WaterDepthMeters / 2200f, 0f, 1f);
        var shallow = new Color(0.035f, 0.145f, 0.205f);
        var abyss = new Color(0.012f, 0.058f, 0.072f);
        return new Color(
            shallow.R + (abyss.R - shallow.R) * depth,
            shallow.G + (abyss.G - shallow.G) * depth,
            shallow.B + (abyss.B - shallow.B) * depth,
            1f);
    }

    private static Color TerrainColor(WorldHexCell cell, GenerationDebugMode debugMode)
    {
        if (debugMode != GenerationDebugMode.None)
            return DebugColor(cell, debugMode);

        var baseColor = cell.Terrain switch
        {
            HexTerrainType.DeepWater => DeepWaterColor(cell),
            HexTerrainType.ShallowWater => new Color(0.055f, 0.255f, 0.300f),
            HexTerrainType.Lake => new Color(0.060f, 0.305f, 0.325f),
            HexTerrainType.River => new Color(0.070f, 0.420f, 0.455f),
            HexTerrainType.Sand => new Color(0.49f, 0.45f, 0.29f),
            HexTerrainType.Desert => SurfaceClimateColor(cell),
            HexTerrainType.Grassland => SurfaceClimateColor(cell),
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
