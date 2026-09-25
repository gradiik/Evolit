using System;
using System.IO;
using System.Text.Json;
using Evolit.Storage;
using Godot;

namespace Evolit.Settings;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public SettingsStore()
    {
        var directory = ProjectSettings.GlobalizePath("user://settings");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        if (TryLoad(_path, out var settings))
            return Normalize(settings);

        var backup = _path + ".bak";
        if (TryLoad(backup, out settings))
        {
            AtomicFile.RestoreBackup(_path, out _);
            return Normalize(settings);
        }

        return AppSettings.Default();
    }

    public bool Save(AppSettings settings, out string error)
    {
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        settings.KeyBindings ??= new();
        var json = JsonSerializer.Serialize(settings, _json);
        return AtomicFile.Write(_path, json, true, out error);
    }

    private bool TryLoad(string path, out AppSettings settings)
    {
        settings = AppSettings.Default();

        try
        {
            if (!File.Exists(path))
                return false;

            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, _json);
            if (parsed is null)
                return false;

            settings = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.KeyBindings ??= new();

        if (settings.SchemaVersion < 2)
        {
            // MotionMode did not exist in schema 1. Preserve the pre-existing
            // animated behaviour for users upgrading from older settings.
            settings.MotionMode = 2;
            settings.SchemaVersion = 2;
        }

        settings.DisplayMode = Math.Clamp(settings.DisplayMode, 0, 2);
        settings.Resolution = Math.Clamp(settings.Resolution, 0, 2);
        settings.DetailLevel = Math.Clamp(settings.DetailLevel, 0, 2);
        settings.WaterQuality = Math.Clamp(settings.WaterQuality, 0, 2);
        settings.UiScale = Math.Clamp(settings.UiScale, 0, 3);
        settings.TextSize = Math.Clamp(settings.TextSize, 0, 2);
        settings.MotionMode = Math.Clamp(settings.MotionMode, 0, 2);
        settings.CameraSpeed = Math.Clamp(settings.CameraSpeed, 1, 10);
        settings.MasterVolume = Math.Clamp(settings.MasterVolume, 0, 100);
        settings.MusicVolume = Math.Clamp(settings.MusicVolume, 0, 100);
        settings.EffectsVolume = Math.Clamp(settings.EffectsVolume, 0, 100);
        settings.UiVolume = Math.Clamp(settings.UiVolume, 0, 100);
        return settings;
    }
}
