using Evolit.Settings;
using Godot;

namespace Evolit.UI;

public static class NatureTechTheme
{
    public static Theme Create(AppSettings? settings = null)
    {
        if (settings is not null)
            UiMetrics.Configure(settings);

        var theme = new Theme();

        theme.SetColor("font_color", "Label", EvolitPalette.MistWhite);
        theme.SetColor("font_color", "Button", EvolitPalette.MistWhite);
        theme.SetColor("font_hover_color", "Button", EvolitPalette.MistWhite);
        theme.SetColor("font_pressed_color", "Button", EvolitPalette.DeepNavyTeal);
        theme.SetColor("font_disabled_color", "Button", EvolitPalette.Disabled);
        theme.SetColor("font_color", "CheckButton", EvolitPalette.MistWhite);
        theme.SetColor("font_color", "OptionButton", EvolitPalette.MistWhite);
        theme.SetColor("font_hover_color", "OptionButton", EvolitPalette.MistWhite);
        theme.SetColor("font_color", "LineEdit", EvolitPalette.MistWhite);
        theme.SetColor("font_placeholder_color", "LineEdit", new Color(EvolitPalette.FogBlue, 0.72f));
        theme.SetColor("font_color", "ProgressBar", EvolitPalette.MistWhite);

        theme.SetColor("icon_normal_color", "Button", EvolitPalette.MistWhite);
        theme.SetColor("icon_hover_color", "Button", EvolitPalette.EvolutionCyan);
        theme.SetColor("icon_pressed_color", "Button", EvolitPalette.DeepNavyTeal);
        theme.SetColor("icon_focus_color", "Button", EvolitPalette.EvolutionCyan);
        theme.SetColor("icon_disabled_color", "Button", EvolitPalette.Disabled);

        theme.SetFontSize("font_size", "Label", UiMetrics.Font(17));
        theme.SetFontSize("font_size", "Button", UiMetrics.Font(16));
        theme.SetFontSize("font_size", "CheckButton", UiMetrics.Font(15));
        theme.SetFontSize("font_size", "OptionButton", UiMetrics.Font(15));
        theme.SetFontSize("font_size", "LineEdit", UiMetrics.Font(15));

        theme.SetStylebox(
            "normal",
            "Button",
            ButtonStyle(
                EvolitPalette.PanelSoft,
                new Color(0.18f, 0.42f, 0.47f, 0.48f)));
        theme.SetStylebox(
            "hover",
            "Button",
            ButtonStyle(
                new Color(0.055f, 0.235f, 0.275f, 0.96f),
                new Color(EvolitPalette.EvolutionCyan, 0.85f)));
        theme.SetStylebox(
            "pressed",
            "Button",
            ButtonStyle(
                EvolitPalette.EvolutionCyan,
                EvolitPalette.SoftAqua));
        theme.SetStylebox(
            "focus",
            "Button",
            ButtonStyle(
                new Color(0.040f, 0.190f, 0.225f, 0.96f),
                EvolitPalette.EvolutionCyan));
        theme.SetStylebox(
            "disabled",
            "Button",
            ButtonStyle(
                new Color(0.035f, 0.100f, 0.115f, 0.62f),
                new Color(0.22f, 0.31f, 0.33f, 0.45f)));

        theme.SetStylebox(
            "normal",
            "OptionButton",
            ButtonStyle(
                EvolitPalette.PanelSoft,
                new Color(0.18f, 0.42f, 0.47f, 0.48f)));
        theme.SetStylebox(
            "hover",
            "OptionButton",
            ButtonStyle(
                new Color(0.055f, 0.235f, 0.275f, 0.96f),
                new Color(EvolitPalette.EvolutionCyan, 0.85f)));
        theme.SetStylebox(
            "pressed",
            "OptionButton",
            ButtonStyle(
                new Color(0.045f, 0.205f, 0.240f, 0.98f),
                EvolitPalette.EvolutionCyan));
        theme.SetStylebox(
            "focus",
            "OptionButton",
            ButtonStyle(
                new Color(0.040f, 0.190f, 0.225f, 0.96f),
                EvolitPalette.EvolutionCyan));

        theme.SetStylebox(
            "normal",
            "LineEdit",
            InputStyle(false));
        theme.SetStylebox(
            "focus",
            "LineEdit",
            InputStyle(true));

        theme.SetStylebox("panel", "PanelContainer", CardStyle());

        theme.SetConstant(
            "separation",
            "VBoxContainer",
            UiMetrics.Space(10));
        theme.SetConstant(
            "separation",
            "HBoxContainer",
            UiMetrics.Space(12));

        return theme;
    }

