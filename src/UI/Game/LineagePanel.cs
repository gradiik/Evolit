using System;
using System.Collections.Generic;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class LineagePanel : Control
{
    public event Action? CloseRequested;

    private DemoWorldDataProvider? _world;
    private LineageCanvas? _canvas;
    private readonly Dictionary<LineageFilter, Button> _filters = new();

    public void Configure(DemoWorldDataProvider world)
    {
        _world = world;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect
        {
            Color = new Color(0.004f, 0.024f, 0.030f, 0.58f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 54);
        outer.AddThemeConstantOverride("margin_right", 54);
        outer.AddThemeConstantOverride("margin_top", 42);
        outer.AddThemeConstantOverride("margin_bottom", 54);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.99f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(titles);

        var title = new Label { Text = "Генетическое древо" };
        title.AddThemeFontSizeOverride("font_size", 30);
        titles.AddChild(title);

        var subtitle = new Label { Text = "Демо-линии, виды и подвиды. Панорамирование — средняя/правая кнопка мыши, масштаб — колесо." };
        subtitle.AddThemeFontSizeOverride("font_size", 12);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 6);
        header.AddChild(controls);

        controls.AddChild(ActionButton("−", () => _canvas?.ZoomOut()));
        controls.AddChild(ActionButton("+", () => _canvas?.ZoomIn()));
        controls.AddChild(ActionButton("Сброс", () => _canvas?.ResetView()));
        controls.AddChild(ActionButton("По центру", () => _canvas?.CenterView()));

        var close = new Button { Text = "Закрыть", Icon = EvolitIcons.Load("actions/close.svg"), CustomMinimumSize = new Vector2(110, 38) };
        close.Pressed += () => CloseRequested?.Invoke();
        controls.AddChild(close);

        var filterRow = new HBoxContainer();
        filterRow.AddThemeConstantOverride("separation", 6);
        root.AddChild(filterRow);

        AddFilter(filterRow, LineageFilter.All, "Все");
        AddFilter(filterRow, LineageFilter.Plants, "Растения");
        AddFilter(filterRow, LineageFilter.Creatures, "Существа");
        AddFilter(filterRow, LineageFilter.Extinct, "Вымершие");
        AddFilter(filterRow, LineageFilter.Active, "Активные");

        filterRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        filterRow.AddChild(Legend(EvolitPalette.EvolutionCyan, "Исходная"));
        filterRow.AddChild(Legend(EvolitPalette.YoungLeaf, "Растения"));
        filterRow.AddChild(Legend(EvolitPalette.SoftAqua, "Существа"));
        filterRow.AddChild(Legend(EvolitPalette.WarmAlert, "Вымершие"));

        root.AddChild(new HSeparator());

        _canvas = new LineageCanvas { SizeFlagsVertical = SizeFlags.ExpandFill };
        if (_world is not null)
            _canvas.Configure(_world);
        root.AddChild(_canvas);

        UpdateFilterButtons(LineageFilter.All);
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(62, 38) };
        button.Pressed += action;
        return button;
    }

    private void AddFilter(Container parent, LineageFilter filter, string text)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(100, 34)
        };
        button.Pressed += () =>
        {
            _canvas?.SetFilter(filter);
            UpdateFilterButtons(filter);
        };
        _filters[filter] = button;
        parent.AddChild(button);
    }

    private void UpdateFilterButtons(LineageFilter active)
    {
        foreach (var pair in _filters)
            pair.Value.ButtonPressed = pair.Key == active;
    }

    private static Control Legend(Color color, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);

        row.AddChild(new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(8, 8),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(label);
        return row;
    }
}
