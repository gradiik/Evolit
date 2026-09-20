using System.Collections.Generic;
using Godot;

namespace Evolit.Input;

public static class InputBindings
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["game_pause"] = "Esc",
        ["camera_up"] = "W",
        ["camera_down"] = "S",
        ["camera_left"] = "A",
        ["camera_right"] = "D",
        ["camera_zoom_in"] = "Колесо вверх",
        ["camera_zoom_out"] = "Колесо вниз",
        ["simulation_speed_1"] = "1",
        ["simulation_speed_2"] = "2",
        ["simulation_speed_3"] = "3",
        ["select"] = "ЛКМ",
        ["cancel"] = "Esc",
        ["debug_overlay"] = "F3"
    };

    public static void EnsureDefaults()
    {
        EnsureKey("game_pause", OS.FindKeycodeFromString("Escape"));
        EnsureKey("camera_up", OS.FindKeycodeFromString("W"));
        EnsureKey("camera_down", OS.FindKeycodeFromString("S"));
        EnsureKey("camera_left", OS.FindKeycodeFromString("A"));
        EnsureKey("camera_right", OS.FindKeycodeFromString("D"));
        EnsureMouse("camera_zoom_in", MouseButton.WheelUp);
        EnsureMouse("camera_zoom_out", MouseButton.WheelDown);
        EnsureKey("simulation_speed_1", OS.FindKeycodeFromString("1"));
        EnsureKey("simulation_speed_2", OS.FindKeycodeFromString("2"));
        EnsureKey("simulation_speed_3", OS.FindKeycodeFromString("3"));
        EnsureMouse("select", MouseButton.Left);
        EnsureKey("cancel", OS.FindKeycodeFromString("Escape"));
        EnsureKey("debug_overlay", OS.FindKeycodeFromString("F3"));
    }

    public static string Describe(string action)
    {
        return Labels.TryGetValue(action, out var label) ? label : "—";
    }

    private static void EnsureKey(string action, Key key)
    {
        EnsureAction(action);
        if (InputMap.ActionGetEvents(action).Count > 0)
            return;

        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private static void EnsureMouse(string action, MouseButton button)
    {
        EnsureAction(action);
        if (InputMap.ActionGetEvents(action).Count > 0)
            return;

        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }

    private static void EnsureAction(string action)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);
    }
}
