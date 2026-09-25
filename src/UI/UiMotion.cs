using System;
using System.Linq;
using Evolit.Settings;
using Godot;

namespace Evolit.UI;

public enum UiMotionMode
{
    Off = 0,
    Reduced = 1,
    Full = 2
}

public static class UiMotion
{
    public static UiMotionMode Mode { get; private set; } = UiMotionMode.Full;

    public static double Fast => Mode switch
    {
        UiMotionMode.Off => 0,
        UiMotionMode.Reduced => 0.07,
        _ => 0.11
    };

    public static double Normal => Mode switch
    {
        UiMotionMode.Off => 0,
        UiMotionMode.Reduced => 0.10,
        _ => 0.17
    };

    public static double Slow => Mode switch
    {
        UiMotionMode.Off => 0,
        UiMotionMode.Reduced => 0.14,
        _ => 0.24
    };

    public static void Configure(AppSettings settings)
    {
        Mode = (UiMotionMode)Math.Clamp(settings.MotionMode, 0, 2);
    }

    public static void FadeIn(Control control, Vector2? fullMotionOffset = null)
    {
        control.Modulate = Colors.White;

        if (Mode == UiMotionMode.Off)
            return;

        var basePosition = control.Position;
        control.Modulate = new Color(1, 1, 1, 0);

        if (Mode == UiMotionMode.Full && fullMotionOffset.HasValue)
            control.Position = basePosition + fullMotionOffset.Value;

        var tween = control.CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        tween.TweenProperty(control, "modulate", Colors.White, Normal);

        if (Mode == UiMotionMode.Full && fullMotionOffset.HasValue)
            tween.Parallel().TweenProperty(control, "position", basePosition, Normal);
    }

    public static void ReplaceScreen(Control host, Control next)
    {
        var previous = host.GetChildren().OfType<Control>().ToArray();

        if (Mode == UiMotionMode.Off || previous.Length == 0)
        {
            foreach (var old in previous)
            {
                host.RemoveChild(old);
                old.QueueFree();
            }

            host.AddChild(next);
            next.Modulate = Colors.White;
            return;
        }

        foreach (var old in previous)
        {
            old.ProcessMode = Node.ProcessModeEnum.Disabled;
            old.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        var targetPosition = next.Position;
        next.Modulate = new Color(1, 1, 1, 0);
        if (Mode == UiMotionMode.Full)
            next.Position += new Vector2(0, UiMetrics.Px(8));

        host.AddChild(next);

        var tween = host.CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        tween.TweenProperty(next, "modulate", Colors.White, Normal);

        if (Mode == UiMotionMode.Full)
            tween.Parallel().TweenProperty(next, "position", targetPosition, Normal);

        foreach (var old in previous)
            tween.Parallel().TweenProperty(old, "modulate", new Color(1, 1, 1, 0), Fast);

        tween.TweenCallback(Callable.From(() =>
        {
            foreach (var old in previous)
            {
                if (!GodotObject.IsInstanceValid(old))
                    continue;
                if (old.GetParent() == host)
                    host.RemoveChild(old);
                old.QueueFree();
            }
        }));
    }

    public static void BindTree(Node node)
    {
        if (node is Control control && !UiMetrics.TooltipsEnabled)
            control.TooltipText = string.Empty;

        if (node is BaseButton button)
            BindButton(button);

        foreach (var child in node.GetChildren())
            BindTree(child);
    }

    public static void BindHover(Control control)
    {
        if (control.HasMeta("_evolit_hover_bound"))
            return;

        control.SetMeta("_evolit_hover_bound", true);
        control.Resized += () => control.PivotOffset = control.Size * 0.5f;
        control.MouseEntered += () => AnimateScale(control, 1.010f);
        control.MouseExited += () => AnimateScale(control, 1f);
    }

    public static void BindButton(BaseButton button)
    {
        if (button.HasMeta("_evolit_motion_bound"))
            return;

        button.SetMeta("_evolit_motion_bound", true);
        button.Resized += () => button.PivotOffset = button.Size * 0.5f;
        button.MouseEntered += () => AnimateScale(button, 1.012f);
        button.MouseExited += () => AnimateScale(button, 1f);
        button.ButtonDown += () => AnimateScale(button, 0.988f);
        button.ButtonUp += () => AnimateScale(button, 1f);
    }

    private static void AnimateScale(Control control, float scale)
    {
        if (Mode != UiMotionMode.Full)
        {
            control.Scale = Vector2.One;
            return;
        }

        control.CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad)
            .TweenProperty(
                control,
                "scale",
                new Vector2(scale, scale),
                Fast);
    }
}
