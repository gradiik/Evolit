using Godot;

namespace Evolit.UI;

public sealed partial class MenuBackground : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var width = Size.X;
        var height = Size.Y;
        if (width <= 1f || height <= 1f) return;

        // Soft vertical atmospheric gradient.
        const int bands = 56;
        for (var i = 0; i < bands; i++)
        {
            var t = i / (float)(bands - 1);
            var color = EvolitPalette.DeepNavyTeal.Lerp(EvolitPalette.DeepWater, t * 0.72f);
            color = color.Lerp(new Color(0.075f, 0.185f, 0.205f), t * t * 0.35f);
            DrawRect(new Rect2(0f, height * t, width, height / bands + 2f), color);
        }

        var horizon = height * 0.58f;

        // Distant moon / soft light.
        DrawCircle(new Vector2(width * 0.79f, height * 0.22f), height * 0.105f, new Color(EvolitPalette.SoftAqua, 0.055f));
        DrawCircle(new Vector2(width * 0.79f, height * 0.22f), height * 0.072f, new Color(EvolitPalette.MistWhite, 0.045f));

        // Mist bands.
        DrawRect(new Rect2(0f, horizon - height * 0.10f, width, height * 0.035f), new Color(EvolitPalette.FogBlue, 0.055f));
        DrawRect(new Rect2(0f, horizon - height * 0.055f, width, height * 0.025f), new Color(EvolitPalette.SoftAqua, 0.035f));

        // Distant rounded land masses.
        for (var i = 0; i < 14; i++)
        {
            var x = width * (0.42f + i * 0.055f);
            var radius = height * (0.07f + ((i * 37) % 5) * 0.012f);
            var y = horizon - radius * 0.25f;
            DrawCircle(new Vector2(x, y), radius, new Color(0.040f, 0.150f, 0.158f, 0.96f));
        }

        // Water.
        DrawRect(new Rect2(0f, horizon, width, height - horizon), new Color(0.030f, 0.165f, 0.215f, 0.88f));
        DrawRect(new Rect2(0f, horizon, width, 2f), new Color(EvolitPalette.SoftAqua, 0.22f));

        // Soft water reflections.
        for (var i = 0; i < 18; i++)
        {
            var x = width * (0.46f + i * 0.028f);
            var reflectionWidth = width * (0.012f + (i % 3) * 0.004f);
            DrawRect(
                new Rect2(x, horizon + height * 0.02f + (i % 4) * 7f, reflectionWidth, height * 0.0025f),
                new Color(EvolitPalette.SoftAqua, 0.05f + (i % 4) * 0.012f));
        }

        // Foreground shoreline silhouettes.
        DrawCircle(new Vector2(width * 0.93f, height * 0.86f), height * 0.28f, new Color(0.020f, 0.095f, 0.088f, 1f));
        DrawCircle(new Vector2(width * 0.79f, height * 0.94f), height * 0.24f, new Color(0.025f, 0.115f, 0.100f, 1f));
        DrawCircle(new Vector2(width * 0.04f, height * 0.96f), height * 0.22f, new Color(0.018f, 0.075f, 0.075f, 1f));

        // Minimal plant silhouettes. Intentionally low-detail.
        for (var i = 0; i < 8; i++)
        {
            var basePoint = new Vector2(width * (0.76f + i * 0.027f), height * (0.88f + (i % 3) * 0.018f));
            var stem = height * (0.06f + (i % 4) * 0.012f);
            var tip = basePoint - new Vector2((i % 2 == 0 ? 1f : -1f) * stem * 0.12f, stem);
            var plantColor = new Color(EvolitPalette.YoungLeaf, 0.20f + (i % 3) * 0.025f);
            DrawLine(basePoint, tip, plantColor, 3f, true);
            DrawCircle(tip + new Vector2(-7f, 6f), 7f, plantColor);
            DrawCircle(tip + new Vector2(7f, 2f), 6f, plantColor);
        }

        // Sparse floating motes.
        for (var i = 0; i < 24; i++)
        {
            var px = width * (((i * 137) % 997) / 997f);
            var py = height * (0.12f + (((i * 71) % 683) / 683f) * 0.56f);
            var radius = 1.2f + (i % 3) * 0.65f;
            DrawCircle(new Vector2(px, py), radius, new Color(EvolitPalette.SoftAqua, 0.055f));
        }
    }
}
