using System;
using Evolit.Settings;
using Godot;

namespace Evolit.UI;

public static class UiMetrics
{
    public static float InterfaceScale { get; private set; } = 1f;
    public static float TextScale { get; private set; } = 1f;
    public static bool TooltipsEnabled { get; private set; } = true;

    public static void Configure(AppSettings settings)
    {
        InterfaceScale = settings.UiScale switch
        {
            0 => 0.90f,
            2 => 1.10f,
            3 => 1.25f,
            _ => 1.00f
        };

        TextScale = settings.TextSize switch
        {
            0 => 0.90f,
            2 => 1.15f,
            _ => 1.00f
        };

        TooltipsEnabled = settings.Tooltips;
    }

    public static int Font(int baseSize)
    {
        return Math.Max(8, (int)MathF.Round(baseSize * TextScale));
    }

    public static float Px(float value)
    {
        return value * InterfaceScale;
    }

    public static int Space(int value)
    {
        return Math.Max(1, (int)MathF.Round(value * InterfaceScale));
    }

    public static Vector2 Size(float x, float y)
    {
        return new Vector2(Px(x), Px(y));
    }
}
