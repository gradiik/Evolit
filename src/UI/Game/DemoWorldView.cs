using System;
using Evolit.Game;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class DemoWorldView : Control
{
    public event Action<DemoEntity?>? SelectionChanged;

    private const float MinZoom = 0.10f;
    private const float MaxZoom = 3.0f;

    private static readonly Vector2[] UnitHexPoints =
    [
        new(0.8660254f, 0.5f),
        new(0f, 1f),
        new(-0.8660254f, 0.5f),
        new(-0.8660254f, -0.5f),
        new(0f, -1f),
        new(0.8660254f, -0.5f)
    ];

    private readonly Vector2[] _hexPoints = new Vector2[6];
    private readonly Vector2[] _hexOutline = new Vector2[7];

    private DemoWorldDataProvider? _world;
    private DemoEntity? _selected;
    private Vector2 _cameraPosition;
    private float _zoom = 0.6f;
    private float _targetZoom = 0.6f;

    private float _cameraSpeed = 5f;
    private bool _smoothZoom = true;
    private GraphicsQualityProfile _quality = GraphicsQualityProfile.From(AppSettings.Default());
    private bool _panning;
    private MouseButton _panButton;
    private Vector2 _zoomAnchorScreen;
    private Vector2 _zoomAnchorWorld;
    private bool _hasZoomAnchor;

    private PanelContainer? _inspectorPanel;
    private Label? _inspectorLabel;
    private WorldHexCell? _inspectedCell;
    private bool _inspectorActive;

    public void Configure(
        DemoWorldDataProvider world,
        double cameraSpeed,
        bool smoothZoom,
        GraphicsQualityProfile quality)
    {
        _world = world;
        _cameraSpeed = (float)Math.Clamp(cameraSpeed, 1, 10);
        _smoothZoom = smoothZoom;
        _quality = quality;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        Resized += HandleResized;

        if (_world is not null)
            _world.DataChanged += QueueRedraw;

        BuildInspectorPanel();
        ApplyFitView();
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.DataChanged -= QueueRedraw;
    }

    public override void _Process(double delta)
    {
        var direction = Input.GetVector(
            "camera_left",
            "camera_right",
            "camera_up",
            "camera_down");

        if (direction.LengthSquared() > 0.001f)
        {
            _hasZoomAnchor = false;
            var worldUnitsPerSecond = 100f + _cameraSpeed * 48f;
            _cameraPosition += direction
                * worldUnitsPerSecond
                * (float)delta
                / Math.Max(_zoom, 0.1f);
            ClampCamera();
            QueueRedraw();
        }

        if (_smoothZoom)
        {
            var next = Mathf.Lerp(
                _zoom,
                _targetZoom,
                1f - MathF.Exp(-10f * (float)delta));

            if (Math.Abs(next - _zoom) > 0.0005f)
            {
                _zoom = next;
                ApplyZoomAnchor();
                QueueRedraw();
            }
            else if (Math.Abs(_zoom - _targetZoom) <= 0.001f)
            {
                _zoom = _targetZoom;
                ApplyZoomAnchor();
                _hasZoomAnchor = false;
            }
        }
        else if (Math.Abs(_zoom - _targetZoom) > 0.0001f)
        {
            _zoom = _targetZoom;
            ApplyZoomAnchor();
            _hasZoomAnchor = false;
            QueueRedraw();
        }

        UpdateInspector();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && _panning)
        {
            _cameraPosition -= motion.Relative / Math.Max(_zoom, 0.01f);
            ClampCamera();
            QueueRedraw();
            AcceptEvent();
            return;
        }

        if (@event is not InputEventMouseButton mouse)
            return;

        if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed)
        {
            ZoomAt(mouse.Position, 1.14f);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed)
        {
            ZoomAt(mouse.Position, 1f / 1.14f);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex is MouseButton.Middle or MouseButton.Right)
        {
            if (mouse.Pressed)
            {
                _hasZoomAnchor = false;
                _panning = true;
                _panButton = mouse.ButtonIndex;
            }
            else if (_panning && mouse.ButtonIndex == _panButton)
            {
                _panning = false;
            }

            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed)
        {
            var worldPoint = ScreenToWorld(mouse.Position);
            SetSelected(HitTest(worldPoint));
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        DrawRect(
            new Rect2(Vector2.Zero, Size),
            new Color(0.010f, 0.040f, 0.047f),
            true);

        if (_world is null)
            return;

        DrawWorldMap(_world.Map);

        foreach (var entity in _world.Entities)
            DrawEntity(entity);

        DrawInspectorHighlight();
    }

    private void DrawWorldMap(WorldMap map)
    {
        var radius = map.HexSize * _zoom;
        var cullMargin = radius * 1.7f;
        var viewport = new Rect2(
            -cullMargin,
            -cullMargin,
            Size.X + cullMargin * 2f,
            Size.Y + cullMargin * 2f);

        var worldTopLeft = ScreenToWorld(
            new Vector2(-cullMargin, -cullMargin));
        var worldBottomRight = ScreenToWorld(
            new Vector2(Size.X + cullMargin, Size.Y + cullMargin));
        var worldRect = new Rect2(
            new Vector2(
                Math.Min(worldTopLeft.X, worldBottomRight.X),
                Math.Min(worldTopLeft.Y, worldBottomRight.Y)),
            new Vector2(
                Math.Abs(worldBottomRight.X - worldTopLeft.X),
                Math.Abs(worldBottomRight.Y - worldTopLeft.Y)));

        foreach (var cell in map.GetCellsInWorldRect(worldRect, 2))
        {
            var center = WorldToScreen(cell.WorldCenter);
            if (!viewport.HasPoint(center))
                continue;

            FillHexPoints(center, radius, _hexPoints);
            DrawColoredPolygon(_hexPoints, TerrainColor(cell));

            if (_quality.DetailLevel >= 1 && _zoom >= 0.34f)
            {
                for (var i = 0; i < 6; i++)
                    _hexOutline[i] = _hexPoints[i];
                _hexOutline[6] = _hexPoints[0];

                DrawPolyline(
                    _hexOutline,
                    new Color(0.02f, 0.09f, 0.10f, 0.30f),
                    Math.Max(0.6f, _zoom * 0.75f),
                    true);
            }

            DrawTerrainDetail(cell, center, radius);
        }
    }

    private void DrawTerrainDetail(
        WorldHexCell cell,
        Vector2 center,
        float radius)
    {
        if (cell.Terrain == HexTerrainType.River && radius >= 3f)
        {
            DrawLine(
                center + new Vector2(-radius * 0.55f, radius * 0.24f),
                center + new Vector2(radius * 0.58f, -radius * 0.22f),
                new Color(0.48f, 0.88f, 0.86f, 0.64f),
                Math.Max(1.2f, radius * 0.18f),
                true);
        }

        if (_quality.DetailLevel == 0 || radius < 8f)
            return;

        switch (cell.Terrain)
        {
            case HexTerrainType.Mountain:
            {
                FillHexPoints(center, radius * 0.62f, _hexPoints);
                DrawColoredPolygon(
                    _hexPoints,
                    new Color(0.46f, 0.49f, 0.45f, 0.26f));
                DrawLine(
                    center + new Vector2(-radius * 0.34f, radius * 0.18f),
                    center + new Vector2(0, -radius * 0.42f),
                    new Color(0.70f, 0.75f, 0.70f, 0.34f),
                    Math.Max(1f, radius * 0.07f),
                    true);
                DrawLine(
                    center + new Vector2(0, -radius * 0.42f),
                    center + new Vector2(radius * 0.34f, radius * 0.22f),
                    new Color(0.10f, 0.16f, 0.15f, 0.42f),
                    Math.Max(1f, radius * 0.08f),
                    true);
                break;
            }
            case HexTerrainType.Rocky:
            {
                var offset = new Vector2(
                    (cell.VisualVariation - 0.5f) * radius * 0.22f,
                    (0.5f - cell.VisualVariation) * radius * 0.14f);
                DrawCircle(
                    center + offset,
                    Math.Max(1.5f, radius * 0.10f),
                    new Color(0.62f, 0.63f, 0.55f, 0.24f));
                DrawLine(
                    center + new Vector2(-radius * 0.34f, radius * 0.28f),
                    center + new Vector2(radius * 0.28f, -radius * 0.22f),
                    new Color(0.72f, 0.74f, 0.64f, 0.12f),
                    Math.Max(0.8f, radius * 0.04f),
                    true);
                break;
            }
            case HexTerrainType.DeepWater:
            case HexTerrainType.ShallowWater:
            case HexTerrainType.Lake:
            {
                if (_quality.WaterDetail == 0)
                    break;

                DrawArc(
                    center,
                    radius * 0.52f,
                    -0.7f,
                    1.7f,
                    14,
                    new Color(0.45f, 0.78f, 0.80f, 0.10f),
                    Math.Max(0.7f, _zoom),
                    true);

                if (_quality.WaterDetail >= 2)
                {
                    DrawArc(
                        center + new Vector2(radius * 0.08f, -radius * 0.06f),
                        radius * 0.34f,
                        2.1f,
                        4.8f,
                        10,
                        new Color(0.62f, 0.90f, 0.88f, 0.08f),
                        Math.Max(0.6f, _zoom * 0.8f),
                        true);
                }
                break;
            }
        }
    }

    private static Color TerrainColor(WorldHexCell cell)
    {
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
            ? -Mathf.Clamp(cell.WaterDepth * 0.36f, 0f, 0.27f)
            : Mathf.Clamp((cell.Elevation - 0.05f) * 0.28f, -0.07f, 0.22f);
        var factor = Mathf.Clamp(1f + variation + elevationLight, 0.68f, 1.22f);

        return new Color(
            Mathf.Clamp(baseColor.R * factor, 0f, 1f),
            Mathf.Clamp(baseColor.G * factor, 0f, 1f),
            Mathf.Clamp(baseColor.B * factor, 0f, 1f),
            1f);
    }

    private static void FillHexPoints(
        Vector2 center,
        float radius,
        Vector2[] target)
    {
        for (var i = 0; i < 6; i++)
            target[i] = center + UnitHexPoints[i] * radius;
    }

    private void DrawEntity(DemoEntity entity)
    {
        var center = WorldToScreen(entity.WorldPosition);
        var selected = ReferenceEquals(entity, _selected);
        var visualScale = Mathf.Clamp(_zoom, 0.38f, 2.5f);

        if (selected)
        {
            DrawCircle(
                center,
                42f * visualScale,
                new Color(EvolitPalette.EvolutionCyan, 0.11f));
            DrawArc(
                center,
                35f * visualScale,
                0,
                Mathf.Tau,
                _quality.SelectionArcSegments,
                EvolitPalette.EvolutionCyan,
                2.5f,
                true);
            DrawCircle(
                center,
                29f * visualScale,
                new Color(EvolitPalette.EvolutionCyan, 0.035f));
        }

        if (entity.Kind == DemoEntityKind.Creature)
        {
            var body = new[]
            {
                center + new Vector2(-28, 0) * visualScale,
                center + new Vector2(-13, -16) * visualScale,
                center + new Vector2(16, -13) * visualScale,
                center + new Vector2(31, 0) * visualScale,
                center + new Vector2(16, 13) * visualScale,
                center + new Vector2(-13, 16) * visualScale
            };

            DrawColoredPolygon(body, new Color(0.48f, 0.74f, 0.67f));
            DrawLine(
                center + new Vector2(-24, -4) * visualScale,
                center + new Vector2(-35, -10) * visualScale,
                new Color(0.40f, 0.64f, 0.59f),
                Math.Max(2f, 3f * visualScale),
                true);
            DrawLine(
                center + new Vector2(-24, 4) * visualScale,
                center + new Vector2(-35, 10) * visualScale,
                new Color(0.40f, 0.64f, 0.59f),
                Math.Max(2f, 3f * visualScale),
                true);
            DrawCircle(
                center + new Vector2(17, -3) * visualScale,
                Math.Max(2.2f, 3.2f * visualScale),
                EvolitPalette.DeepNavyTeal);

            if (_quality.ExtraEntityDetails)
            {
                DrawLine(
                    center + new Vector2(-4, -14) * visualScale,
                    center + new Vector2(4, -22) * visualScale,
                    new Color(0.58f, 0.82f, 0.74f, 0.72f),
                    Math.Max(1f, 1.6f * visualScale),
                    true);
            }
        }
        else
        {
            var stemColor = new Color(0.32f, 0.53f, 0.28f);
            DrawLine(
                center + new Vector2(0, 23) * visualScale,
                center + new Vector2(0, -18) * visualScale,
                stemColor,
                Math.Max(2.5f, 4f * visualScale),
                true);
            DrawCircle(
                center + new Vector2(-12, -10) * visualScale,
                12f * visualScale,
                new Color(0.53f, 0.67f, 0.32f));
            DrawCircle(
                center + new Vector2(12, -17) * visualScale,
                11f * visualScale,
                new Color(0.43f, 0.61f, 0.29f));
            DrawCircle(
                center + new Vector2(3, -25) * visualScale,
                8f * visualScale,
                new Color(0.59f, 0.70f, 0.36f));
        }
    }


    private void BuildInspectorPanel()
    {
        _inspectorPanel = new PanelContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 15,
            CustomMinimumSize = UiMetrics.Size(268, 0)
        };
        _inspectorPanel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.98f));
        AddChild(_inspectorPanel);

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 9);
        margin.AddThemeConstantOverride("margin_bottom", 9);
        _inspectorPanel.AddChild(margin);

        _inspectorLabel = new Label
        {
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _inspectorLabel.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        _inspectorLabel.AddThemeColorOverride(
            "font_color",
            EvolitPalette.MistWhite);
        margin.AddChild(_inspectorLabel);
    }

    private void UpdateInspector()
    {
        if (_world is null || _inspectorPanel is null)
            return;

        var active = Input.IsActionPressed("terrain_inspect");
        var mouse = GetLocalMousePosition();
        var inside = new Rect2(Vector2.Zero, Size).HasPoint(mouse);
        var nextCell = active && inside
            ? _world.Map.GetCellAtWorld(ScreenToWorld(mouse))
            : null;

        if (!active || nextCell is null)
        {
            if (_inspectorActive || _inspectedCell is not null)
            {
                _inspectorActive = false;
                _inspectedCell = null;
                _inspectorPanel.Visible = false;
                QueueRedraw();
            }
            return;
        }

        _inspectorActive = true;
        _inspectorPanel.Visible = true;
        _inspectorPanel.Position = new Vector2(
            Math.Max(8f, Math.Min(Size.X - 284f, mouse.X + 18f)),
            Math.Max(8f, Math.Min(Size.Y - 176f, mouse.Y + 18f)));

        if (!ReferenceEquals(_inspectedCell, nextCell))
        {
            _inspectedCell = nextCell;
            if (_inspectorLabel is not null)
                _inspectorLabel.Text = FormatInspectorText(nextCell);
            QueueRedraw();
        }
    }

    private void DrawInspectorHighlight()
    {
        if (!_inspectorActive || _inspectedCell is null)
            return;

        var center = WorldToScreen(_inspectedCell.WorldCenter);
        var radius = _world?.Map.HexSize * _zoom ?? 0f;
        if (radius <= 0f)
            return;

        FillHexPoints(center, radius * 0.96f, _hexPoints);
        DrawColoredPolygon(
            _hexPoints,
            new Color(EvolitPalette.EvolutionCyan, 0.08f));

        for (var i = 0; i < 6; i++)
            _hexOutline[i] = _hexPoints[i];
        _hexOutline[6] = _hexPoints[0];

        DrawPolyline(
            _hexOutline,
            EvolitPalette.EvolutionCyan,
            Math.Max(1.5f, 2.2f * _zoom),
            true);
    }

    private static string FormatInspectorText(WorldHexCell cell)
    {
        var location = $"Гекс {cell.Coord.Q}:{cell.Coord.R}";
        var terrain = $"Тип: {TerrainLabel(cell.Terrain)}";
        var vertical = cell.IsWater
            ? $"Глубина: {cell.WaterDepthMeters:0} м"
            : $"Высота: {cell.ElevationMeters:0} м";

        return
            $"{location}\n" +
            $"{terrain}\n" +
            $"{vertical}\n" +
            $"Температура: {cell.TemperatureCelsius:0.0} °C\n" +
            $"Влажность: {cell.Humidity * 100f:0}%\n" +
            $"Давление: {cell.PressureKPa:0.0} кПа\n" +
            $"Движение: ×{cell.MovementSpeedMultiplier:0.00} " +
            $"(стоимость {cell.MovementCost:0.00})";
    }

    private static string TerrainLabel(HexTerrainType terrain)
    {
        return terrain switch
        {
            HexTerrainType.DeepWater => "Глубокий океан",
            HexTerrainType.ShallowWater => "Мелководье",
            HexTerrainType.Lake => "Озеро",
            HexTerrainType.River => "Река",
            HexTerrainType.Sand => "Песчаный берег",
            HexTerrainType.Desert => "Пустыня",
            HexTerrainType.Grassland => "Равнина",
            HexTerrainType.Rocky => "Каменистая возвышенность",
            HexTerrainType.Mountain => "Горы",
            _ => terrain.ToString()
        };
    }

    private void ZoomAt(Vector2 screenPoint, float factor)
    {
        _zoomAnchorScreen = screenPoint;
        _zoomAnchorWorld = ScreenToWorld(screenPoint);
        _hasZoomAnchor = true;
        _targetZoom = Mathf.Clamp(
            _targetZoom * factor,
            MinZoom,
            MaxZoom);

        if (!_smoothZoom)
        {
            _zoom = _targetZoom;
            ApplyZoomAnchor();
            _hasZoomAnchor = false;
            QueueRedraw();
        }
    }

    private void ApplyZoomAnchor()
    {
        if (!_hasZoomAnchor)
            return;

        _cameraPosition = _zoomAnchorWorld
            - (_zoomAnchorScreen - Size * 0.5f)
            / Math.Max(_zoom, 0.01f);

        ClampCamera();
    }

    public void ZoomIn()
    {
        ZoomAt(Size * 0.5f, 1.14f);
    }

    public void ZoomOut()
    {
        ZoomAt(Size * 0.5f, 1f / 1.14f);
    }

    public void CenterCamera()
    {
        if (_world is null)
            return;

        _cameraPosition = _world.Map.Bounds.GetCenter();
        _hasZoomAnchor = false;
        QueueRedraw();
    }

    public void ResetCamera()
    {
        ApplyFitView();
        QueueRedraw();
    }

    private void ApplyFitView()
    {
        if (_world is null || Size.X <= 1 || Size.Y <= 1)
            return;

        var bounds = _world.Map.Bounds;
        var availableWidth = Math.Max(1f, Size.X * 0.92f);
        var availableHeight = Math.Max(1f, Size.Y * 0.72f);
        var fit = Math.Min(
            availableWidth / Math.Max(1f, bounds.Size.X),
            availableHeight / Math.Max(1f, bounds.Size.Y));

        _cameraPosition = bounds.GetCenter();
        _zoom = Mathf.Clamp(fit, MinZoom, 1.05f);
        _targetZoom = _zoom;
        _hasZoomAnchor = false;
        ClampCamera();
    }

    private DemoEntity? HitTest(Vector2 worldPoint)
    {
        if (_world is null)
            return null;

        DemoEntity? best = null;
        var bestDistance = float.MaxValue;
        var radius = 48f / Math.Max(_zoom, 0.4f);

        foreach (var entity in _world.Entities)
        {
            var distance = worldPoint.DistanceTo(entity.WorldPosition);
            if (distance <= radius && distance < bestDistance)
            {
                best = entity;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void SetSelected(DemoEntity? entity)
    {
        if (ReferenceEquals(_selected, entity))
            return;

        _selected = entity;
        QueueRedraw();
        SelectionChanged?.Invoke(entity);
    }

    private Vector2 WorldToScreen(Vector2 world)
    {
        return (world - _cameraPosition) * _zoom + Size * 0.5f;
    }

    private Vector2 ScreenToWorld(Vector2 screen)
    {
        return (screen - Size * 0.5f)
            / Math.Max(_zoom, 0.01f)
            + _cameraPosition;
    }

    private void ClampCamera()
    {
        if (_world is null)
            return;

        var bounds = _world.Map.Bounds;
        var padding = _world.Map.HexSize * 5f;

        _cameraPosition.X = Mathf.Clamp(
            _cameraPosition.X,
            bounds.Position.X - padding,
            bounds.End.X + padding);
        _cameraPosition.Y = Mathf.Clamp(
            _cameraPosition.Y,
            bounds.Position.Y - padding,
            bounds.End.Y + padding);
    }

    private void HandleResized()
    {
        ClampCamera();
        QueueRedraw();
    }
}
