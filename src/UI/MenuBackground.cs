using System;
using Godot;

namespace Evolit.UI;

public sealed partial class MenuBackground : Control
{
    private double _time;
    private Vector2 _parallax;
    private Vector2 _parallaxTarget;

    public float MotionScale { get; set; } = 1f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _time += delta;

        var viewportSize = GetViewportRect().Size;
        if (viewportSize.X > 1 && viewportSize.Y > 1)
        {
            var mouse = GetViewport().GetMousePosition();
            var normalized = new Vector2(
                mouse.X / viewportSize.X - 0.5f,
                mouse.Y / viewportSize.Y - 0.5f);

            _parallaxTarget = normalized * 18f * MotionScale;
            _parallax = _parallax.Lerp(_parallaxTarget, 1f - Mathf.Exp(-3.2f * (float)delta));
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var width = Size.X;
        var height = Size.Y;
        if (width <= 1f || height <= 1f)
            return;

        const int bands = 34;
        for (var i = 0; i < bands; i++)
        {
            var t = i / (float)(bands - 1);
            var color = EvolitPalette.DeepNavyTeal.Lerp(EvolitPalette.DeepWater, t * 0.72f);
            color = color.Lerp(new Color(0.075f, 0.185f, 0.205f), t * t * 0.35f);
            DrawRect(new Rect2(0f, height * t, width, height / bands + 2f), color);
        }

        var horizon = height * 0.58f;
        DrawMoon(width, height);
        DrawMist(width, height, horizon);
        DrawDistantLand(width, height, horizon);
        DrawWater(width, height, horizon);
        DrawForeground(width, height);
        DrawMotes(width, height);
    }

    private void DrawMoon(float width, float height)
    {
        var pulse = 0.5f + 0.5f * Mathf.Sin((float)_time * 0.55f);
        var center = new Vector2(width * 0.79f, height * 0.22f) - _parallax * 0.10f;

        DrawCircle(center, height * (0.115f + pulse * 0.006f), new Color(EvolitPalette.SoftAqua, 0.035f + pulse * 0.022f));
        DrawCircle(center, height * 0.082f, new Color(EvolitPalette.FogBlue, 0.055f + pulse * 0.018f));
        DrawCircle(center, height * 0.061f, new Color(EvolitPalette.MistWhite, 0.030f + pulse * 0.012f));
    }

    private void DrawMist(float width, float height, float horizon)
    {
        var drift = Mathf.Sin((float)_time * 0.15f) * width * 0.012f;
        DrawRect(new Rect2(drift - 20, horizon - height * 0.10f, width + 40, height * 0.035f), new Color(EvolitPalette.FogBlue, 0.050f));
        DrawRect(new Rect2(-drift - 20, horizon - height * 0.055f, width + 40, height * 0.025f), new Color(EvolitPalette.SoftAqua, 0.032f));
    }

    private void DrawDistantLand(float width, float height, float horizon)
    {
        var offset = _parallax * 0.22f;

        for (var i = 0; i < 14; i++)
        {
            var x = width * (0.42f + i * 0.055f) - offset.X;
            var radius = height * (0.07f + ((i * 37) % 5) * 0.012f);
            var y = horizon - radius * 0.25f - offset.Y * 0.35f;
            DrawCircle(new Vector2(x, y), radius, new Color(0.040f, 0.150f, 0.158f, 0.96f));
        }
    }

