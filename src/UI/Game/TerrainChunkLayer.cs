using System;
using System.Collections.Generic;
using Evolit.Core;
using Evolit.Game;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

public readonly record struct WorldOverlaySegment(
    Vector2 Start,
    Vector2 End,
    float Strength,
    float Width);

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

    private static readonly HexCoord[] AxialDirections =
    [
        new HexCoord(1, 0),
        new HexCoord(1, -1),
        new HexCoord(0, -1),
        new HexCoord(-1, 0),
        new HexCoord(-1, 1),
        new HexCoord(0, 1)
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
    private ArrayMesh? _coastMesh;
    private ArrayMesh? _ridgeMesh;
    private ArrayMesh? _riverMesh;
    private WorldOverlaySegment[] _coastSegments = Array.Empty<WorldOverlaySegment>();
    private WorldOverlaySegment[] _ridgeSegments = Array.Empty<WorldOverlaySegment>();
    private WorldGeographyStyle _geographyStyle;

    public int EstimatedCommands { get; private set; }

    public void Configure(
        WorldHexCell[] cells,
        Rect2 worldBounds,
        float hexSize,
        GraphicsQualityProfile quality,
        TerrainLayerKind kind,
        WorldOverlaySegment[]? coastSegments = null,
        WorldOverlaySegment[]? ridgeSegments = null,
        WorldGeographyStyle? geographyStyle = null)
    {
        _cells = cells;
        _worldBounds = worldBounds;
        _hexSize = hexSize;
        _quality = quality;
        _kind = kind;
        _coastSegments = coastSegments ?? Array.Empty<WorldOverlaySegment>();
        _ridgeSegments = ridgeSegments ?? Array.Empty<WorldOverlaySegment>();
        _geographyStyle = geographyStyle ?? default;
        if (_kind == TerrainLayerKind.Base)
        {
            _baseMesh = BuildBaseMesh();
            // Coastline ribbons duplicated the edge of individual land cells;
            // terrain adjacency already provides the shoreline and the map
            // boundary is drawn separately by DemoWorldView.
            _coastMesh = null;
            // Geological information already affects the cell elevation and
            // colour. Long ribbon overlays made ridges look ruler-straight.
            _ridgeMesh = null;
            // A river occupies its complete water cell in the base mesh.
            _riverMesh = null;
        }
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
            if (_debugMode == GenerationDebugMode.None)
            {
                if (_coastMesh is not null)
                    DrawMesh(_coastMesh, null, Transform2D.Identity, Colors.White);
                if (_ridgeMesh is not null)
                    DrawMesh(_ridgeMesh, null, Transform2D.Identity, Colors.White);
                if (_riverMesh is not null)
                    DrawMesh(_riverMesh, null, Transform2D.Identity, Colors.White);
            }
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

    private ArrayMesh? BuildCoastMesh()
    {
        if (_coastSegments.Length == 0)
            return null;

        return BuildOverlayRibbonMesh(
            _coastSegments,
            wideScale: 0.30f,
            narrowScale: 0.040f,
            wideColor: new Color(0.055f, 0.255f, 0.300f, 0.74f),
            narrowColor: new Color(0.38f, 0.56f, 0.43f, 0.46f));
    }

    private ArrayMesh? BuildRidgeMesh()
    {
        if (_ridgeSegments.Length == 0)
            return null;

        return BuildOverlayRibbonMesh(
            _ridgeSegments,
            wideScale: 0.050f,
            narrowScale: 0.018f,
            wideColor: new Color(0.24f, 0.27f, 0.25f, 0.26f),
            narrowColor: new Color(0.60f, 0.60f, 0.52f, 0.18f));
    }

    private ArrayMesh? BuildOverlayRibbonMesh(
        WorldOverlaySegment[] segments,
        float wideScale,
        float narrowScale,
        Color wideColor,
        Color narrowColor)
    {
        var vertices = new List<Vector2>(segments.Length * 8);
        var colors = new List<Color>(segments.Length * 8);
        var indices = new List<int>(segments.Length * 12);

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var start = segment.Start - _worldBounds.Position;
            var end = segment.End - _worldBounds.Position;
            var delta = end - start;
            if (delta.LengthSquared() <= 0.0001f)
                continue;

            var normal = new Vector2(-delta.Y, delta.X).Normalized();
            var strength = Math.Clamp(segment.Strength, 0.15f, 1f);
            var widthFactor = Math.Clamp(segment.Width, 0.25f, 1.5f);
            AddRibbonQuad(
                start,
                end,
                normal,
                _hexSize * wideScale * widthFactor,
                new Color(
                    wideColor.R,
                    wideColor.G,
                    wideColor.B,
                    wideColor.A * strength),
                vertices,
                colors,
                indices);
            AddRibbonQuad(
                start,
                end,
                normal,
                _hexSize * narrowScale * widthFactor,
                new Color(
                    narrowColor.R,
                    narrowColor.G,
                    narrowColor.B,
                    narrowColor.A * strength),
                vertices,
                colors,
                indices);
        }

        if (indices.Count == 0)
            return null;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void AddRibbonQuad(
        Vector2 start,
        Vector2 end,
        Vector2 normal,
        float halfWidth,
        Color color,
        List<Vector2> vertices,
        List<Color> colors,
        List<int> indices)
    {
        var baseIndex = vertices.Count;
        vertices.Add(start + normal * halfWidth);
        vertices.Add(start - normal * halfWidth);
        vertices.Add(end + normal * halfWidth);
        vertices.Add(end - normal * halfWidth);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        indices.Add(baseIndex);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }

    private ArrayMesh? BuildRiverMesh()
    {
        var riverCount = 0;
        for (var i = 0; i < _cells.Length; i++)
            if (_cells[i].WaterKind == HexWaterKind.River &&
                _cells[i].RiverDirection is >= 0 and <= 5)
                riverCount++;

        if (riverCount == 0)
            return null;

        const int samplesPerSegment = 3;
        const int sampleCount = samplesPerSegment * 2 + 1;
        var vertices = new List<Vector2>(riverCount * sampleCount * 2);
        var colors = new List<Color>(riverCount * sampleCount * 2);
        var indices = new List<int>(riverCount * (sampleCount - 1) * 6);
        var riverColor = new Color(0.075f, 0.50f, 0.55f, 0.96f);

        for (var cellIndex = 0; cellIndex < _cells.Length; cellIndex++)
        {
            var cell = _cells[cellIndex];
            if (cell.WaterKind != HexWaterKind.River ||
                cell.RiverDirection is < 0 or > 5)
                continue;

            var direction = AxialDirections[cell.RiverDirection];
            var targetCoord = new HexCoord(
                cell.Coord.Q + direction.Q,
                cell.Coord.R + direction.R);

            var sourceCenter = cell.WorldCenter - _worldBounds.Position;
            var targetCenter = WorldMap.HexToWorld(targetCoord, _hexSize) - _worldBounds.Position;
            var sourceJunction = sourceCenter + RiverAnchorOffset(cell.Coord, _hexSize);
            var targetJunction = targetCenter + RiverAnchorOffset(targetCoord, _hexSize);
            var edgePoint = (sourceCenter + targetCenter) * 0.5f;

            var chord = targetCenter - sourceCenter;
            if (chord.LengthSquared() <= 0.001f)
                continue;

            var perpendicular = new Vector2(-chord.Y, chord.X).Normalized();
            var curveSign = RiverCurveSign(cell.Coord, cell.RiverDirection);
            var riverScale = Math.Clamp(cell.RiverWidth / 5.2f, 0.08f, 1f);
            var meander = _geographyStyle.RiverMeander <= 0f
                ? 0.42f
                : Math.Clamp(_geographyStyle.RiverMeander, 0f, 1f);
            var curveAmount =
                _hexSize *
                (0.035f + meander * 0.11f) *
                (1f - riverScale * 0.45f) *
                curveSign;

            var controlOut =
                (sourceJunction + edgePoint) * 0.5f +
                perpendicular * curveAmount;
            var controlIn =
                (edgePoint + targetJunction) * 0.5f -
                perpendicular * curveAmount * 0.62f;

            Span<Vector2> path = stackalloc Vector2[sampleCount];
            for (var sample = 0; sample <= samplesPerSegment; sample++)
            {
                var t = sample / (float)samplesPerSegment;
                path[sample] = QuadraticBezier(sourceJunction, controlOut, edgePoint, t);
            }
            for (var sample = 1; sample <= samplesPerSegment; sample++)
            {
                var t = sample / (float)samplesPerSegment;
                path[samplesPerSegment + sample] =
                    QuadraticBezier(edgePoint, controlIn, targetJunction, t);
            }

            var cellWidth = Mathf.Sqrt(3f) * _hexSize;
            var halfWidth = cellWidth * (0.025f + riverScale * 0.115f);
            var baseIndex = vertices.Count;

            for (var sample = 0; sample < sampleCount; sample++)
            {
                Vector2 tangent;
                if (sample == 0)
                    tangent = path[1] - path[0];
                else if (sample == sampleCount - 1)
                    tangent = path[^1] - path[^2];
                else
                    tangent = path[sample + 1] - path[sample - 1];

                if (tangent.LengthSquared() <= 0.0001f)
                    tangent = chord;

                var normal = new Vector2(-tangent.Y, tangent.X).Normalized();
                vertices.Add(path[sample] + normal * halfWidth);
                vertices.Add(path[sample] - normal * halfWidth);
                colors.Add(riverColor);
                colors.Add(riverColor);
            }

            for (var segment = 0; segment < sampleCount - 1; segment++)
            {
                var a = baseIndex + segment * 2;
                var b = a + 1;
                var c0 = a + 2;
                var d = a + 3;
                indices.Add(a);
                indices.Add(c0);
                indices.Add(b);
                indices.Add(b);
                indices.Add(c0);
                indices.Add(d);
            }
        }

        if (indices.Count == 0)
            return null;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 control, Vector2 b, float t)
    {
        var inverse = 1f - t;
        return a * (inverse * inverse) +
               control * (2f * inverse * t) +
               b * (t * t);
    }

    private static Vector2 RiverAnchorOffset(HexCoord coord, float hexSize)
    {
        var hash = unchecked(
            (uint)coord.Q * 0x9E3779B9u ^
            (uint)coord.R * 0x85EBCA6Bu ^
            0xC2B2AE35u);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;

        var x = ((hash & 0xFFFFu) / 65535f - 0.5f) * 2f;
        var y = (((hash >> 16) & 0xFFFFu) / 65535f - 0.5f) * 2f;
        var offset = new Vector2(x, y);
        if (offset.LengthSquared() > 1f)
            offset = offset.Normalized();

        // The junction stays well inside the cell. Every upstream segment ends
        // at the same deterministic point and the downstream segment starts
        // there, so tributaries form a connected sub-cell river graph.
        return offset * hexSize * 0.075f;
    }

    private static float RiverCurveSign(HexCoord coord, int direction)
    {
        var hash = unchecked(
            (uint)(coord.Q * 73856093) ^
            (uint)(coord.R * 19349663) ^
            (uint)(direction * 83492791));
        hash ^= hash >> 13;
        return (hash & 1u) == 0u ? -1f : 1f;
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
            case HexTerrainType.Highland:
            {
                var offset = new Vector2(
                    (cell.VisualVariation - 0.5f) * _hexSize * 0.16f,
                    (0.5f - cell.VisualVariation) * _hexSize * 0.10f);
                DrawCircle(center + offset, _hexSize * 0.07f, new Color(0.66f, 0.67f, 0.47f, 0.10f));
                break;
            }
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
            {
                var riverScale = Math.Clamp(cell.RiverWidth / 5.2f, 0.12f, 1f);
                var radius = _hexSize * (0.075f + riverScale * 0.16f);
                DrawCircle(center, radius, new Color(0.48f, 0.88f, 0.86f, 0.34f));
                break;
            }
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
        DrawPolyline(_hexOutline, new Color(0.02f, 0.09f, 0.10f, 0.22f), 0.62f, true);

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
        {
            if (_cells.Length == 0)
                return 0;

            var commands = 1;
            if (_coastMesh is not null) commands++;
            if (_ridgeMesh is not null) commands++;
            if (_riverMesh is not null) commands++;
            return commands;
        }

        var detailCommands = 0;
        foreach (var cell in _cells)
        {
            if (_kind == TerrainLayerKind.Detail)
            {
                if (_quality.DetailLevel <= 0)
                    continue;

                if (cell.Terrain is HexTerrainType.Mountain or HexTerrainType.Highland or HexTerrainType.Rocky or HexTerrainType.River)
                    detailCommands++;
                else if (_quality.WaterDetail > 0
                         && (cell.Terrain is HexTerrainType.DeepWater or HexTerrainType.ShallowWater or HexTerrainType.Lake))
                    detailCommands++;
            }
            else
            {
                if (_quality.DetailLevel <= 0)
                    continue;

                detailCommands++;
                if (_quality.WaterDetail >= 2
                    && (cell.Terrain is HexTerrainType.DeepWater or HexTerrainType.ShallowWater or HexTerrainType.Lake))
                    detailCommands++;
            }
        }
        return detailCommands;
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
            GenerationDebugMode.GeologicalRegion => ProvinceColor(cell.GeologicalRegionId),
            GenerationDebugMode.Macroplate => ProvinceColor(cell.MacroplateId),
            GenerationDebugMode.PlateBoundary => Ramp(cell.PlateBoundaryStrength),
            GenerationDebugMode.Elevation => Ramp((cell.ElevationMeters + 4000f) / 9000f),
            GenerationDebugMode.Slope => Ramp(cell.Slope / 1200f),
            GenerationDebugMode.CoastDistance => Ramp(Math.Clamp(cell.CoastDistance / 18f, 0f, 1f)),
            GenerationDebugMode.WaterDepth => cell.WaterDepthMeters > 0f
                ? new Color(0.05f, Math.Clamp(0.28f + cell.WaterDepthMeters / 6000f, 0.28f, 0.62f), 0.92f, 1f)
                : new Color(0.08f, 0.10f, 0.10f, 1f),
            GenerationDebugMode.FlowAccumulation => Ramp(MathF.Log10(1f + cell.FlowAccumulation) / 3.5f),
            GenerationDebugMode.StreamOrder => Ramp(cell.StreamOrder / 5f),
            GenerationDebugMode.RiverDirection => RiverDirectionColor(cell.RiverDirection),
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

    private static Color RiverDirectionColor(int direction)
    {
        return direction switch
        {
            0 => new Color(0.95f, 0.35f, 0.30f),
            1 => new Color(0.95f, 0.70f, 0.25f),
            2 => new Color(0.48f, 0.82f, 0.30f),
            3 => new Color(0.25f, 0.78f, 0.70f),
            4 => new Color(0.30f, 0.52f, 0.95f),
            5 => new Color(0.72f, 0.38f, 0.92f),
            _ => new Color(0.10f, 0.13f, 0.15f)
        };
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

    private static Color HighlandColor(WorldHexCell cell)
    {
        var baseColor = SurfaceClimateColor(cell);
        var lift = Math.Clamp((cell.ElevationMeters - 900f) / 1400f, 0f, 1f);
        var target = new Color(0.43f, 0.47f, 0.33f);
        return new Color(
            baseColor.R + (target.R - baseColor.R) * (0.32f + lift * 0.18f),
            baseColor.G + (target.G - baseColor.G) * (0.32f + lift * 0.18f),
            baseColor.B + (target.B - baseColor.B) * (0.32f + lift * 0.18f),
            1f);
    }

    private static Color DeepWaterColor(WorldHexCell cell)
    {
        var depth = Math.Clamp(cell.WaterDepthMeters / 9000f, 0f, 1f);
        depth *= depth * (3f - 2f * depth);
        return new Color(0.045f, 0.22f, 0.30f).Lerp(new Color(0.018f, 0.10f, 0.21f), depth);
    }

    private static Color TerrainColor(WorldHexCell cell, GenerationDebugMode debugMode)
    {
        if (debugMode != GenerationDebugMode.None)
            return DebugColor(cell, debugMode);

        if (cell.WaterKind == HexWaterKind.River)
            return new Color(0.070f, 0.420f, 0.455f);

        var baseColor = cell.Terrain switch
        {
            HexTerrainType.DeepWater => DeepWaterColor(cell),
            HexTerrainType.ShallowWater => new Color(0.055f, 0.255f, 0.300f),
            HexTerrainType.Lake => new Color(0.060f, 0.305f, 0.325f),
            HexTerrainType.River => new Color(0.070f, 0.420f, 0.455f),
            HexTerrainType.Sand => new Color(0.46f, 0.45f, 0.34f),
            HexTerrainType.Desert => SurfaceClimateColor(cell),
            HexTerrainType.Grassland => SurfaceClimateColor(cell),
            HexTerrainType.Highland => HighlandColor(cell),
            HexTerrainType.Rocky => new Color(0.335f, 0.365f, 0.315f),
            HexTerrainType.Mountain => new Color(0.285f, 0.315f, 0.300f),
            _ => new Color(0.23f, 0.38f, 0.22f)
        };

        if (cell.WaterKind == HexWaterKind.None)
        {
            var elevation = Math.Max(0f, cell.ElevationMeters);
            var highlandBlend = SmoothElevationBlend(250f, 1800f, elevation) * 0.14f;
            var alpineBlend = SmoothElevationBlend(1500f, 3600f, elevation) * 0.24f;
            var summitBlend = SmoothElevationBlend(3800f, 6500f, elevation) * 0.36f;
            baseColor = baseColor.Lerp(new Color(0.39f, 0.43f, 0.31f), highlandBlend);
            baseColor = baseColor.Lerp(new Color(0.50f, 0.45f, 0.37f), alpineBlend);
            baseColor = baseColor.Lerp(new Color(0.70f, 0.69f, 0.62f), summitBlend);
        }

        var variation = (cell.VisualVariation - 0.5f) * 0.035f;
        var elevationLight = cell.WaterKind is HexWaterKind.Ocean or HexWaterKind.Lake
            ? -Mathf.Clamp(cell.WaterDepth * 0.32f, 0f, 0.24f)
            : Mathf.Clamp(cell.Elevation * 0.20f, -0.06f, 0.18f);
        var factor = Mathf.Clamp(1f + variation + elevationLight, 0.68f, 1.22f);

        return new Color(
            Mathf.Clamp(baseColor.R * factor, 0f, 1f),
            Mathf.Clamp(baseColor.G * factor, 0f, 1f),
            Mathf.Clamp(baseColor.B * factor, 0f, 1f),
            1f);
    }

    private static float SmoothElevationBlend(float start, float end, float elevation)
    {
        var t = Math.Clamp((elevation - start) / (end - start), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
