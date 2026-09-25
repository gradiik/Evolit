using System.Collections.Generic;

namespace Evolit.Settings;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; }

    public int DisplayMode { get; set; }
    public int Resolution { get; set; }
    public bool VSync { get; set; }
    public int DetailLevel { get; set; }
    public int WaterQuality { get; set; }

    // Kept for backwards-compatible settings files. These options are not
    // exposed until the corresponding renderer systems exist.
    public int ShadowQuality { get; set; }
    public int EffectsQuality { get; set; }

    public double MasterVolume { get; set; }
    public double MusicVolume { get; set; }
    public double EffectsVolume { get; set; }
    public double UiVolume { get; set; }
    public bool Mute { get; set; }

    public int UiScale { get; set; }
    public int TextSize { get; set; }
    public bool Tooltips { get; set; }
    public bool ShowFps { get; set; }
    public bool ShowPerformance { get; set; }
    public int MotionMode { get; set; }

    // Kept for migration. Game flow currently pauses implicitly whenever the
    // GameScreen leaves the tree, so exposing this toggle would be misleading.
    public bool AutoPause { get; set; }

    public double CameraSpeed { get; set; }
    public bool SmoothZoom { get; set; }

    public Dictionary<string, string> KeyBindings { get; set; } = new();

    public static AppSettings Default()
    {
        return new AppSettings
        {
            SchemaVersion = CurrentSchemaVersion,
            DisplayMode = 0,
            Resolution = 1,
            VSync = true,
            DetailLevel = 1,
            WaterQuality = 1,
            ShadowQuality = 2,
            EffectsQuality = 1,
            MasterVolume = 80,
            MusicVolume = 65,
            EffectsVolume = 80,
            UiVolume = 75,
            Mute = false,
            UiScale = 1,
            TextSize = 1,
            Tooltips = true,
            ShowFps = false,
            ShowPerformance = false,
            MotionMode = 2,
            AutoPause = true,
            CameraSpeed = 5,
            SmoothZoom = true,
            KeyBindings = new Dictionary<string, string>()
        };
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            SchemaVersion = SchemaVersion,
            DisplayMode = DisplayMode,
            Resolution = Resolution,
            VSync = VSync,
            DetailLevel = DetailLevel,
            WaterQuality = WaterQuality,
            ShadowQuality = ShadowQuality,
            EffectsQuality = EffectsQuality,
            MasterVolume = MasterVolume,
            MusicVolume = MusicVolume,
            EffectsVolume = EffectsVolume,
            UiVolume = UiVolume,
            Mute = Mute,
            UiScale = UiScale,
            TextSize = TextSize,
            Tooltips = Tooltips,
            ShowFps = ShowFps,
            ShowPerformance = ShowPerformance,
            MotionMode = MotionMode,
            AutoPause = AutoPause,
            CameraSpeed = CameraSpeed,
            SmoothZoom = SmoothZoom,
            KeyBindings = new Dictionary<string, string>(KeyBindings)
        };
    }
}