    public static StyleBoxFlat CardStyle(float opacity = 0.90f)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.022f, 0.094f, 0.116f, opacity),
            BorderColor = new Color(0.125f, 0.780f, 0.831f, 0.22f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = UiMetrics.Space(18),
            CornerRadiusTopRight = UiMetrics.Space(18),
            CornerRadiusBottomLeft = UiMetrics.Space(18),
            CornerRadiusBottomRight = UiMetrics.Space(18)
        };
    }

    public static StyleBoxFlat SectionStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.030f, 0.135f, 0.160f, 0.72f),
            BorderColor = new Color(0.125f, 0.780f, 0.831f, 0.14f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = UiMetrics.Space(13),
            CornerRadiusTopRight = UiMetrics.Space(13),
            CornerRadiusBottomLeft = UiMetrics.Space(13),
            CornerRadiusBottomRight = UiMetrics.Space(13)
        };
    }

    public static StyleBoxFlat SubtleSectionStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.024f, 0.105f, 0.125f, 0.48f),
            BorderColor = new Color(EvolitPalette.FogBlue, 0.10f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = UiMetrics.Space(10),
            CornerRadiusTopRight = UiMetrics.Space(10),
            CornerRadiusBottomLeft = UiMetrics.Space(10),
            CornerRadiusBottomRight = UiMetrics.Space(10)
        };
    }

    public static void MarkPrimary(Button button)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            ButtonStyle(
                new Color(0.055f, 0.270f, 0.300f, 0.94f),
                new Color(EvolitPalette.EvolutionCyan, 0.72f)));
        button.AddThemeStyleboxOverride(
            "hover",
            ButtonStyle(
                new Color(0.075f, 0.330f, 0.355f, 0.98f),
                EvolitPalette.EvolutionCyan));
    }

    public static void MarkDanger(Button button)
    {
        button.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.WarmAlert, 0.96f));
        button.AddThemeColorOverride(
            "font_hover_color",
            EvolitPalette.MistWhite);
        button.AddThemeStyleboxOverride(
            "normal",
            ButtonStyle(
                new Color(0.18f, 0.075f, 0.075f, 0.66f),
                new Color(EvolitPalette.WarmAlert, 0.42f)));
        button.AddThemeStyleboxOverride(
            "hover",
            ButtonStyle(
                new Color(0.28f, 0.085f, 0.085f, 0.90f),
                new Color(EvolitPalette.WarmAlert, 0.84f)));
    }

    private static StyleBoxFlat InputStyle(bool focused)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.016f, 0.070f, 0.084f, 0.88f),
            BorderColor = focused
                ? new Color(EvolitPalette.EvolutionCyan, 0.78f)
                : new Color(EvolitPalette.FogBlue, 0.24f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = UiMetrics.Space(10),
            CornerRadiusTopRight = UiMetrics.Space(10),
            CornerRadiusBottomLeft = UiMetrics.Space(10),
            CornerRadiusBottomRight = UiMetrics.Space(10),
            ContentMarginLeft = UiMetrics.Px(12),
            ContentMarginRight = UiMetrics.Px(12),
            ContentMarginTop = UiMetrics.Px(8),
            ContentMarginBottom = UiMetrics.Px(8)
        };
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = UiMetrics.Space(11),
            CornerRadiusTopRight = UiMetrics.Space(11),
            CornerRadiusBottomLeft = UiMetrics.Space(11),
            CornerRadiusBottomRight = UiMetrics.Space(11),
            ContentMarginLeft = UiMetrics.Px(10),
            ContentMarginRight = UiMetrics.Px(10),
            ContentMarginTop = UiMetrics.Px(7),
            ContentMarginBottom = UiMetrics.Px(7)
        };
    }
}