    private void DrawWater(float width, float height, float horizon)
    {
        DrawRect(new Rect2(0f, horizon, width, height - horizon), new Color(0.030f, 0.165f, 0.215f, 0.88f));
        DrawRect(new Rect2(0f, horizon, width, 2f), new Color(EvolitPalette.SoftAqua, 0.24f));

        var moonX = width * 0.79f;
        var shimmer = 0.5f + 0.5f * Mathf.Sin((float)_time * 0.62f);
        for (var i = 0; i < 17; i++)
        {
            var phase = (float)_time * (0.20f + i * 0.004f) + i * 0.73f;
            var y = horizon + 15f + i * height * 0.020f;
            var x = moonX - width * 0.10f + Mathf.Sin(phase) * (12f + i * 1.4f);
            var lineWidth = width * (0.025f + (i % 4) * 0.009f) * (1f - i / 28f);
            var alpha = 0.035f + shimmer * 0.025f;
            DrawLine(new Vector2(x, y), new Vector2(x + lineWidth, y), new Color(EvolitPalette.SoftAqua, alpha), 1.2f, true);
        }

        for (var i = 0; i < 11; i++)
        {
            var y = horizon + height * (0.055f + i * 0.031f);
            var wave = Mathf.Sin((float)_time * 0.32f + i * 0.9f) * width * 0.012f;
            var start = width * (0.43f + (i % 3) * 0.035f) + wave;
            DrawLine(
                new Vector2(start, y),
                new Vector2(start + width * (0.055f + (i % 4) * 0.018f), y),
                new Color(EvolitPalette.SoftAqua, 0.035f),
                1f,
                true);
        }
    }

    private void DrawForeground(float width, float height)
    {
        var offset = _parallax * 0.65f;

        DrawCircle(new Vector2(width * 0.93f, height * 0.86f) - offset, height * 0.28f, new Color(0.020f, 0.095f, 0.088f, 1f));
        DrawCircle(new Vector2(width * 0.79f, height * 0.94f) - offset, height * 0.24f, new Color(0.025f, 0.115f, 0.100f, 1f));
        DrawCircle(new Vector2(width * 0.04f, height * 0.96f) - offset, height * 0.22f, new Color(0.018f, 0.075f, 0.075f, 1f));

        // A few large pebbles to add depth without visual noise.
        DrawCircle(new Vector2(width * 0.70f, height * 0.87f) - offset * 0.8f, height * 0.037f, new Color(EvolitPalette.Slate, 0.16f));
        DrawCircle(new Vector2(width * 0.735f, height * 0.90f) - offset * 0.82f, height * 0.026f, new Color(EvolitPalette.Slate, 0.12f));
        DrawCircle(new Vector2(width * 0.18f, height * 0.91f) - offset * 0.76f, height * 0.031f, new Color(EvolitPalette.Slate, 0.11f));

        for (var i = 0; i < 9; i++)
        {
            var sway = Mathf.Sin((float)_time * 0.60f + i * 0.67f) * 5f;
            var basePoint = new Vector2(width * (0.755f + i * 0.026f), height * (0.88f + (i % 3) * 0.018f)) - offset;
            var stem = height * (0.055f + (i % 4) * 0.011f);
            var tip = basePoint - new Vector2((i % 2 == 0 ? 1f : -1f) * stem * 0.10f - sway, stem);
            var plantColor = new Color(EvolitPalette.YoungLeaf, 0.22f + (i % 3) * 0.028f);

            DrawLine(basePoint, tip, plantColor, 3f, true);
            DrawCircle(tip + new Vector2(-7f, 6f), 7f, plantColor);
            DrawCircle(tip + new Vector2(7f, 2f), 6f, plantColor);
        }
    }

    private void DrawMotes(float width, float height)
    {
        for (var i = 0; i < 20; i++)
        {
            var baseX = width * (((i * 137) % 997) / 997f);
            var baseY = height * (0.10f + (((i * 71) % 683) / 683f) * 0.52f);
            var x = baseX + Mathf.Sin((float)_time * 0.17f + i) * 9f;
            var y = baseY - (float)(_time * (2.0 + i % 3)) % (height * 0.05f);
            var radius = 1.1f + (i % 3) * 0.55f;
            DrawCircle(new Vector2(x, y), radius, new Color(EvolitPalette.SoftAqua, 0.045f + (i % 4) * 0.008f));
        }
    }
}
