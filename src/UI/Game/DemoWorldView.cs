using System;
using Evolit.Game;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class DemoWorldView : Control
{
    public event Action<DemoEntity?>? SelectionChanged;

    private DemoWorldDataProvider? _world;
    private DemoEntity? _selected;
    private Vector2 _cameraPosition;
    private float _zoom = 1f;
    private float _targetZoom = 1f;
    private const float MinZoom = 0.45f;
    private const float MaxZoom = 3.0f;

    private float _cameraSpeed = 5f;
    private bool _smoothZoom = true;
    private GraphicsQualityProfile _quality = GraphicsQualityProfile.From(AppSettings.Default());
    private bool _panning;
    private MouseButton _panButton;
    private Vector2 _zoomAnchorScreen;
    private Vector2 _zoomAnchorWorld;
    private bool _hasZoomAnchor;

    private static readonly Rect2 LandBounds = new(-720, -440, 1440, 880);

    public void Configure(DemoWorldDataProvider world, double cameraSpeed, bool smoothZoom, GraphicsQualityProfile quality)
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
        Resized += QueueRedraw;

        if (_world is not null)
            _world.DataChanged += QueueRedraw;

        BuildCameraControls();
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.DataChanged -= QueueRedraw;
    }

    public override void _Process(double delta)
    {
        var direction = Input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
        if (direction.LengthSquared() > 0.001f)
        {
            _hasZoomAnchor = false;
            var worldUnitsPerSecond = 90f + _cameraSpeed * 42f;
            _cameraPosition += direction * worldUnitsPerSecond * (float)delta / Math.Max(_zoom, 0.1f);
            ClampCamera();
            QueueRedraw();
        }

        if (_smoothZoom)
        {
            var next = Mathf.Lerp(_zoom, _targetZoom, 1f - MathF.Exp(-10f * (float)delta));
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
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.014f, 0.052f, 0.060f), true);

        var landTopLeft = WorldToScreen(LandBounds.Position);
        var landSize = LandBounds.Size * _zoom;
        var landScreen = new Rect2(landTopLeft, landSize);

        DrawRect(landScreen, new Color(0.235f, 0.355f, 0.205f), true);
        DrawRect(landScreen, new Color(EvolitPalette.YoungLeaf, 0.20f), false, 2f);

        DrawDecorativePatches();

        if (_world is null)
            return;

        foreach (var entity in _world.Entities)
            DrawEntity(entity);
    }

    private void DrawDecorativePatches()
    {
        DrawOrganicPatch(new Vector2(-390, -190), 205f, 0.22f, new Color(0.29f, 0.41f, 0.24f, 0.24f));
        DrawOrganicPatch(new Vector2(360, 210), 250f, 1.90f, new Color(0.18f, 0.31f, 0.19f, 0.19f));

        if (_quality.DecorativePatchCount >= 4)
        {
            DrawOrganicPatch(new Vector2(30, 120), 165f, 3.40f, new Color(0.38f, 0.46f, 0.24f, 0.11f));
            DrawOrganicPatch(new Vector2(420, -240), 120f, 5.10f, new Color(0.31f, 0.39f, 0.22f, 0.10f));
        }

        if (_quality.DecorativePatchCount >= 6)
        {
            DrawOrganicPatch(new Vector2(-520, 255), 118f, 2.70f, new Color(0.24f, 0.38f, 0.20f, 0.11f));
            DrawOrganicPatch(new Vector2(175, -285), 105f, 4.55f, new Color(0.34f, 0.43f, 0.23f, 0.10f));
        }
    }

    private void DrawOrganicPatch(Vector2 center, float radius, float phase, Color color)
    {
        const int pointCount = 22;
        var points = new Vector2[pointCount];

        for (var i = 0; i < pointCount; i++)
        {
            var angle = Mathf.Tau * i / pointCount;
            var wobble = 0.82f
                + 0.10f * Mathf.Sin(angle * 3f + phase)
                + 0.06f * Mathf.Sin(angle * 5f - phase * 0.7f);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            points[i] = WorldToScreen(center + direction * radius * wobble);
        }

        DrawColoredPolygon(points, color);
    }

    private void DrawEntity(DemoEntity entity)
    {
        var center = WorldToScreen(entity.WorldPosition);
        var selected = ReferenceEquals(entity, _selected);

        if (selected)
        {
            DrawCircle(center, 42f * _zoom, new Color(EvolitPalette.EvolutionCyan, 0.11f));
            DrawArc(center, 35f * _zoom, 0, Mathf.Tau, _quality.SelectionArcSegments, EvolitPalette.EvolutionCyan, 2.5f, true);
            DrawCircle(center, 29f * _zoom, new Color(EvolitPalette.EvolutionCyan, 0.035f));
        }

        if (entity.Kind == DemoEntityKind.Creature)
        {
            var body = new[]
            {
                center + new Vector2(-28, 0) * _zoom,
                center + new Vector2(-13, -16) * _zoom,
                center + new Vector2(16, -13) * _zoom,
                center + new Vector2(31, 0) * _zoom,
                center + new Vector2(16, 13) * _zoom,
                center + new Vector2(-13, 16) * _zoom
            };
            DrawColoredPolygon(body, new Color(0.48f, 0.74f, 0.67f));
            DrawLine(center + new Vector2(-24, -4) * _zoom, center + new Vector2(-35, -10) * _zoom, new Color(0.40f, 0.64f, 0.59f), Math.Max(2f, 3f * _zoom), true);
            DrawLine(center + new Vector2(-24, 4) * _zoom, center + new Vector2(-35, 10) * _zoom, new Color(0.40f, 0.64f, 0.59f), Math.Max(2f, 3f * _zoom), true);
            DrawCircle(center + new Vector2(17, -3) * _zoom, Math.Max(2.2f, 3.2f * _zoom), EvolitPalette.DeepNavyTeal);

            if (_quality.ExtraEntityDetails)
            {
                DrawLine(center + new Vector2(-4, -14) * _zoom, center + new Vector2(4, -22) * _zoom, new Color(0.58f, 0.82f, 0.74f, 0.72f), Math.Max(1f, 1.6f * _zoom), true);
                DrawLine(center + new Vector2(3, 14) * _zoom, center + new Vector2(9, 21) * _zoom, new Color(0.58f, 0.82f, 0.74f, 0.62f), Math.Max(1f, 1.4f * _zoom), true);
            }
        }
        else
        {
            var stemColor = new Color(0.32f, 0.53f, 0.28f);
            DrawLine(center + new Vector2(0, 23) * _zoom, center + new Vector2(0, -18) * _zoom, stemColor, Math.Max(2.5f, 4f * _zoom), true);
            DrawCircle(center + new Vector2(-12, -10) * _zoom, 12f * _zoom, new Color(0.53f, 0.67f, 0.32f));
            DrawCircle(center + new Vector2(12, -17) * _zoom, 11f * _zoom, new Color(0.43f, 0.61f, 0.29f));
            DrawCircle(center + new Vector2(3, -25) * _zoom, 8f * _zoom, new Color(0.59f, 0.70f, 0.36f));

            if (_quality.ExtraEntityDetails)
            {
                DrawCircle(center + new Vector2(-17, -24) * _zoom, 6f * _zoom, new Color(0.49f, 0.66f, 0.31f, 0.90f));
                DrawCircle(center + new Vector2(16, -5) * _zoom, 5f * _zoom, new Color(0.57f, 0.71f, 0.36f, 0.82f));
            }
        }
    }

    private void BuildCameraControls()
    {
        var panel = new PanelContainer
        {
            AnchorLeft = 0.02f,
            AnchorRight = 0.25f,
            AnchorTop = 0.105f,
            AnchorBottom = 0.165f,
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.90f));
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 7);
        margin.AddThemeConstantOverride("margin_right", 7);
        margin.AddThemeConstantOverride("margin_top", 5);
        margin.AddThemeConstantOverride("margin_bottom", 5);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        margin.AddChild(row);

        var label = new Label { Text = "Камера" };
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(label);

        row.AddChild(CameraButton("−", "Отдалить", () => ZoomAt(Size * 0.5f, 1f / 1.14f)));
        row.AddChild(CameraButton("+", "Приблизить", () => ZoomAt(Size * 0.5f, 1.14f)));
        row.AddChild(CameraButton("Центр", "Вернуть камеру к центру мира", CenterCamera));
        row.AddChild(CameraButton("Сброс", "Сбросить положение и масштаб камеры", ResetCamera));
    }

    private static Button CameraButton(string text, string tooltip, Action action)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(text.Length > 2 ? 58 : 34, 30)
        };
        button.Pressed += action;
        return button;
    }

    private void ZoomAt(Vector2 screenPoint, float factor)
    {
        _zoomAnchorScreen = screenPoint;
        _zoomAnchorWorld = ScreenToWorld(screenPoint);
        _hasZoomAnchor = true;
        _targetZoom = Mathf.Clamp(_targetZoom * factor, MinZoom, MaxZoom);

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
            - (_zoomAnchorScreen - Size * 0.5f) / Math.Max(_zoom, 0.01f);
        ClampCamera();
    }

    private void CenterCamera()
    {
        _cameraPosition = Vector2.Zero;
        _hasZoomAnchor = false;
        QueueRedraw();
    }

    private void ResetCamera()
    {
        _cameraPosition = Vector2.Zero;
        _zoom = 1f;
        _targetZoom = 1f;
        _hasZoomAnchor = false;
        QueueRedraw();
    }

    private DemoEntity? HitTest(Vector2 worldPoint)
    {
        if (_world is null)
            return null;

        DemoEntity? best = null;
        var bestDistance = float.MaxValue;

        foreach (var entity in _world.Entities)
        {
            var distance = worldPoint.DistanceTo(entity.WorldPosition);
            if (distance <= 48f && distance < bestDistance)
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
        return (screen - Size * 0.5f) / Math.Max(_zoom, 0.01f) + _cameraPosition;
    }

    private void ClampCamera()
    {
        _cameraPosition.X = Mathf.Clamp(_cameraPosition.X, LandBounds.Position.X - 360, LandBounds.End.X + 360);
        _cameraPosition.Y = Mathf.Clamp(_cameraPosition.Y, LandBounds.Position.Y - 260, LandBounds.End.Y + 260);
    }
}
