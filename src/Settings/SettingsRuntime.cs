using System;
using Godot;

namespace Evolit.Settings;

public readonly record struct GraphicsQualityProfile(
    int DetailLevel,
    int DecorativePatchCount,
    int SelectionArcSegments,
    bool ExtraEntityDetails,
    int WaterDetail)
{
    public static GraphicsQualityProfile From(AppSettings settings)
    {
        var level = Math.Clamp(settings.DetailLevel, 0, 2);
        var water = Math.Clamp(settings.WaterQuality, 0, 2);
        return level switch
        {
            0 => new GraphicsQualityProfile(0, 2, 28, false, water),
            2 => new GraphicsQualityProfile(2, 6, 72, true, water),
            _ => new GraphicsQualityProfile(1, 4, 48, false, water)
        };
    }
}

public static class SettingsRuntime
{
    private static readonly Vector2I[] Resolutions =
    [
        new Vector2I(1280, 720),
        new Vector2I(1920, 1080),
        new Vector2I(2560, 1440)
    ];

    public static void Apply(AppSettings settings)
    {
        ApplyDisplay(settings);
        ApplyAudio(settings);
    }

    public static bool HasAudioBus(string name)
    {
        return AudioServer.GetBusIndex(name) >= 0;
    }

    private static void ApplyDisplay(AppSettings settings)
    {
        var displayMode = Math.Clamp(settings.DisplayMode, 0, 2);
        switch (displayMode)
        {
            case 2:
                DisplayServer.WindowSetFlag(
                    DisplayServer.WindowFlags.Borderless,
                    false);
                DisplayServer.WindowSetMode(
                    DisplayServer.WindowMode.Fullscreen);
                break;
            case 1:
                DisplayServer.WindowSetMode(
                    DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(
                    DisplayServer.WindowFlags.Borderless,
                    true);
                ApplyResolution(settings.Resolution);
                break;
            default:
                DisplayServer.WindowSetMode(
                    DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(
                    DisplayServer.WindowFlags.Borderless,
                    false);
                ApplyResolution(settings.Resolution);
                break;
        }

        DisplayServer.WindowSetVsyncMode(
            settings.VSync
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);
    }

    private static void ApplyResolution(int resolutionIndex)
    {
        var index = Math.Clamp(resolutionIndex, 0, Resolutions.Length - 1);
        DisplayServer.WindowSetSize(Resolutions[index]);
    }

    private static void ApplyAudio(AppSettings settings)
    {
        SetBus("Master", settings.MasterVolume, settings.Mute);
        SetBus("Music", settings.MusicVolume, settings.Mute);
        SetBus("Effects", settings.EffectsVolume, settings.Mute);
        SetBus("UI", settings.UiVolume, settings.Mute);
    }

    private static void SetBus(string name, double volume, bool mute)
    {
        var index = AudioServer.GetBusIndex(name);
        if (index < 0)
            return;

        var linear = (float)Math.Clamp(volume / 100.0, 0.0, 1.0);
        var db = linear <= 0.0001f ? -80f : Mathf.LinearToDb(linear);
        AudioServer.SetBusVolumeDb(index, db);
        AudioServer.SetBusMute(index, mute);
    }
}
