using System;
using Evolit.Game;
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
    private float _cameraSpeed = 5f;
    private bool _smoothZoom = true;

    private static readonly Rect2 LandBounds = new(-720, -440, 1440, 880);

    public void Configure(DemoWorldDataProvider world, double cameraSpeed, bool smoothZoom)
    {
        _world = world;
        _cameraSpeed = (float)Math.Clamp(cameraSpeed, 1, 10);
        _smoothZoom = smoothZoom;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var direction = Input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
        if (direction.LengthSquared() > 0.001f)
        {
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
                QueueRedraw();
            }
        }
        else if (Math.Abs(_zoom - _targetZoom) > 0.0001f)
        {
            _zoom = _targetZoom;
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouse || !mouse.Pressed)
            return;

        if (mouse.ButtonIndex == MouseButton.WheelUp)
        {
            _targetZoom = Mathf.Clamp(_targetZoom * 1.12f, 0.55f, 2.25f);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.WheelDown)
        {
            _targetZoom = Mathf.Clamp(_targetZoom / 1.12f, 0.55f, 2.25f);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex != MouseButton.Left)
            return;

        var worldPoint = ScreenToWorld(mouse.Position);
        var hit = HitTest(worldPoint);
        SetSelected(hit);
        AcceptEvent();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.018f, 0.060f, 0.070f), true);

        var landTopLeft = WorldToScreen(LandBounds.Position);
        var landSize = LandBounds.Size * _zoom;
        var landScreen = new Rect2(landTopLeft, landSize);

        DrawRect(landScreen, new Color(0.235f, 0.355f, 0.205f), true);
        DrawRect(landScreen, new Color(EvolitPalette.YoungLeaf, 0.20f), false, 2f);

        // Cheap, fixed terrain variation. This is deliberately not a terrain generator.
        DrawCircle(WorldToScreen(new Vector2(-390, -190)), 180f * _zoom, new Color(0.29f, 0.41f, 0.24f, 0.28f));
        DrawCircle(WorldToScreen(new Vector2(360, 210)), 230f * _zoom, new Color(0.18f, 0.31f, 0.19f, 0.22f));
        DrawCircle(WorldToScreen(new Vector2(30, 120)), 150f * _zoom, new Color(0.38f, 0.46f, 0.24f, 0.13f));

        if (_world is null)
            return;

        foreach (var entity in _world.Entities)
            DrawEntity(entity);
    }

    private void DrawEntity(DemoEntity entity)
    {
        var center = WorldToScreen(entity.WorldPosition);
        var selected = ReferenceEquals(entity, _selected);

        if (selected)
        {
            DrawCircle(center, 27f * _zoom, new Color(EvolitPalette.EvolutionCyan, 0.10f));
            DrawArc(center, 24f * _zoom, 0, Mathf.Tau, 48, EvolitPalette.EvolutionCyan, 2f, true);
        }

        if (entity.Kind == DemoEntityKind.Creature)
        {
            var body = new[]
            {
                center + new Vector2(-18, 0) * _zoom,
                center + new Vector2(-8, -11) * _zoom,
                center + new Vector2(12, -9) * _zoom,
                center + new Vector2(21, 0) * _zoom,
                center + new Vector2(12, 9) * _zoom,
                center + new Vector2(-8, 11) * _zoom
            };
            DrawColoredPolygon(body, new Color(0.48f, 0.74f, 0.67f));
            DrawCircle(center + new Vector2(12, -2) * _zoom, Math.Max(1.8f, 2.5f * _zoom), EvolitPalette.DeepNavyTeal);
        }
        else
        {
            var stemColor = new Color(0.32f, 0.53f, 0.28f);
            DrawLine(center + new Vector2(0, 15) * _zoom, center + new Vector2(0, -12) * _zoom, stemColor, Math.Max(2f, 3f * _zoom), true);
            DrawCircle(center + new Vector2(-7, -8) * _zoom, 8f * _zoom, new Color(0.49f, 0.65f, 0.34f));
            DrawCircle(center + new Vector2(7, -13) * _zoom, 7f * _zoom, new Color(0.40f, 0.59f, 0.30f));
        }
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
            if (distance <= 32f && distance < bestDistance)
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
