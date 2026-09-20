using System;
using System.Collections.Generic;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class WorldStatisticsPanel : Control
{
    public event Action? CloseRequested;

    private DemoWorldDataProvider? _world;
    private ISimulationStatsProvider? _stats;

    private readonly Dictionary<string, Label> _values = new();
    private WorldStatsGraph? _creatureGraph;
    private WorldStatsGraph? _plantGraph;
    private WorldStatsGraph? _speciesGraph;
    private WorldStatsGraph? _subspeciesGraph;
    private double _refreshTimer;

    public void Configure(DemoWorldDataProvider world, ISimulationStatsProvider stats)
    {
        _world = world;
        _stats = stats;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        BuildFrame();

        if (_world is not null)
            _world.DataChanged += RefreshGraphs;

        RefreshAll();
    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.DataChanged -= RefreshGraphs;
    }

    public override void _Process(double delta)
    {
        _refreshTimer += delta;
        if (_refreshTimer < 0.25)
            return;

        _refreshTimer = 0;
        RefreshValues();
    }

    private void BuildFrame()
    {
        var dim = new ColorRect
        {
            Color = new Color(0.004f, 0.024f, 0.030f, 0.34f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 58);
        outer.AddThemeConstantOverride("margin_right", 58);
        outer.AddThemeConstantOverride("margin_top", 44);
        outer.AddThemeConstantOverride("margin_bottom", 54);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.985f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(titles);

        var title = new Label { Text = "Статистика мира" };
        title.AddThemeFontSizeOverride("font_size", 30);
        titles.AddChild(title);

        var subtitle = new Label { Text = "Сводка состояния мира, популяций и производительности runtime." };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var close = new Button { Text = "Закрыть", Icon = EvolitIcons.Load("actions/close.svg"), CustomMinimumSize = new Vector2(112, 40) };
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        root.AddChild(new HSeparator());

        var summary = new GridContainer { Columns = 6 };
        summary.AddThemeConstantOverride("h_separation", 8);
        summary.AddThemeConstantOverride("v_separation", 8);
        root.AddChild(summary);

        summary.AddChild(Metric("day", "День"));
        summary.AddChild(Metric("time", "Время"));
        summary.AddChild(Metric("creatures", "Существа"));
        summary.AddChild(Metric("plants", "Растения"));
        summary.AddChild(Metric("species", "Виды"));
        summary.AddChild(Metric("subspecies", "Подвиды"));

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);

        var lifePanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        lifePanel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(lifePanel);

        var lifeMargin = new MarginContainer();
        lifeMargin.AddThemeConstantOverride("margin_left", 14);
        lifeMargin.AddThemeConstantOverride("margin_right", 14);
        lifeMargin.AddThemeConstantOverride("margin_top", 12);
        lifeMargin.AddThemeConstantOverride("margin_bottom", 12);
        lifePanel.AddChild(lifeMargin);

        var graphsRoot = new VBoxContainer();
        lifeMargin.AddChild(graphsRoot);

        var lifeTitle = new Label { Text = "Жизнь и разнообразие" };
        lifeTitle.AddThemeFontSizeOverride("font_size", 17);
        lifeTitle.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        graphsRoot.AddChild(lifeTitle);

        var graphGrid = new GridContainer { Columns = 2, SizeFlagsVertical = SizeFlags.ExpandFill };
        graphGrid.AddThemeConstantOverride("h_separation", 8);
        graphGrid.AddThemeConstantOverride("v_separation", 8);
        graphsRoot.AddChild(graphGrid);

        _creatureGraph = GraphCard(graphGrid, "Популяция существ", EvolitPalette.SoftAqua);
        _plantGraph = GraphCard(graphGrid, "Популяция растений", EvolitPalette.YoungLeaf);
        _speciesGraph = GraphCard(graphGrid, "Количество видов", EvolitPalette.EvolutionCyan);
        _subspeciesGraph = GraphCard(graphGrid, "Количество подвидов", EvolitPalette.WarmSand);

        var performance = new PanelContainer { CustomMinimumSize = new Vector2(250, 0) };
        performance.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(performance);

        var perfMargin = new MarginContainer();
        perfMargin.AddThemeConstantOverride("margin_left", 16);
        perfMargin.AddThemeConstantOverride("margin_right", 16);
        perfMargin.AddThemeConstantOverride("margin_top", 14);
        perfMargin.AddThemeConstantOverride("margin_bottom", 14);
        performance.AddChild(perfMargin);

        var perf = new VBoxContainer();
        perfMargin.AddChild(perf);

        var perfTitle = new Label { Text = "Производительность" };
        perfTitle.AddThemeFontSizeOverride("font_size", 17);
        perfTitle.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        perf.AddChild(perfTitle);

        perf.AddChild(PerformanceLine("FPS", "fps"));
        perf.AddChild(PerformanceLine("TPS", "tps"));
        perf.AddChild(PerformanceLine("Tick", "tick"));
        perf.AddChild(PerformanceLine("Playtime", "playtime"));

        perf.AddChild(new HSeparator());

        var note = new Label
        {
            Text = "FPS — реальный.\nTPS и Tick — лёгкий runtime-time loop.\nПопуляции и разнообразие — demo-слой.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 11);
        note.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        perf.AddChild(note);
    }

    private Control Metric(string key, string caption)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 70), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);
        margin.AddChild(box);

        var cap = new Label { Text = caption };
        cap.AddThemeFontSizeOverride("font_size", 10);
        cap.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        box.AddChild(cap);

        var value = new Label { Text = "—" };
        value.AddThemeFontSizeOverride("font_size", 18);
        value.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        box.AddChild(value);

        _values[key] = value;
        return panel;
    }

    private Control PerformanceLine(string caption, string key)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 36) };
        var cap = new Label { Text = caption, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cap.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(cap);

        var value = new Label { Text = "—" };
        value.AddThemeFontSizeOverride("font_size", 16);
        value.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        row.AddChild(value);

        _values[key] = value;
        return row;
    }

    private WorldStatsGraph GraphCard(Container parent, string title, Color color)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 164), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.64f));
        parent.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        margin.AddChild(box);

        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", color);
        box.AddChild(label);

        var graph = new WorldStatsGraph { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(graph);
        return graph;
    }

    private void RefreshAll()
    {
        RefreshValues();
        RefreshGraphs();
    }

    private void RefreshValues()
    {
        if (_stats is null)
            return;

        var data = _stats.GetSnapshot();
        Set("day", data.Day.ToString());
        Set("time", data.GameTime);
        Set("creatures", data.CreatureCount.ToString());
        Set("plants", data.PlantCount.ToString());
        Set("species", data.SpeciesCount.ToString());
        Set("subspecies", data.SubspeciesCount.ToString());
        Set("fps", $"{data.Fps:0}");
        Set("tps", $"{data.Tps:0}");
        Set("tick", data.Tick.ToString());

        var playtime = TimeSpan.FromSeconds(Math.Max(0, data.PlaytimeSeconds));
        Set("playtime", $"{(int)playtime.TotalHours:00}:{playtime.Minutes:00}:{playtime.Seconds:00}");
    }

    private void RefreshGraphs()
    {
        if (_world is null)
            return;

        _creatureGraph?.SetValues(_world.CreaturePopulationHistory, EvolitPalette.SoftAqua);
        _plantGraph?.SetValues(_world.PlantPopulationHistory, EvolitPalette.YoungLeaf);
        _speciesGraph?.SetValues(_world.SpeciesHistory, EvolitPalette.EvolutionCyan);
        _subspeciesGraph?.SetValues(_world.SubspeciesHistory, EvolitPalette.WarmSand);
    }

    private void Set(string key, string text)
    {
        if (_values.TryGetValue(key, out var label))
            label.Text = text;
    }
}
