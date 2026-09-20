using System;
using System.Collections.Generic;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class GameToolPanel : Control
{
    public event Action? CloseRequested;

    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private IReadOnlyList<string> _lines = Array.Empty<string>();

    public void Configure(string title, string subtitle, IReadOnlyList<string> lines)
    {
        _title = title;
        _subtitle = subtitle;
        _lines = lines;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect
        {
            Color = new Color(0.005f, 0.025f, 0.032f, 0.58f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(760, 500) };
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.98f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        card.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var title = new Label { Text = _title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 28);
        header.AddChild(title);

        var close = new Button { Text = "Закрыть", Icon = EvolitIcons.Load("actions/close.svg") };
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        if (!string.IsNullOrWhiteSpace(_subtitle))
        {
            var subtitle = new Label
            {
                Text = _subtitle,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
            root.AddChild(subtitle);
        }

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(content);

        foreach (var line in _lines)
        {
            var label = new Label
            {
                Text = line,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
            content.AddChild(label);
        }

        Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.15);
    }
}
