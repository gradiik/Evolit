using System;
using System.Collections.Generic;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class WorldEventPopupHost : Control
{
    private const int MaxVisible = 3;

    private readonly List<PopupItem> _items = new();
    private VBoxContainer? _stack;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _stack = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.End
        };
        _stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _stack.AddThemeConstantOverride("separation", 7);
        AddChild(_stack);
    }

    public void ShowEvent(DemoEventEntry entry)
    {
        if (_stack is null || !ShouldDisplay(entry))
            return;

        while (_items.Count >= MaxVisible)
            RemoveImmediately(_items[0]);

        var wrapper = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            CustomMinimumSize = UiMetrics.Size(400, 84)
        };

        var panel = BuildPanel(entry);
        panel.Scale = UiMotion.Mode == UiMotionMode.Full
            ? new Vector2(0.985f, 0.985f)
            : Vector2.One;
        panel.Modulate = UiMotion.Mode == UiMotionMode.Off
            ? Colors.White
            : new Color(1, 1, 1, 0);
        wrapper.AddChild(panel);
        _stack.AddChild(wrapper);

        var item = new PopupItem(wrapper, panel);
        _items.Add(item);

        if (UiMotion.Mode != UiMotionMode.Off)
        {
            var appear = CreateTween()
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);

            if (UiMotion.Mode == UiMotionMode.Full)
                appear.TweenProperty(panel, "scale", Vector2.One, UiMotion.Normal);

            appear.Parallel().TweenProperty(
                panel,
                "modulate",
                Colors.White,
                UiMotion.Normal);
        }

        var lifetime = entry.Severity switch
        {
            DemoEventSeverity.Important => 8.0,
            DemoEventSeverity.Warning => 5.5,
            _ => 3.2
        };

        var lifetimeTween = CreateTween();
        lifetimeTween.TweenInterval(lifetime);
        lifetimeTween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(wrapper))
                BeginDismiss(item, 0.24);
        }));
    }

    private static bool ShouldDisplay(DemoEventEntry entry)
    {
        if (entry.Severity is DemoEventSeverity.Warning or DemoEventSeverity.Important)
            return true;

        return entry.Kind is DemoEventKind.Branching
            or DemoEventKind.Speciation
            or DemoEventKind.Extinction
            or DemoEventKind.Catastrophe;
    }

    private Control BuildPanel(DemoEventEntry entry)
    {
        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = UiMetrics.Size(390, 78)
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.96f));

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 9);
        margin.AddThemeConstantOverride("margin_bottom", 9);
        panel.AddChild(margin);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        var accent = Accent(entry);
        row.AddChild(new ColorRect
        {
            Color = accent,
            CustomMinimumSize = UiMetrics.Size(4, 0),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var icon = new TextureRect
        {
            Texture = EvolitIcons.Load(entry.IconPath),
            Modulate = accent,
            CustomMinimumSize = UiMetrics.Size(26, 26),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(icon);

        var text = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);

        var titleRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        text.AddChild(titleRow);

        var title = new Label
        {
            Text = entry.Title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(14));
        title.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        titleRow.AddChild(title);

        var severity = new Label
        {
            Text = SeverityLabel(entry.Severity),
            MouseFilter = MouseFilterEnum.Ignore
        };
        severity.AddThemeFontSizeOverride("font_size", UiMetrics.Font(9));
        severity.AddThemeColorOverride("font_color", accent);
        titleRow.AddChild(severity);

        var description = new Label
        {
            Text = entry.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        description.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        description.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        text.AddChild(description);

        var meta = new Label
        {
            Text = $"День {entry.Day} · {entry.Time}",
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = UiMetrics.Size(86, 0),
            MouseFilter = MouseFilterEnum.Ignore
        };
        meta.AddThemeFontSizeOverride("font_size", UiMetrics.Font(9));
        meta.AddThemeColorOverride("font_color", new Color(accent, 0.86f));
        row.AddChild(meta);

        return panel;
    }

    private void RemoveImmediately(PopupItem item)
    {
        if (!_items.Remove(item))
            return;

        if (IsInstanceValid(item.Wrapper))
            item.Wrapper.QueueFree();
    }

    private void BeginDismiss(PopupItem item, double duration)
    {
        if (!_items.Remove(item))
            return;

        if (!IsInstanceValid(item.Wrapper) || !IsInstanceValid(item.Panel))
            return;

        if (UiMotion.Mode == UiMotionMode.Off)
        {
            item.Wrapper.QueueFree();
            return;
        }

        duration = UiMotion.Slow;
        var exit = CreateTween()
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        exit.Parallel().TweenProperty(
            item.Panel,
            "scale",
            new Vector2(0.985f, 0.985f),
            duration);
        exit.Parallel().TweenProperty(
            item.Panel,
            "modulate",
            new Color(1, 1, 1, 0),
            duration);

        exit.TweenCallback(Callable.From(() =>
        {
            if (!IsInstanceValid(item.Panel) || !IsInstanceValid(item.Wrapper))
                return;

            item.Panel.Visible = false;
            var collapse = CreateTween()
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Quad);
            collapse.TweenProperty(
                item.Wrapper,
                "custom_minimum_size",
                Vector2.Zero,
                UiMotion.Fast);
            collapse.TweenCallback(Callable.From(() =>
            {
                if (!IsInstanceValid(item.Wrapper))
                    return;
                item.Wrapper.QueueFree();
            }));
        }));
    }

    private static string SeverityLabel(DemoEventSeverity severity)
    {
        return severity switch
        {
            DemoEventSeverity.Important => "ВАЖНО",
            DemoEventSeverity.Warning => "ВНИМАНИЕ",
            _ => "СОБЫТИЕ"
        };
    }

    private static Color Accent(DemoEventEntry entry)
    {
        if (entry.Severity == DemoEventSeverity.Important)
            return EvolitPalette.WarmSand;
        if (entry.Severity == DemoEventSeverity.Warning)
            return EvolitPalette.WarmAlert;

        return entry.Kind switch
        {
            DemoEventKind.Extinction => EvolitPalette.WarmAlert,
            DemoEventKind.Catastrophe => EvolitPalette.WarmAlert,
            DemoEventKind.Speciation => EvolitPalette.EvolutionCyan,
            DemoEventKind.Branching => EvolitPalette.EvolutionCyan,
            _ => EvolitPalette.SoftAqua
        };
    }

    private sealed record PopupItem(Control Wrapper, Control Panel);
}
