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

    public void Configure(
        string title,
        string subtitle,
        IReadOnlyList<string> lines)
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

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", UiMetrics.Space(72));
        outer.AddThemeConstantOverride("margin_right", UiMetrics.Space(72));
        outer.AddThemeConstantOverride("margin_top", UiMetrics.Space(54));
        outer.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(64));
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.98f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(24));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(24));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(20));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(20));
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", UiMetrics.Space(9));
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        header.AddChild(titles);

        var title = new Label { Text = _title };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(27));
        titles.AddChild(title);

        if (!string.IsNullOrWhiteSpace(_subtitle))
        {
            var subtitle = new Label
            {
                Text = _subtitle,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            subtitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
            subtitle.AddThemeColorOverride(
                "font_color",
                EvolitPalette.FogBlue);
            titles.AddChild(subtitle);
        }

        var close = new Button
        {
            Text = "Закрыть",
            Icon = EvolitIcons.Load("actions/close.svg"),
            CustomMinimumSize = UiMetrics.Size(108, 38)
        };
        close.Pressed += () => CloseRequested?.Invoke();
        UiMotion.BindButton(close);
        header.AddChild(close);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", UiMetrics.Space(10));
        scroll.AddChild(content);

        foreach (var line in _lines)
        {
            var label = new Label
            {
                Text = line,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeColorOverride(
                "font_color",
                EvolitPalette.MistWhite);
            content.AddChild(label);
        }

        UiMotion.FadeIn(
            card,
            new Vector2(0, UiMetrics.Px(8)));
    }
}
