using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class EventsPanel : Control
{
    public event Action? CloseRequested;

    private DemoWorldDataProvider? _world;
    private VBoxContainer? _feed;
    private readonly Dictionary<string, Button> _filters = new();
    private string _activeFilter = "Все";
    private LineEdit? _search;
    private OptionButton? _severity;
    private OptionButton? _kind;
    private Label? _resultCount;

    public void Configure(DemoWorldDataProvider world)
    {
        _world = world;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        BuildFrame();

        if (_world is not null)
            _world.DataChanged += Refresh;

        Refresh();
    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.DataChanged -= Refresh;
    }

    private void BuildFrame()
    {
        var dim = new ColorRect
        {
            Color = new Color(0.004f, 0.024f, 0.030f, 0.30f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer
        {
            AnchorLeft = 0.08f,
            AnchorRight = 0.92f,
            AnchorTop = 0.10f,
            AnchorBottom = 0.86f
        };
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.985f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 26);
        margin.AddThemeConstantOverride("margin_right", 26);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(titles);

        var title = new Label { Text = "События" };
        title.AddThemeFontSizeOverride("font_size", 30);
        titles.AddChild(title);

        var subtitle = new Label { Text = "Живой feed недавних событий мира, системы и эволюционного слоя." };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var close = new Button { Text = "Закрыть", Icon = EvolitIcons.Load("actions/close.svg"), CustomMinimumSize = new Vector2(112, 40) };
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 6);
        root.AddChild(filters);

        AddFilter(filters, "Все");
        AddFilter(filters, "Мир");
        AddFilter(filters, "Система");
        AddFilter(filters, "Эволюция");

        filters.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 7);
        root.AddChild(tools);

        _severity = new OptionButton { CustomMinimumSize = new Vector2(145, 34) };
        foreach (var item in new[] { "Любая важность", "Обычные", "Предупреждения", "Важные" })
            _severity.AddItem(item);
        _severity.ItemSelected += _ => Refresh();
        tools.AddChild(_severity);

        _kind = new OptionButton { CustomMinimumSize = new Vector2(150, 34) };
        foreach (var item in new[] { "Любой тип", "Наблюдения", "Ветвления", "Вымирания", "Сохранения", "Время", "Runtime" })
            _kind.AddItem(item);
        _kind.ItemSelected += _ => Refresh();
        tools.AddChild(_kind);

        _resultCount = new Label
        {
            Text = "0 событий",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _resultCount.AddThemeFontSizeOverride("font_size", 11);
        _resultCount.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        tools.AddChild(_resultCount);

        _search = new LineEdit { PlaceholderText = "Поиск событий…", ClearButtonEnabled = true, CustomMinimumSize = new Vector2(250, 36) };
        _search.TextChanged += _ => Refresh();
        tools.AddChild(_search);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        _feed = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _feed.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_feed);

        UpdateFilterButtons();
    }

    private void AddFilter(Container parent, string name)
    {
        var button = new Button
        {
            Text = name,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(108, 36)
        };
        button.Pressed += () =>
        {
            _activeFilter = name;
            UpdateFilterButtons();
            Refresh();
        };
        _filters[name] = button;
        parent.AddChild(button);
    }

    private void UpdateFilterButtons()
    {
        foreach (var pair in _filters)
            pair.Value.ButtonPressed = pair.Key == _activeFilter;
    }

    private void Refresh()
    {
        if (_feed is null || _world is null)
            return;

        foreach (var child in _feed.GetChildren())
        {
            _feed.RemoveChild(child);
            child.QueueFree();
        }

        IEnumerable<DemoEventEntry> events = _world.Events;
        events = _activeFilter switch
        {
            "Мир" => events.Where(item => item.Category == DemoEventCategory.World),
            "Система" => events.Where(item => item.Category == DemoEventCategory.System),
            "Эволюция" => events.Where(item => item.Category == DemoEventCategory.Evolution),
            _ => events
        };

        events = (_severity?.Selected ?? 0) switch
        {
            1 => events.Where(item => item.Severity == DemoEventSeverity.Info),
            2 => events.Where(item => item.Severity == DemoEventSeverity.Warning),
            3 => events.Where(item => item.Severity == DemoEventSeverity.Important),
            _ => events
        };

        events = (_kind?.Selected ?? 0) switch
        {
            1 => events.Where(item => item.Kind == DemoEventKind.Observation),
            2 => events.Where(item => item.Kind == DemoEventKind.Branching),
            3 => events.Where(item => item.Kind == DemoEventKind.Extinction),
            4 => events.Where(item => item.Kind == DemoEventKind.Save),
            5 => events.Where(item => item.Kind == DemoEventKind.Speed),
            6 => events.Where(item => item.Kind == DemoEventKind.Runtime),
            _ => events
        };

        var query = _search?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(query))
        {
            events = events.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.RelatedEntityId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = events.Take(60).ToList();
        if (_resultCount is not null)
            _resultCount.Text = $"{materialized.Count} событий";

        if (materialized.Count == 0)
        {
            var empty = new Label
            {
                Text = "По текущим фильтрам событий нет.",
                CustomMinimumSize = new Vector2(0, 72),
                VerticalAlignment = VerticalAlignment.Center
            };
            empty.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
            _feed.AddChild(empty);
            return;
        }

        foreach (var entry in materialized)
            _feed.AddChild(BuildEvent(entry));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("game_pause")) { CloseRequested?.Invoke(); GetViewport().SetInputAsHandled(); }
    }

    private static Control BuildEvent(DemoEventEntry entry)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 66) };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        margin.AddChild(row);

        var accent = EventAccent(entry);
        var icon = new TextureRect
        {
            Texture = EvolitIcons.Load(entry.IconPath),
            CustomMinimumSize = new Vector2(28, 28),
            Modulate = accent,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(icon);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(text);

        var titleRow = new HBoxContainer();
        text.AddChild(titleRow);

        var title = new Label { Text = entry.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 16);
        titleRow.AddChild(title);

        titleRow.AddChild(Badge(KindLabel(entry.Kind), accent));
        if (entry.Severity != DemoEventSeverity.Info)
            titleRow.AddChild(Badge(SeverityLabel(entry.Severity), SeverityColor(entry.Severity)));

        var description = new Label { Text = entry.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        description.AddThemeFontSizeOverride("font_size", 12);
        description.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        text.AddChild(description);

        var meta = new VBoxContainer();
        row.AddChild(meta);

        var time = new Label { Text = $"День {entry.Day}\n{entry.Time}", HorizontalAlignment = HorizontalAlignment.Right };
        time.AddThemeFontSizeOverride("font_size", 11);
        time.AddThemeColorOverride("font_color", accent);
        meta.AddChild(time);

        return panel;
    }

    private static Control Badge(string text, Color color)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(color, 0.08f),
            BorderColor = new Color(color, 0.30f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7
        });

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_top", 2);
        margin.AddThemeConstantOverride("margin_bottom", 2);
        panel.AddChild(margin);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 9);
        label.AddThemeColorOverride("font_color", color);
        margin.AddChild(label);
        return panel;
    }

    private static string KindLabel(DemoEventKind kind) => kind switch
    {
        DemoEventKind.Branching => "ВЕТВЛЕНИЕ",
        DemoEventKind.Extinction => "ВЫМИРАНИЕ",
        DemoEventKind.WorldCreated => "МИР",
        DemoEventKind.Observation => "НАБЛЮДЕНИЕ",
        DemoEventKind.Save => "СОХРАНЕНИЕ",
        DemoEventKind.Speed => "ВРЕМЯ",
        DemoEventKind.Runtime => "RUNTIME",
        _ => "СОБЫТИЕ"
    };

    private static string SeverityLabel(DemoEventSeverity severity) => severity switch
    {
        DemoEventSeverity.Warning => "ВНИМАНИЕ",
        DemoEventSeverity.Important => "ВАЖНО",
        _ => "INFO"
    };

    private static Color EventAccent(DemoEventEntry entry)
    {
        if (entry.Severity == DemoEventSeverity.Warning)
            return EvolitPalette.WarmAlert;
        if (entry.Severity == DemoEventSeverity.Important)
            return EvolitPalette.WarmSand;

        return entry.Category switch
        {
            DemoEventCategory.Evolution => EvolitPalette.EvolutionCyan,
            DemoEventCategory.System => EvolitPalette.FogBlue,
            _ => EvolitPalette.YoungLeaf
        };
    }

    private static Color SeverityColor(DemoEventSeverity severity) => severity switch
    {
        DemoEventSeverity.Warning => EvolitPalette.WarmAlert,
        DemoEventSeverity.Important => EvolitPalette.WarmSand,
        _ => EvolitPalette.FogBlue
    };
}
