using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Evolit.Input;

public readonly record struct RebindableAction(string Id, string Label);

public static class InputBindings
{
    public static readonly IReadOnlyList<RebindableAction> RebindableActions =
    [
        new("camera_up", "Камера вверх"),
        new("camera_down", "Камера вниз"),
        new("camera_left", "Камера влево"),
        new("camera_right", "Камера вправо"),
        new("camera_zoom_in", "Приблизить"),
        new("camera_zoom_out", "Отдалить"),
        new("simulation_speed_1", "Скорость 1×"),
        new("simulation_speed_2", "Скорость 4×"),
        new("simulation_speed_3", "Скорость 16×"),
        new("simulation_speed_max", "Скорость MAX"),
        new("select", "Выбор объекта"),
        new("terrain_inspect", "Инспектор клетки"),
        new("game_pause", "Пауза / назад"),
        new("debug_overlay", "Технический оверлей")
    ];

    public static void EnsureDefaults()
    {
        foreach (var action in RebindableActions)
        {
            EnsureAction(action.Id);
            if (InputMap.ActionGetEvents(action.Id).Count == 0)
                AddDefaults(action.Id);
        }

        EnsureAction("cancel");
        if (InputMap.ActionGetEvents("cancel").Count == 0)
            AddKey("cancel", "Escape");
    }

    public static void ApplyOverrides(IReadOnlyDictionary<string, string>? overrides)
    {
        EnsureDefaults();

        foreach (var action in RebindableActions)
        {
            InputMap.ActionEraseEvents(action.Id);

            if (overrides is not null
                && overrides.TryGetValue(action.Id, out var encoded)
                && TryDecode(encoded, out var @event))
            {
                InputMap.ActionAddEvent(action.Id, @event);
            }
            else
            {
                AddDefaults(action.Id);
            }
        }

        InputMap.ActionEraseEvents("cancel");
        AddKey("cancel", "Escape");
    }

    public static string Describe(string action)
    {
        var events = InputMap.ActionGetEvents(action);
        if (events.Count == 0)
            return "—";

        return string.Join(" / ", events.Select(DescribeEvent));
    }

    public static string Describe(
        string action,
        IReadOnlyDictionary<string, string> overrides)
    {
        if (overrides.TryGetValue(action, out var encoded))
            return DescribeEncoded(encoded);

        var defaults = DefaultEncodings(action);
        return defaults.Count == 0
            ? "—"
            : string.Join(" / ", defaults.Select(DescribeEncoded));
    }

    public static string? Encode(InputEvent @event)
    {
        if (@event is InputEventKey key)
        {
            var code = key.PhysicalKeycode != Key.None
                ? key.PhysicalKeycode
                : key.Keycode;
            if (code == Key.None)
                return null;

            return $"key:{(long)code}";
        }

        if (@event is InputEventMouseButton mouse)
            return $"mouse:{(int)mouse.ButtonIndex}";

        return null;
    }

    public static string? FindConflict(
        string action,
        string encoded,
        IReadOnlyDictionary<string, string> overrides)
    {
        foreach (var candidate in RebindableActions)
        {
            if (candidate.Id == action)
                continue;

            var bindings = overrides.TryGetValue(candidate.Id, out var custom)
                ? new[] { custom }
                : DefaultEncodings(candidate.Id);

            if (bindings.Contains(encoded, StringComparer.Ordinal))
                return candidate.Id;
        }

        return null;
    }

    public static string? GetPrimaryEncoding(
        string action,
        IReadOnlyDictionary<string, string> overrides)
    {
        if (overrides.TryGetValue(action, out var custom))
            return custom;

        return DefaultEncodings(action).FirstOrDefault();
    }

    public static string FriendlyName(string action)
    {
        return RebindableActions
            .FirstOrDefault(item => item.Id == action)
            .Label ?? action;
    }

    public static string DescribeEncoded(string encoded)
    {
        if (!TryDecode(encoded, out var @event))
            return "—";

        return DescribeEvent(@event);
    }

    private static string DescribeEvent(InputEvent @event)
    {
        if (@event is InputEventKey key)
        {
            var code = key.PhysicalKeycode != Key.None
                ? key.PhysicalKeycode
                : key.Keycode;
            return code.ToString();
        }

        if (@event is InputEventMouseButton mouse)
        {
            return mouse.ButtonIndex switch
            {
                MouseButton.Left => "ЛКМ",
                MouseButton.Right => "ПКМ",
                MouseButton.Middle => "СКМ",
                MouseButton.WheelUp => "Колесо вверх",
                MouseButton.WheelDown => "Колесо вниз",
                _ => $"Mouse {(int)mouse.ButtonIndex}"
            };
        }

        return @event.AsText();
    }

    private static bool TryDecode(string encoded, out InputEvent @event)
    {
        @event = new InputEventKey();

        if (encoded.StartsWith("key:", StringComparison.Ordinal)
            && long.TryParse(encoded[4..], out var keyValue))
        {
            @event = new InputEventKey
            {
                PhysicalKeycode = (Key)keyValue
            };
            return true;
        }

        if (encoded.StartsWith("mouse:", StringComparison.Ordinal)
            && int.TryParse(encoded[6..], out var mouseValue))
        {
            @event = new InputEventMouseButton
            {
                ButtonIndex = (MouseButton)mouseValue
            };
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> DefaultEncodings(string action)
    {
        return action switch
        {
            "camera_up" => [KeyEncoding("W"), KeyEncoding("Up")],
            "camera_down" => [KeyEncoding("S"), KeyEncoding("Down")],
            "camera_left" => [KeyEncoding("A"), KeyEncoding("Left")],
            "camera_right" => [KeyEncoding("D"), KeyEncoding("Right")],
            "camera_zoom_in" => [MouseEncoding(MouseButton.WheelUp)],
            "camera_zoom_out" => [MouseEncoding(MouseButton.WheelDown)],
            "simulation_speed_1" => [KeyEncoding("1")],
            "simulation_speed_2" => [KeyEncoding("2")],
            "simulation_speed_3" => [KeyEncoding("3")],
            "simulation_speed_max" => [KeyEncoding("4")],
            "select" => [MouseEncoding(MouseButton.Left)],
            "terrain_inspect" => [KeyEncoding("T")],
            "game_pause" => [KeyEncoding("Escape")],
            "debug_overlay" => [KeyEncoding("F3")],
            _ => []
        };
    }

    private static void AddDefaults(string action)
    {
        foreach (var encoded in DefaultEncodings(action))
        {
            if (TryDecode(encoded, out var @event))
                InputMap.ActionAddEvent(action, @event);
        }
    }

    private static string KeyEncoding(string key)
    {
        return $"key:{(long)OS.FindKeycodeFromString(key)}";
    }

    private static string MouseEncoding(MouseButton button)
    {
        return $"mouse:{(int)button}";
    }

    private static void AddKey(string action, string key)
    {
        InputMap.ActionAddEvent(
            action,
            new InputEventKey
            {
                PhysicalKeycode = OS.FindKeycodeFromString(key)
            });
    }

    private static void EnsureAction(string action)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);
    }
}
