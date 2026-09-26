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
    public const string CurrentVersion = "0.0.7";
    public const string PreviousVersion = "0.0.6";
    public const string PreviousCommit = "e6c8407ac642cf79cfcd3d711fcd24a96ef3beae";
    public const string BaselineVersion = "0.0.4";
    public const string BaselineCommit = "268fce1dd4d1d4b9f4d644a789e3110868201ff6";
    public const string LegacyVersion = "0.0.3";
    public const string LegacyCommit = "04e1cfa88de983dca6ed2ad582c9af7a4a010dcc";
    public const int VisibleHistoryLimit = 5;

    private static readonly IReadOnlyList<AppVersionRecord> Records =
    [
        new(
            CurrentVersion,
            "26 сентября 2026",
            "0.0.7: dynamic physical environment, hydrology, atmosphere/climate foundation, resources/substrate, derived environmental regions и bootstrap stabilization.",
            "Текущая разработка",
            true),
        new(
            PreviousVersion,
            "26 сентября 2026",
            "0.0.6: Evolit Core foundation, deterministic fixed-step simulation, Godot-independent topology/environment, data-oriented organisms, genetics, lineages, snapshots и headless benchmark.",
            PreviousCommit,
            false),
        new(
            "0.0.5",
            "26 сентября 2026",
            "0.0.5: гексагональная карта и климатические данные, оптимизированный chunk renderer, terrain inspector на T, исправленные save/load и UI-flow.",
            "d8a0280ec7293931842049e0f51bef18860e5fad",
            false),
        new(
            BaselineVersion,
            "21 сентября 2026",
            "0.0.4: переработанный HUD, 1×/4×/16×/MAX, event popups, улучшенная камера, графики, энциклопедия и летопись.",
            BaselineCommit,
            false),
        new(
            LegacyVersion,
            "20 сентября 2026",
            "Evolution UI, игровое время, древо, события и статистика.",
            LegacyCommit,
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
