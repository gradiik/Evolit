using System;
using Evolit.Save;

namespace Evolit.Session;

public sealed class GameSession
{
    public string SaveId { get; }
    public string WorldName { get; }
    public string Seed { get; }
    public string WorldSize { get; }
    public DateTimeOffset CreatedAt { get; }
    public double PlaytimeSeconds { get; private set; }
    public bool WasCreatedNew { get; }

    private GameSession(
        string saveId,
        string worldName,
        string seed,
        string worldSize,
        DateTimeOffset createdAt,
        double playtimeSeconds,
        bool wasCreatedNew)
    {
        SaveId = saveId;
        WorldName = worldName;
        Seed = seed;
        WorldSize = worldSize;
        CreatedAt = createdAt;
        PlaytimeSeconds = playtimeSeconds;
        WasCreatedNew = wasCreatedNew;
    }

    public static GameSession CreateNew(string worldName, string seed, string worldSize)
    {
        return new GameSession(
            Guid.NewGuid().ToString("N"),
            worldName,
            seed,
            worldSize,
            DateTimeOffset.UtcNow,
            0,
            true);
    }

    public static GameSession FromSave(SaveDocument document)
    {
        return new GameSession(
            document.SaveId,
            document.WorldName,
            document.Seed,
            document.WorldSize,
            document.CreatedAt,
            document.PlaytimeSeconds,
            false);
    }

    public void Tick(double delta)
    {
        if (delta > 0)
            PlaytimeSeconds += delta;
    }
}
