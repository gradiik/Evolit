using System;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class LineagePanel : Control
{
    public event Action? CloseRequested;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect
        {
            Color = new Color(0.005f, 0.025f, 0.032f, 0.62f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 100);
        outer.AddThemeConstantOverride("margin_right", 100);
        outer.AddThemeConstantOverride("margin_top", 80);
        outer.AddThemeConstantOverride("margin_bottom", 80);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.98f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        card.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var title = new Label { Text = "Генетическое древо", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 28);
        header.AddChild(title);

        var close = new Button { Text = "Закрыть", Icon = EvolitIcons.Load("actions/close.svg") };
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        var subtitle = new Label
        {
            Text = "Панорамирование: средняя/правая кнопка мыши · масштаб: колесо. Реальная lineage-модель появится позже."
        };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(subtitle);

        root.AddChild(new HSeparator());

        var canvas = new LineageCanvas { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(canvas);
    }
}
