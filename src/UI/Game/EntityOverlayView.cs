using System;
using Evolit.Game;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

internal sealed partial class EntityOverlayView : Control
{
    private static readonly Vector2[] CreatureShape =
    [
        new Vector2(-28, 0),
        new Vector2(-13, -16),
        new Vector2(16, -13),
        new Vector2(31, 0),
        new Vector2(16, 13),
        new Vector2(-13, 16)
    ];

    private DemoWorldDataProvider? _world;
    private GraphicsQualityProfile _quality = GraphicsQualityProfile.From(AppSettings.Default());
    private readonly Vector2[] _creatureBody = new Vector2[CreatureShape.Length];
    private Vector2 _cameraPosition;
    private float _zoom = 1f;
    private Vector2 _viewportSize;
    private DemoEntity? _selected;

    public void Configure(DemoWorldDataProvider world, GraphicsQualityProfile quality)
    {
        _world = world;
        _quality = quality;
        QueueRedraw();
    }

    public void SetView(Vector2 cameraPosition, float zoom, Vector2 viewportSize, DemoEntity? selected)
    {
        _cameraPosition = cameraPosition;
        _zoom = zoom;
        _viewportSize = viewportSize;
        _selected = selected;
        QueueRedraw();
    }

    public void Refresh(DemoEntity? selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_world is null)
            return;

        foreach (var entity in _world.Entities)
            DrawEntity(entity);
    }

    private void DrawEntity(DemoEntity entity)
    {
        var center = (entity.WorldPosition - _cameraPosition) * _zoom + _viewportSize * 0.5f;
        var selected = ReferenceEquals(entity, _selected);
        var visualScale = Mathf.Clamp(_zoom, 0.38f, 2.5f);

        if (selected)
        {
            DrawCircle(center, 42f * visualScale, new Color(EvolitPalette.EvolutionCyan, 0.11f));
            DrawArc(
                center,
                35f * visualScale,
                0,
                Mathf.Tau,
                _quality.SelectionArcSegments,
                EvolitPalette.EvolutionCyan,
                2.5f,
                true);
            DrawCircle(center, 29f * visualScale, new Color(EvolitPalette.EvolutionCyan, 0.035f));
        }

        if (entity.Kind == DemoEntityKind.Creature)
        {
            for (var i = 0; i < CreatureShape.Length; i++)
                _creatureBody[i] = center + CreatureShape[i] * visualScale;

            DrawColoredPolygon(_creatureBody, new Color(0.48f, 0.74f, 0.67f));
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
            DrawCircle(center + new Vector2(-12, -10) * visualScale, 12f * visualScale, new Color(0.53f, 0.67f, 0.32f));
            DrawCircle(center + new Vector2(12, -17) * visualScale, 11f * visualScale, new Color(0.43f, 0.61f, 0.29f));
            DrawCircle(center + new Vector2(3, -25) * visualScale, 8f * visualScale, new Color(0.59f, 0.70f, 0.36f));
        }
    }
}
