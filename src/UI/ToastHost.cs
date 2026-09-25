using System.Collections.Generic;
using Godot;

namespace Evolit.UI;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed partial class ToastHost : Control
{
    private const int MaxVisible = 4;

    private readonly Queue<Control> _items = new();
    private VBoxContainer? _stack;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _stack = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _stack.AnchorLeft = 0.68f;
        _stack.AnchorRight = 0.96f;
        _stack.AnchorTop = 0.105f;
        _stack.AnchorBottom = 0.50f;
        _stack.Alignment = BoxContainer.AlignmentMode.Begin;
        _stack.AddThemeConstantOverride("separation", UiMetrics.Space(7));
        AddChild(_stack);
    }

    public void ShowToast(
        string message,
        ToastKind kind = ToastKind.Info)
    {
        if (_stack is null)
            return;

        while (_items.Count >= MaxVisible)
        {
            var oldest = _items.Dequeue();
            if (GodotObject.IsInstanceValid(oldest))
                oldest.QueueFree();
        }

        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = UiMetrics.Size(280, 0)
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(14));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(14));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(10));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(10));
        panel.AddChild(margin);

        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", UiMetrics.Space(9));
        margin.AddChild(row);

        var accentColor = kind switch
        {
            ToastKind.Success => EvolitPalette.SoftAqua,
            ToastKind.Warning => EvolitPalette.WarmSand,
            ToastKind.Error => EvolitPalette.WarmAlert,
            _ => EvolitPalette.EvolutionCyan
        };

        row.AddChild(new ColorRect
        {
            Color = accentColor,
            CustomMinimumSize = UiMetrics.Size(3, 0),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var label = new Label
        {
            Text = message,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        label.AddThemeColorOverride("font_color", accentColor);
        row.AddChild(label);

        _stack.AddChild(panel);
        _items.Enqueue(panel);

        var lifetime = kind == ToastKind.Error ? 4.2 : 2.8;

        if (UiMotion.Mode == UiMotionMode.Off)
        {
            var timer = GetTree().CreateTimer(lifetime);
            timer.Timeout += () => RemoveToast(panel);
            return;
        }

        var target = panel.Position;
        panel.Modulate = new Color(1, 1, 1, 0);
        if (UiMotion.Mode == UiMotionMode.Full)
            panel.Position += new Vector2(UiMetrics.Px(14), 0);

        var tween = CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(panel, "modulate", Colors.White, UiMotion.Normal);

        if (UiMotion.Mode == UiMotionMode.Full)
            tween.Parallel().TweenProperty(
                panel,
                "position",
                target,
                UiMotion.Normal);

        tween.TweenInterval(lifetime);
        tween.SetEase(Tween.EaseType.In);
        tween.TweenProperty(
            panel,
            "modulate",
            new Color(1, 1, 1, 0),
            UiMotion.Slow);
        tween.TweenCallback(
            Callable.From(() => RemoveToast(panel)));
    }

    private void RemoveToast(Control panel)
    {
        if (!GodotObject.IsInstanceValid(panel))
            return;

        panel.QueueFree();

        var next = new Queue<Control>();
        while (_items.Count > 0)
        {
            var item = _items.Dequeue();
            if (GodotObject.IsInstanceValid(item) && item != panel)
                next.Enqueue(item);
        }

        while (next.Count > 0)
            _items.Enqueue(next.Dequeue());
    }
}
