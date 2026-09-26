using System;
using Evolit.Save;
using Evolit.Core;

namespace Evolit.Session;

public sealed class GameSession
{
    public string SaveId { get; }
    public string WorldName { get; }
    public string Seed { get; }
    public string WorldSize { get; }
    public WorldLandAmount LandAmount { get; }
    public WorldClimate Climate { get; }
    public GeologicalActivity Geology { get; }
    public DateTimeOffset CreatedAt { get; }
    public double PlaytimeSeconds { get; private set; }
    public bool WasCreatedNew { get; }

    private GameSession(
        string saveId,
        string worldName,
        string seed,
        string worldSize,
        WorldLandAmount landAmount,
        WorldClimate climate,
        GeologicalActivity geology,
        DateTimeOffset createdAt,
        double playtimeSeconds,
        bool wasCreatedNew)
    {
        SaveId = saveId;
        WorldName = worldName;
        Seed = seed;
        WorldSize = worldSize;
        LandAmount = landAmount;
        Climate = climate;
        Geology = geology;
        CreatedAt = createdAt;
        PlaytimeSeconds = playtimeSeconds;
        WasCreatedNew = wasCreatedNew;
    }

    public static GameSession CreateNew(
        string worldName,
        string seed,
        string worldSize,
        WorldLandAmount landAmount = WorldLandAmount.Normal,
        WorldClimate climate = WorldClimate.Temperate,
        GeologicalActivity geology = GeologicalActivity.Normal)
    {
        return new GameSession(
            Guid.NewGuid().ToString("N"),
            worldName,
            seed,
            worldSize,
            landAmount,
            climate,
            geology,
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
            Enum.IsDefined(typeof(WorldLandAmount), document.LandAmount) ? (WorldLandAmount)document.LandAmount : WorldLandAmount.Normal,
            Enum.IsDefined(typeof(WorldClimate), document.Climate) ? (WorldClimate)document.Climate : WorldClimate.Temperate,
            Enum.IsDefined(typeof(GeologicalActivity), document.GeologicalActivity) ? (GeologicalActivity)document.GeologicalActivity : GeologicalActivity.Normal,
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
