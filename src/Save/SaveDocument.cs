using System;
using System.Collections.Generic;

namespace Evolit.Save;

public sealed class SaveDocument
{
    public int SchemaVersion { get; set; } = SaveManager.SchemaVersion;
    public string SaveId { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public string Seed { get; set; } = string.Empty;
    public string WorldSize { get; set; } = "Средний";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public double PlaytimeSeconds { get; set; }
    public string GameVersion { get; set; } = SaveManager.GameVersion;
    public string SaveType { get; set; } = SaveManager.ManualType;
    public int? AutosaveIndex { get; set; }
    public Dictionary<string, string> Payload { get; set; } = new();
}
