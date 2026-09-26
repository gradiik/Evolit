using System;
using System.Text.Json;

namespace Evolit.Core;

public static class CoreSnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static string Serialize(CoreSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static CoreSimulationSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Snapshot JSON is empty.", nameof(json));
        return JsonSerializer.Deserialize<CoreSimulationSnapshot>(json, Options)
            ?? throw new InvalidOperationException("Snapshot JSON did not contain a Core snapshot.");
    }
}
