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
            return settings;

        var backup = _path + ".bak";
        if (TryLoad(backup, out settings))
        {
            AtomicFile.RestoreBackup(_path, out _);
            return settings;
        }

        return AppSettings.Default();
    }

    public bool Save(AppSettings settings, out string error)
    {
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
}
