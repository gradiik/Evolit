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
    private VBoxContainer? _stack;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _stack = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _stack.AnchorLeft = 0.69f;
        _stack.AnchorRight = 0.94f;
        _stack.AnchorTop = 0.115f;
        _stack.AnchorBottom = 0.46f;
        _stack.Alignment = BoxContainer.AlignmentMode.Begin;
        AddChild(_stack);
    }

    public void ShowToast(string message, ToastKind kind = ToastKind.Info)
    {
        if (_stack is null)
            return;

        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var label = new Label
        {
            Text = message,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };

        label.AddThemeColorOverride("font_color", kind switch
        {
            ToastKind.Success => EvolitPalette.SoftAqua,
            ToastKind.Warning => EvolitPalette.WarmSand,
            ToastKind.Error => EvolitPalette.WarmAlert,
            _ => EvolitPalette.MistWhite
        });

        margin.AddChild(label);
        _stack.AddChild(panel);

        var lifetime = kind == ToastKind.Error ? 4.2 : 2.8;
        var tween = CreateTween();
        tween.TweenProperty(panel, "modulate", Colors.White, 0.16);
        tween.TweenInterval(lifetime);
        tween.TweenProperty(panel, "modulate", new Color(1, 1, 1, 0), 0.28);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }
}
