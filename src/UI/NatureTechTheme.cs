using Godot;

namespace Evolit.UI;

public static class NatureTechTheme
{
    public static Theme Create()
    {
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

        theme.SetColor("icon_normal_color", "Button", EvolitPalette.MistWhite);
        theme.SetColor("icon_hover_color", "Button", EvolitPalette.EvolutionCyan);
        theme.SetColor("icon_pressed_color", "Button", EvolitPalette.DeepNavyTeal);
        theme.SetColor("icon_focus_color", "Button", EvolitPalette.EvolutionCyan);
        theme.SetColor("icon_disabled_color", "Button", EvolitPalette.Disabled);

        theme.SetFontSize("font_size", "Label", 17);
        theme.SetFontSize("font_size", "Button", 17);
        theme.SetFontSize("font_size", "CheckButton", 16);
        theme.SetFontSize("font_size", "OptionButton", 16);

        theme.SetStylebox("normal", "Button", ButtonStyle(EvolitPalette.PanelSoft, new Color(0.18f, 0.42f, 0.47f, 0.48f)));
        theme.SetStylebox("hover", "Button", ButtonStyle(new Color(0.055f, 0.235f, 0.275f, 0.96f), new Color(EvolitPalette.EvolutionCyan, 0.85f)));
        theme.SetStylebox("pressed", "Button", ButtonStyle(EvolitPalette.EvolutionCyan, EvolitPalette.SoftAqua));
        theme.SetStylebox("focus", "Button", ButtonStyle(new Color(0.040f, 0.190f, 0.225f, 0.96f), EvolitPalette.EvolutionCyan));
        theme.SetStylebox("disabled", "Button", ButtonStyle(new Color(0.035f, 0.100f, 0.115f, 0.62f), new Color(0.22f, 0.31f, 0.33f, 0.45f)));

        theme.SetStylebox("normal", "OptionButton", ButtonStyle(EvolitPalette.PanelSoft, new Color(0.18f, 0.42f, 0.47f, 0.48f)));
        theme.SetStylebox("hover", "OptionButton", ButtonStyle(new Color(0.055f, 0.235f, 0.275f, 0.96f), new Color(EvolitPalette.EvolutionCyan, 0.85f)));
        theme.SetStylebox("pressed", "OptionButton", ButtonStyle(new Color(0.045f, 0.205f, 0.240f, 0.98f), EvolitPalette.EvolutionCyan));
        theme.SetStylebox("focus", "OptionButton", ButtonStyle(new Color(0.040f, 0.190f, 0.225f, 0.96f), EvolitPalette.EvolutionCyan));

        theme.SetStylebox("panel", "PanelContainer", CardStyle());

        theme.SetConstant("separation", "VBoxContainer", 10);
        theme.SetConstant("separation", "HBoxContainer", 12);

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
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18
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
            CornerRadiusTopLeft = 13,
            CornerRadiusTopRight = 13,
            CornerRadiusBottomLeft = 13,
            CornerRadiusBottomRight = 13
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
            CornerRadiusTopLeft = 13,
            CornerRadiusTopRight = 13,
            CornerRadiusBottomLeft = 13,
            CornerRadiusBottomRight = 13
        };
    }
}
