using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class ChroniclePanel : Control
{
    public event Action? CloseRequested;

    private DemoWorldDataProvider? _world;
    private VBoxContainer? _timeline;
    private LineEdit? _search;
    private Label? _resultCount;
    private Button? _sortButton;
    private readonly Dictionary<string, Button> _filters = new();
    private string _activeFilter = "Все";
    private bool _newestFirst = true;

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
            Color = new Color(0.004f, 0.024f, 0.030f, 0.32f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer
        {
            AnchorLeft = 0.07f,
            AnchorRight = 0.93f,
            AnchorTop = 0.08f,
            AnchorBottom = 0.875f
        };
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.985f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        card.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(titles);

        var title = new Label { Text = "Летопись мира" };
        title.AddThemeFontSizeOverride("font_size", 30);
        titles.AddChild(title);

        var subtitle = new Label { Text = "Постоянная история ключевых событий и поворотных точек мира." };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var close = new Button { Text = "Закрыть", Icon = EvolitIcons.Load("actions/close.svg"), CustomMinimumSize = new Vector2(112, 40) };
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 5);
        root.AddChild(filters);
        AddFilter(filters, "Все");
        AddFilter(filters, "Мир");
        AddFilter(filters, "Эволюция");
        AddFilter(filters, "Ветвления");
        AddFilter(filters, "Новые виды");
        AddFilter(filters, "Вымирания");
        AddFilter(filters, "Катастрофы");
        AddFilter(filters, "Системные");
        AddFilter(filters, "Важные");

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 7);
        root.AddChild(tools);

        _resultCount = new Label
        {
            Text = "0 записей",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _resultCount.AddThemeFontSizeOverride("font_size", 11);
        _resultCount.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        tools.AddChild(_resultCount);

        _sortButton = new Button
        {
            Text = "Новые сверху",
            ToggleMode = true,
            ButtonPressed = true,
            CustomMinimumSize = new Vector2(132, 34)
        };
        _sortButton.Pressed += () =>
        {
            _newestFirst = _sortButton.ButtonPressed;
            _sortButton.Text = _newestFirst ? "Новые сверху" : "Старые сверху";
            Refresh();
        };
        tools.AddChild(_sortButton);

        _search = new LineEdit
        {
            PlaceholderText = "Поиск в летописи…",
            ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(270, 36)
        };
        _search.TextChanged += _ => Refresh();
        tools.AddChild(_search);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        _timeline = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _timeline.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(_timeline);
        UpdateFilterButtons();
    }

    private void Refresh()
    {
        if (_timeline is null || _world is null)
            return;

        Clear(_timeline);

        IEnumerable<DemoChronicleEntry> entries = _world.Chronicle;
        entries = entries.Where(MatchesFilter);

        var query = _search?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(query))
        {
            entries = entries.Where(entry =>
                entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.RelatedEntityId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Time.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = (_newestFirst
                ? entries.OrderByDescending(item => item.Day).ThenByDescending(item => item.Time)
                : entries.OrderBy(item => item.Day).ThenBy(item => item.Time))
            .ToList();

        if (_resultCount is not null)
            _resultCount.Text = $"{materialized.Count} записей";

        if (materialized.Count == 0)
        {
            var empty = new Label
            {
                Text = "По текущему фильтру записей нет.",
                CustomMinimumSize = new Vector2(0, 72),
                VerticalAlignment = VerticalAlignment.Center
            };
            empty.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
            _timeline.AddChild(empty);
            return;
        }

        int? currentDay = null;
        foreach (var entry in materialized)
        {
            if (currentDay != entry.Day)
            {
                currentDay = entry.Day;
                _timeline.AddChild(BuildDayHeader(entry.Day));
            }

            _timeline.AddChild(BuildEntry(entry));
        }
    }

    private void AddFilter(Container parent, string name)
    {
        var button = new Button
        {
            Text = name,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(92, 34)
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

    private bool MatchesFilter(DemoChronicleEntry entry)
    {
        return _activeFilter switch
        {
            "Мир" => entry.Category == DemoEventCategory.World,
            "Эволюция" => entry.Category == DemoEventCategory.Evolution,
            "Ветвления" => entry.Kind == DemoEventKind.Branching,
            "Новые виды" => entry.Kind == DemoEventKind.Speciation,
            "Вымирания" => entry.Kind == DemoEventKind.Extinction,
            "Катастрофы" => entry.Kind == DemoEventKind.Catastrophe,
            "Системные" => entry.Category == DemoEventCategory.System,
            "Важные" => entry.Severity == DemoEventSeverity.Important,
            _ => true
        };
    }

    private static Control BuildDayHeader(int day)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 30) };
        var dayLabel = new Label { Text = $"ДЕНЬ {day}" };
        dayLabel.AddThemeFontSizeOverride("font_size", 11);
        dayLabel.AddThemeColorOverride("font_color", EvolitPalette.EvolutionCyan);
        row.AddChild(dayLabel);
        row.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        return row;
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static Control BuildEntry(DemoChronicleEntry entry)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 78),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        margin.AddChild(content);

        var accent = Accent(entry.Category, entry.Severity);
        var icon = new TextureRect
        {
            Texture = EvolitIcons.Load(entry.IconPath),
            CustomMinimumSize = new Vector2(28, 28),
            Modulate = accent,
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(icon);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        content.AddChild(text);

        var titleRow = new HBoxContainer();
        text.AddChild(titleRow);

        var title = new Label { Text = entry.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 16);
        titleRow.AddChild(title);

        titleRow.AddChild(Badge(KindLabel(entry.Kind), accent));
        if (entry.Severity != DemoEventSeverity.Info)
            titleRow.AddChild(Badge(SeverityLabel(entry.Severity), SeverityColor(entry.Severity)));

        var description = new Label
        {
            Text = entry.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeFontSizeOverride("font_size", 12);
        description.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        text.AddChild(description);

        var metaText = string.Empty;
        if (!string.IsNullOrWhiteSpace(entry.Time))
            metaText = entry.Time;
        if (!string.IsNullOrWhiteSpace(entry.RelatedEntityId))
            metaText += (metaText.Length > 0 ? "\n" : "") + entry.RelatedEntityId;

        if (metaText.Length > 0)
        {
            var meta = new Label
            {
                Text = metaText,
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize = new Vector2(126, 0)
            };
            meta.AddThemeFontSizeOverride("font_size", 10);
            meta.AddThemeColorOverride("font_color", new Color(accent, 0.82f));
            content.AddChild(meta);
        }

        return panel;
    }

    private static Control Badge(string text, Color color)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(color, 0.08f),
            BorderColor = new Color(color, 0.32f),
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
        DemoEventKind.Speciation => "НОВЫЙ ВИД",
        DemoEventKind.Extinction => "ВЫМИРАНИЕ",
        DemoEventKind.Catastrophe => "КАТАСТРОФА",
        DemoEventKind.WorldCreated => "МИР",
        DemoEventKind.Observation => "НАБЛЮДЕНИЕ",
        DemoEventKind.Runtime => "СИСТЕМА",
        DemoEventKind.Save => "СОХРАНЕНИЕ",
        DemoEventKind.Speed => "ВРЕМЯ",
        _ => "СОБЫТИЕ"
    };

    private static string SeverityLabel(DemoEventSeverity severity) => severity switch
    {
        DemoEventSeverity.Warning => "ВНИМАНИЕ",
        DemoEventSeverity.Important => "ВАЖНО",
        _ => "INFO"
    };

    private static Color Accent(DemoEventCategory category, DemoEventSeverity severity)
    {
        if (severity == DemoEventSeverity.Warning)
            return EvolitPalette.WarmAlert;
        if (severity == DemoEventSeverity.Important)
            return EvolitPalette.WarmSand;

        return category switch
        {
            DemoEventCategory.Evolution => EvolitPalette.EvolutionCyan,
            DemoEventCategory.System => EvolitPalette.Slate,
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
