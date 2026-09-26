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
    public string GameVersion { get; set; } = string.Empty;
    public string SaveType { get; set; } = SaveManager.ManualType;
    public int? AutosaveIndex { get; set; }
    public Dictionary<string, string> Payload { get; set; } = new();

    // Optional so legacy schema-1 saves remain loadable. A missing Runtime means
    // the old metadata-only save format and is restored with deterministic defaults.
    public GameRuntimeSaveState? Runtime { get; set; }
}

public sealed class GameRuntimeSaveState
{
    public GameTimeSaveState Time { get; set; } = new();
    public SimulationSpeedSaveState Speed { get; set; } = new();
    public DemoWorldSaveState World { get; set; } = new();
}

public sealed class GameTimeSaveState
{
    public int Day { get; set; } = 1;
    public double MinuteOfDay { get; set; }
    public long TickCount { get; set; }
}

public sealed class SimulationSpeedSaveState
{
    public bool Paused { get; set; }
    public int Multiplier { get; set; } = 1;
}

public sealed class DemoWorldSaveState
{
    public int LastSimulatedDay { get; set; } = 1;
    public List<DemoEntitySaveState> Entities { get; set; } = new();
    public List<DemoSpeciesSaveState> Species { get; set; } = new();
    public List<WorldHistorySaveState> History { get; set; } = new();
    public List<DemoChronicleSaveState> Chronicle { get; set; } = new();
    public List<DemoEventSaveState> Events { get; set; } = new();
}

public sealed class DemoEntitySaveState
{
    public string Id { get; set; } = string.Empty;
    public int Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Subspecies { get; set; } = string.Empty;
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public int AgeDays { get; set; }
    public float Health { get; set; }
    public float Energy { get; set; }
    public float Size { get; set; }
    public float Speed { get; set; }
    public string Diet { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PlantType { get; set; } = string.Empty;
    public int Population { get; set; }
    public List<float> PopulationHistory { get; set; } = new();
}

public sealed class DemoSpeciesSaveState
{
    public string Id { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public int Status { get; set; }
    public int DayAppeared { get; set; }
    public int Population { get; set; }
    public float Adaptability { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<float> PopulationHistory { get; set; } = new();
}

public sealed class WorldHistorySaveState
{
    public long Sequence { get; set; }
    public int? Day { get; set; }
    public string? GameTime { get; set; }
    public int CreaturePopulation { get; set; }
    public int PlantPopulation { get; set; }
    public int SpeciesCount { get; set; }
    public int SubspeciesCount { get; set; }
}

public sealed class DemoChronicleSaveState
{
    public int Day { get; set; }
    public string Time { get; set; } = string.Empty;
    public int Category { get; set; }
    public int Kind { get; set; }
    public int Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RelatedEntityId { get; set; } = string.Empty;
    public string IconPath { get; set; } = "simulation/event.svg";
}

public sealed class DemoEventSaveState
{
    public int Day { get; set; }
    public string Time { get; set; } = "00:00";
    public int Category { get; set; }
    public int Kind { get; set; }
    public int Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RelatedEntityId { get; set; } = string.Empty;
    public string IconPath { get; set; } = "simulation/event.svg";
}
