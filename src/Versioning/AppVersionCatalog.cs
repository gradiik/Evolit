using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Evolit.Versioning;

public sealed record AppVersionRecord(
    string Version,
    string Date,
    string Description,
    string Commit,
    bool IsCurrent);

public static class AppVersionCatalog
{
    public const string CurrentVersion = "0.0.4";
    public const string BaselineVersion = "0.0.3";
    public const string BaselineCommit = "04e1cfa88de983dca6ed2ad582c9af7a4a010dcc";
    public const int VisibleHistoryLimit = 5;

    private static readonly IReadOnlyList<AppVersionRecord> Records =
    [
        new(
            CurrentVersion,
            "21 сентября 2026",
            "Полировка UI 0.0.4: интерактивные графики, лаборатория species portraits, улучшенная камера, структурированные события и летопись.",
            "Текущая сборка",
            true),
        new(
            BaselineVersion,
            "20 сентября 2026",
            "Evolution UI, игровое время, древо, события и статистика.",
            BaselineCommit,
            false)
    ];

    public static IReadOnlyList<AppVersionRecord> VisibleVersions =>
        Records.Take(VisibleHistoryLimit).ToArray();
}

public sealed class VersionSelectionStore
{
    private const string Path = "user://version-selection.cfg";
    private const string Section = "rollback";
    private const string Key = "target_version";

    public string? LoadTarget()
    {
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok)
            return null;

        var value = config.GetValue(Section, Key, string.Empty).AsString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public Error SaveTarget(string version)
    {
        var config = new ConfigFile();
        config.SetValue(Section, Key, version);
        return config.Save(Path);
    }
}
