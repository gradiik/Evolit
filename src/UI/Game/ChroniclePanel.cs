using System;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class ChroniclePanel : Control
{
    public event Action? CloseRequested;

    private DemoWorldDataProvider? _world;
    private VBoxContainer? _timeline;

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

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 120);
        outer.AddThemeConstantOverride("margin_right", 120);
        outer.AddThemeConstantOverride("margin_top", 64);
        outer.AddThemeConstantOverride("margin_bottom", 72);
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

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        _timeline = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _timeline.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_timeline);
    }

    private void Refresh()
    {
        if (_timeline is null || _world is null)
            return;

        foreach (var child in _timeline.GetChildren())
        {
            _timeline.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var entry in _world.Chronicle.OrderBy(item => item.Day))
            _timeline.AddChild(BuildEntry(entry));
    }

    private static Control BuildEntry(DemoChronicleEntry entry)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 84) };
        row.AddThemeConstantOverride("separation", 14);

        var day = new Label
        {
            Text = $"ДЕНЬ\n{entry.Day}",
            CustomMinimumSize = new Vector2(72, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        day.AddThemeFontSizeOverride("font_size", 12);
        day.AddThemeColorOverride("font_color", EvolitPalette.EvolutionCyan);
        row.AddChild(day);

        var markerColumn = new VBoxContainer { CustomMinimumSize = new Vector2(18, 0), Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(markerColumn);

        markerColumn.AddChild(new ColorRect
        {
            Color = CategoryColor(entry.Category),
            CustomMinimumSize = new Vector2(9, 9),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        row.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var content = new HBoxContainer();
        margin.AddChild(content);

        var icon = new TextureRect
        {
            Texture = EvolitIcons.Load(entry.IconPath),
            CustomMinimumSize = new Vector2(26, 26),
            Modulate = CategoryColor(entry.Category),
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(icon);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(text);

        var title = new Label { Text = entry.Title };
        title.AddThemeFontSizeOverride("font_size", 16);
        text.AddChild(title);

        var description = new Label { Text = entry.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        description.AddThemeFontSizeOverride("font_size", 12);
        description.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        text.AddChild(description);

        return row;
    }

    private static Color CategoryColor(DemoEventCategory category)
    {
        return category switch
        {
            DemoEventCategory.Evolution => EvolitPalette.EvolutionCyan,
            DemoEventCategory.System => EvolitPalette.Slate,
            _ => EvolitPalette.YoungLeaf
        };
    }
}
