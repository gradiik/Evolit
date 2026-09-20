using Godot;

namespace Evolit.UI;

public sealed partial class DebugOverlay : PanelContainer
{
    private Label? _label;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = 0.01f;
        AnchorRight = 0.31f;
        AnchorTop = 0.01f;
        AnchorBottom = 0.30f;
        AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        _label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _label.AddThemeFontSizeOverride("font_size", 13);
        _label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        margin.AddChild(_label);
    }

    public void Toggle()
    {
        Visible = !Visible;
    }

    public void SetData(string text)
    {
        if (_label is not null)
            _label.Text = text;
    }
}
