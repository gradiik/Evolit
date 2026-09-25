using System.Collections.Generic;
using Godot;

namespace Evolit.Input;

public static class InputBindings
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["game_pause"] = "Esc",
        ["camera_up"] = "W / ↑",
        ["camera_down"] = "S / ↓",
        ["camera_left"] = "A / ←",
        ["camera_right"] = "D / →",
        ["camera_zoom_in"] = "Колесо вверх",
        ["camera_zoom_out"] = "Колесо вниз",
        ["simulation_speed_1"] = "1",
        ["simulation_speed_2"] = "2",
        ["simulation_speed_3"] = "3",
        ["simulation_speed_max"] = "4",
        ["select"] = "ЛКМ",
        ["cancel"] = "Esc",
        ["debug_overlay"] = "F3",
        ["terrain_inspect"] = "T"
    };

    public static void EnsureDefaults()
    {
        EnsureKeys("game_pause", OS.FindKeycodeFromString("Escape"));
        EnsureKeys("camera_up", OS.FindKeycodeFromString("W"), OS.FindKeycodeFromString("Up"));
        EnsureKeys("camera_down", OS.FindKeycodeFromString("S"), OS.FindKeycodeFromString("Down"));
        EnsureKeys("camera_left", OS.FindKeycodeFromString("A"), OS.FindKeycodeFromString("Left"));
        EnsureKeys("camera_right", OS.FindKeycodeFromString("D"), OS.FindKeycodeFromString("Right"));
        EnsureMouse("camera_zoom_in", MouseButton.WheelUp);
        EnsureMouse("camera_zoom_out", MouseButton.WheelDown);
        EnsureKeys("simulation_speed_1", OS.FindKeycodeFromString("1"));
        EnsureKeys("simulation_speed_2", OS.FindKeycodeFromString("2"));
        EnsureKeys("simulation_speed_3", OS.FindKeycodeFromString("3"));
        EnsureKeys("simulation_speed_max", OS.FindKeycodeFromString("4"));
        EnsureMouse("select", MouseButton.Left);
        EnsureKeys("cancel", OS.FindKeycodeFromString("Escape"));
        EnsureKeys("debug_overlay", OS.FindKeycodeFromString("F3"));
        EnsureKeys("terrain_inspect", OS.FindKeycodeFromString("T"));
    }

    public static string Describe(string action)
    {
        return Labels.TryGetValue(action, out var label) ? label : "—";
    }

    private static void EnsureKeys(string action, params Key[] keys)
    {
        EnsureAction(action);
        if (InputMap.ActionGetEvents(action).Count > 0)
            return;

        foreach (var key in keys)
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
