using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class EvolutionPanel : Control
{
    public event Action? CloseRequested;

    private readonly string[] _sections = ["Обзор", "Линии развития", "Мутации", "Давление среды", "Отбор"];
    private readonly Dictionary<string, Button> _navButtons = new();

    private DemoWorldDataProvider? _world;
    private ISimulationStatsProvider? _stats;
    private VBoxContainer? _content;
    private VBoxContainer? _summary;
    private string _activeSection = "Обзор";

    public void Configure(DemoWorldDataProvider world, ISimulationStatsProvider stats)
    {
        _world = world;
        _stats = stats;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect
        {
            Color = new Color(0.004f, 0.024f, 0.030f, 0.34f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 54);
        outer.AddThemeConstantOverride("margin_right", 54);
        outer.AddThemeConstantOverride("margin_top", 46);
        outer.AddThemeConstantOverride("margin_bottom", 54);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.985f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(titles);

        var title = new Label { Text = "Эволюция" };
        title.AddThemeFontSizeOverride("font_size", 30);
        titles.AddChild(title);

        var subtitle = new Label { Text = "Наблюдение за линиями развития и демонстрация будущих инструментов отбора." };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var close = new Button { Text = "Закрыть", Icon = EvolitIcons.Load("actions/close.svg"), CustomMinimumSize = new Vector2(112, 40) };
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        root.AddChild(new HSeparator());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        body.AddChild(BuildNavigation());

        var contentPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentPanel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(contentPanel);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 20);
        contentMargin.AddThemeConstantOverride("margin_right", 20);
        contentMargin.AddThemeConstantOverride("margin_top", 16);
        contentMargin.AddThemeConstantOverride("margin_bottom", 16);
        contentPanel.AddChild(contentMargin);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentMargin.AddChild(scroll);

        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_content);

        var summaryPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(245, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        summaryPanel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(summaryPanel);

        var summaryMargin = new MarginContainer();
        summaryMargin.AddThemeConstantOverride("margin_left", 16);
        summaryMargin.AddThemeConstantOverride("margin_right", 16);
        summaryMargin.AddThemeConstantOverride("margin_top", 16);
        summaryMargin.AddThemeConstantOverride("margin_bottom", 16);
        summaryPanel.AddChild(summaryMargin);

        _summary = new VBoxContainer();
        summaryMargin.AddChild(_summary);

        if (_world is not null)
            _world.DataChanged += HandleDataChanged;

        ShowSection(_activeSection);

    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.DataChanged -= HandleDataChanged;
    }

    private Control BuildNavigation()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(188, 0) };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var nav = new VBoxContainer();
        nav.AddThemeConstantOverride("separation", 7);
        margin.AddChild(nav);

        foreach (var section in _sections)
        {
            var button = new Button
            {
                Text = section,
                ToggleMode = true,
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 42)
            };
            var captured = section;
            button.Pressed += () => ShowSection(captured);
            _navButtons[section] = button;
            nav.AddChild(button);
        }

        return panel;
    }

    private void ShowSection(string section)
    {
        _activeSection = section;

        foreach (var pair in _navButtons)
            pair.Value.ButtonPressed = pair.Key == section;

        Clear(_content);
        Clear(_summary);

        if (_content is null || _summary is null || _world is null)
            return;

        var heading = new Label { Text = section };
        heading.AddThemeFontSizeOverride("font_size", 23);
        heading.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        _content.AddChild(heading);

        switch (section)
        {
            case "Обзор": BuildOverview(); break;
            case "Линии развития": BuildLineages(); break;
            case "Мутации": BuildMutations(); break;
            case "Давление среды": BuildPressure(); break;
            case "Отбор": BuildSelection(); break;
        }

        BuildSummary();
    }

    private void BuildOverview()
    {
        if (_content is null || _world is null)
            return;

        var stats = _stats?.GetSnapshot() ?? default;
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        _content.AddChild(grid);

        grid.AddChild(MetricCard("Активных видов", stats.SpeciesCount.ToString(), "Основные линии"));
        grid.AddChild(MetricCard("Подвидов", stats.SubspeciesCount.ToString(), "Активные ответвления"));
        grid.AddChild(MetricCard("Адаптивность", $"{_world.AverageAdaptability * 100:0}%", "Среднее demo-значение"));

        var dominant = _world.Species
            .Where(s => s.Status == DemoSpeciesStatus.Active && s.Kind != DemoSpeciesKind.Origin)
            .OrderByDescending(s => s.Population)
            .FirstOrDefault();
        grid.AddChild(MetricCard("Доминирует", dominant?.Name ?? "—", dominant is null ? "Нет данных" : $"Популяция {dominant.Population}"));

        _content.AddChild(SectionLabel("Тренд подвижных форм"));

        var graph = new WorldStatsGraph();
        graph.SetData(
            _world.History,
            [new WorldGraphSeries("Существа", WorldHistoryMetric.CreaturePopulation, EvolitPalette.EvolutionCyan)]);
        _content.AddChild(graph);

        _content.AddChild(SectionLabel("Линии сейчас"));
        foreach (var lineage in _world.Species.Where(s => s.ParentId == "origin"))
            _content.AddChild(LineageCard(lineage));
    }

    private void BuildLineages()
    {
        if (_content is null || _world is null)
            return;

        foreach (var lineage in _world.Species.Where(s => s.ParentId == "origin"))
        {
            _content.AddChild(LineageCard(lineage));

            var children = _world.Species.Where(s => s.ParentId == lineage.Id).ToList();
            foreach (var child in children)
                _content.AddChild(SpeciesRow(child));
        }
    }

    private void BuildMutations()
    {
        if (_content is null)
            return;

        _content.AddChild(Hint("Это визуальный прототип каталога признаков. Геном и мутационная модель ещё не реализованы."));
        _content.AddChild(TraitCard("Ускоренный рост", "Рост", 0.72f, "Увеличивает темп набора массы в будущей модели."));
        _content.AddChild(TraitCard("Улучшенное поглощение", "Ресурсы", 0.64f, "Потенциальное повышение эффективности получения ресурсов."));
        _content.AddChild(TraitCard("Примитивное зрение", "Сенсорика", 0.48f, "Задел под будущую сенсорную систему существ."));
        _content.AddChild(TraitCard("Плотная оболочка", "Защита", 0.57f, "Визуальная карточка будущего защитного признака."));
    }

    private void BuildPressure()
    {
        if (_content is null)
            return;

        _content.AddChild(Hint("Индикаторы ниже — demo UI. Они не вычисляются настоящей экосистемой."));
        _content.AddChild(ProgressRow("Конкуренция", 58, "Средняя"));
        _content.AddChild(ProgressRow("Нехватка ресурсов", 34, "Низкая"));
        _content.AddChild(ProgressRow("Климатическое давление", 46, "Умеренное"));
        _content.AddChild(ProgressRow("Хищничество", 21, "Низкое"));
    }

    private void BuildSelection()
    {
        if (_content is null)
            return;

        _content.AddChild(Hint("Экран показывает будущую логику наблюдения за тем, какие признаки распространяются или теряют значение."));
        _content.AddChild(SelectionTrend("Компактное тело", "+12%", true, "Motilis Minor"));
        _content.AddChild(SelectionTrend("Эффективное поглощение", "+9%", true, "Viridia Minor"));
        _content.AddChild(SelectionTrend("Удлинённая форма", "+4%", true, "Motilis Longa"));
        _content.AddChild(SelectionTrend("Высокие затраты энергии", "−11%", false, "Motilis Brevis"));
    }

    private void BuildSummary()
    {
        if (_summary is null || _world is null)
            return;

        var label = new Label { Text = "Сводка" };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        _summary.AddChild(label);

        _summary.AddChild(SummaryLine("Активные линии", _world.Species.Count(s => s.ParentId == "origin" && s.Status == DemoSpeciesStatus.Active).ToString()));
        _summary.AddChild(SummaryLine("Активные подвиды", _world.SubspeciesCount.ToString()));
        _summary.AddChild(SummaryLine("Вымершие ветви", _world.Species.Count(s => s.Status == DemoSpeciesStatus.Extinct).ToString()));
        _summary.AddChild(SummaryLine("Средняя адаптивность", $"{_world.AverageAdaptability * 100:0}%"));

        _summary.AddChild(new HSeparator());

        var note = new Label
        {
            Text = "Интерфейс 0.0.4\n\nМутации, давление среды и отбор здесь являются демонстрацией интерфейса и не изменяют мир. Биологическая симуляция будет подключена отдельно.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 12);
        note.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        _summary.AddChild(note);
    }

    private static Control MetricCard(string caption, string value, string note)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 86), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.72f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        margin.AddChild(box);

        var cap = new Label { Text = caption };
        cap.AddThemeFontSizeOverride("font_size", 11);
        cap.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        box.AddChild(cap);

        var data = new Label { Text = value };
        data.AddThemeFontSizeOverride("font_size", 21);
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        box.AddChild(data);

        var sub = new Label { Text = note };
        sub.AddThemeFontSizeOverride("font_size", 11);
        sub.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.76f));
        box.AddChild(sub);

        return panel;
    }

    private static Control LineageCard(DemoSpeciesRecord lineage)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 88) };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.72f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        margin.AddChild(row);

        var icon = new TextureRect
        {
            Texture = EvolitIcons.Load(lineage.Kind == DemoSpeciesKind.Plant ? "biology/plant.svg" : "biology/creature.svg"),
            CustomMinimumSize = new Vector2(30, 30),
            Modulate = lineage.Kind == DemoSpeciesKind.Plant ? EvolitPalette.YoungLeaf : EvolitPalette.SoftAqua,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(icon);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(text);

        var title = new Label { Text = lineage.Name };
        title.AddThemeFontSizeOverride("font_size", 17);
        text.AddChild(title);

        var description = new Label { Text = lineage.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        description.AddThemeFontSizeOverride("font_size", 12);
        description.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        text.AddChild(description);

        var value = new Label { Text = $"{lineage.Population} · {lineage.Adaptability * 100:0}%" };
        value.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        row.AddChild(value);

        return panel;
    }

    private static Control SpeciesRow(DemoSpeciesRecord species)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 38) };
        var name = new Label { Text = $"   {species.Name}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.AddThemeColorOverride("font_color", species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.Disabled : EvolitPalette.MistWhite);
        row.AddChild(name);

        var status = new Label { Text = species.Status == DemoSpeciesStatus.Extinct ? "Вымер" : $"Популяция {species.Population}" };
        status.AddThemeColorOverride("font_color", species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.WarmAlert : EvolitPalette.FogBlue);
        row.AddChild(status);
        return row;
    }

    private static Control TraitCard(string name, string category, float progress, string description)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.70f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        margin.AddChild(box);

        var header = new HBoxContainer();
        box.AddChild(header);

        var title = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 16);
        header.AddChild(title);

        var badge = new Label { Text = category };
        badge.AddThemeFontSizeOverride("font_size", 11);
        badge.AddThemeColorOverride("font_color", EvolitPalette.EvolutionCyan);
        header.AddChild(badge);

        var text = new Label { Text = description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        text.AddThemeFontSizeOverride("font_size", 12);
        text.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        box.AddChild(text);

        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = progress * 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 7) };
        box.AddChild(bar);

        return panel;
    }

    private static Control ProgressRow(string name, double value, string label)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.68f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        margin.AddChild(box);

        var row = new HBoxContainer();
        box.AddChild(row);

        var nameLabel = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(nameLabel);

        var valueLabel = new Label { Text = $"{label} · {value:0}%" };
        valueLabel.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(valueLabel);

        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = value, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
        box.AddChild(bar);
        return panel;
    }

    private static Control SelectionTrend(string trait, string delta, bool positive, string line)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.68f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        margin.AddChild(row);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(text);

        var title = new Label { Text = trait };
        text.AddChild(title);

        var source = new Label { Text = line };
        source.AddThemeFontSizeOverride("font_size", 11);
        source.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        text.AddChild(source);

        var change = new Label { Text = delta };
        change.AddThemeFontSizeOverride("font_size", 18);
        change.AddThemeColorOverride("font_color", positive ? EvolitPalette.YoungLeaf : EvolitPalette.WarmAlert);
        row.AddChild(change);

        return panel;
    }

    private static Control SummaryLine(string name, string value)
    {
        var row = new HBoxContainer();
        var label = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(label);

        var data = new Label { Text = value };
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        row.AddChild(data);
        return row;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        return label;
    }

    private static Label Hint(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        return label;
    }

    private void HandleDataChanged()
    {
        ShowSection(_activeSection);
    }

    private static void Clear(Node? node)
    {
        if (node is null)
            return;

        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
