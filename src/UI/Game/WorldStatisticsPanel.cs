using System;
using System.Collections.Generic;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class WorldStatisticsPanel : Control
{
    public event Action? CloseRequested;

    private static readonly WorldGraphSeries[] PopulationSeries =
    [
        new("Существа", WorldHistoryMetric.CreaturePopulation, EvolitPalette.SoftAqua),
        new("Растения", WorldHistoryMetric.PlantPopulation, EvolitPalette.YoungLeaf)
    ];

    private static readonly WorldGraphSeries[] DiversitySeries =
    [
        new("Виды", WorldHistoryMetric.SpeciesCount, EvolitPalette.EvolutionCyan),
        new("Подвиды", WorldHistoryMetric.SubspeciesCount, EvolitPalette.WarmSand)
    ];

    private DemoWorldDataProvider? _world;
    private ISimulationStatsProvider? _stats;
    private readonly Dictionary<string, Label> _values = new();
    private readonly List<RangeOption> _rangeOptions = new();
    private WorldStatsGraph? _populationGraph;
    private WorldStatsGraph? _diversityGraph;
    private OptionButton? _range;
    private string _selectedRangeKey = "all";
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
            _world.HistoryChanged += RefreshGraphs;

        RefreshAll();
    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.HistoryChanged -= RefreshGraphs;
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
            Color = new Color(0.004f, 0.024f, 0.030f, 0.36f),
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
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.985f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
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
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(30));
        titles.AddChild(title);

        var subtitle = new Label { Text = "Популяции и разнообразие мира во времени." };
        subtitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(13));
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var close = new Button
        {
            Text = "Закрыть",
            Icon = EvolitIcons.Load("actions/close.svg"),
            CustomMinimumSize = UiMetrics.Size(112, 40)
        };
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

        var rangeRow = new HBoxContainer();
        rangeRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(rangeRow);

        var rangeLabel = new Label { Text = "Период" };
        rangeLabel.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        rangeRow.AddChild(rangeLabel);

        _range = new OptionButton { CustomMinimumSize = UiMetrics.Size(170, 34) };
        _range.ItemSelected += index =>
        {
            if (index >= 0 && index < _rangeOptions.Count)
                _selectedRangeKey = _rangeOptions[(int)index].Key;
            ApplyRange();
        };
        rangeRow.AddChild(_range);

        var rangeHint = new Label
        {
            Text = "Наведи на график для точных значений · клик закрепляет точку · диапазоны появляются по мере накопления истории.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        rangeHint.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        rangeHint.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.78f));
        rangeRow.AddChild(rangeHint);

        var graphs = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        graphs.AddThemeConstantOverride("separation", 9);
        root.AddChild(graphs);

        _populationGraph = GraphCard(graphs, "Популяция", PopulationSeries, 205);
        _diversityGraph = GraphCard(graphs, "Разнообразие", DiversitySeries, 175);
    }

    private Control Metric(string key, string caption)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(0, 64),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 7);
        margin.AddThemeConstantOverride("margin_bottom", 7);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);
        margin.AddChild(box);

        var cap = new Label { Text = caption };
        cap.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        cap.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        box.AddChild(cap);

        var value = new Label { Text = "—" };
        value.AddThemeFontSizeOverride("font_size", UiMetrics.Font(18));
        value.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        box.AddChild(value);

        _values[key] = value;
        return panel;
    }

    private WorldStatsGraph GraphCard(Container parent, string title, IReadOnlyList<WorldGraphSeries> series, float minimumHeight)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, minimumHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        parent.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 5);
        margin.AddChild(box);

        var header = new HBoxContainer();
        box.AddChild(header);

        var label = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(16));
        label.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        header.AddChild(label);

        foreach (var item in series)
            header.AddChild(Legend(item.Label, item.Color));

        var graph = new WorldStatsGraph
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddChild(graph);
        return graph;
    }

    private static Control Legend(string text, Color color)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);

        var dot = new ColorRect
        {
            Color = color,
            CustomMinimumSize = UiMetrics.Size(9, 9),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(dot);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        label.AddThemeColorOverride("font_color", new Color(color, 0.92f));
        row.AddChild(label);
        return row;
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
    }

    private void RefreshGraphs()
    {
        if (_world is null)
            return;

        RebuildRangeOptions(_world.History);
        ApplyRange();
    }

    private void RebuildRangeOptions(IReadOnlyList<WorldHistorySample> history)
    {
        if (_range is null)
            return;

        _rangeOptions.Clear();
        _range.Clear();

        AddRange("all", "Вся история", 0);

        if (history.Count > 24)
            AddRange("last24", "Последние 24", Math.Max(0, history.Count - 24));

        var firstTimedDay = int.MaxValue;
        var lastTimedDay = int.MinValue;
        for (var i = 0; i < history.Count; i++)
        {
            if (!history[i].Day.HasValue)
                continue;
            firstTimedDay = Math.Min(firstTimedDay, history[i].Day!.Value);
            lastTimedDay = Math.Max(lastTimedDay, history[i].Day!.Value);
        }

        if (lastTimedDay >= firstTimedDay && lastTimedDay - firstTimedDay >= 6)
            AddRange("days7", "7 дней", FindDayStart(history, lastTimedDay - 6));
        if (lastTimedDay >= firstTimedDay && lastTimedDay - firstTimedDay >= 29)
            AddRange("days30", "30 дней", FindDayStart(history, lastTimedDay - 29));

        var selectedIndex = 0;
        for (var i = 0; i < _rangeOptions.Count; i++)
        {
            if (_rangeOptions[i].Key == _selectedRangeKey)
            {
                selectedIndex = i;
                break;
            }
        }

        _selectedRangeKey = _rangeOptions[selectedIndex].Key;
        _range.Select(selectedIndex);
        _range.Disabled = _rangeOptions.Count <= 1;
    }

    private void AddRange(string key, string title, int startIndex)
    {
        if (_range is null)
            return;

        _rangeOptions.Add(new RangeOption(key, startIndex));
        _range.AddItem(title);
    }

    private void ApplyRange()
    {
        if (_world is null)
            return;

        var startIndex = 0;
        foreach (var option in _rangeOptions)
        {
            if (option.Key == _selectedRangeKey)
            {
                startIndex = option.StartIndex;
                break;
            }
        }

        _populationGraph?.SetData(_world.History, PopulationSeries, startIndex);
        _diversityGraph?.SetData(_world.History, DiversitySeries, startIndex);
    }

    private static int FindDayStart(IReadOnlyList<WorldHistorySample> history, int minimumDay)
    {
        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].Day.HasValue && history[i].Day!.Value >= minimumDay)
                return i;
        }
        return 0;
    }

    private void Set(string key, string text)
    {
        if (_values.TryGetValue(key, out var label))
            label.Text = text;
    }

    private readonly record struct RangeOption(string Key, int StartIndex);
}
