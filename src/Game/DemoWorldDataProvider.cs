using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Session;
using Godot;

namespace Evolit.Game;

public enum DemoEntityKind
{
    Creature,
    Plant
}

public sealed class DemoEntity
{
    public string Id { get; init; } = string.Empty;
    public DemoEntityKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Species { get; init; } = string.Empty;
    public string Subspecies { get; init; } = string.Empty;
    public Vector2 WorldPosition { get; init; }
    public int AgeDays { get; set; }
    public float Health { get; set; }
    public float Energy { get; set; }
    public float Size { get; init; }
    public float Speed { get; init; }
    public string Diet { get; init; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PlantType { get; init; } = string.Empty;
    public int Population { get; set; }
    public List<float> PopulationHistory { get; init; } = new();
}

public sealed class DemoWorldDataProvider
{
    private const int MaxHistorySamples = 2048;

    private readonly List<DemoEntity> _entities;
    private readonly List<DemoSpeciesRecord> _species;
    private readonly List<DemoChronicleEntry> _chronicle;
    private readonly List<DemoEventEntry> _events;
    private readonly List<WorldHistorySample> _history = new();
    private long _nextHistorySequence;

    public WorldMap Map { get; }

    public event Action? DataChanged;
    public event Action? HistoryChanged;
    public event Action<DemoEventEntry>? EventAdded;

    public int LastSimulatedDay { get; private set; } = 1;

    public DemoWorldDataProvider(GameSession session)
    {
        Map = WorldMapGenerator.Generate(session.Seed, session.WorldSize);
        _species = SpeciesDemoData.CreateSpecies();
        _chronicle = SpeciesDemoData.CreateChronicle();
        _events = SpeciesDemoData.CreateEvents(session.WorldName);

        var creatureSpawn = Map.FindNearestLandPosition(new Vector2(-240, 70));
        var plantSpawn = Map.FindNearestLandPosition(new Vector2(250, -90));

        _entities =
        [
            new DemoEntity
            {
                Id = "demo-creature-01",
                Kind = DemoEntityKind.Creature,
                Name = "Motilis Minor",
                Species = "Motilis",
                Subspecies = "Motilis Minor",
                WorldPosition = creatureSpawn,
                AgeDays = 3,
                Health = 0.86f,
                Energy = 0.63f,
                Size = 1.2f,
                Speed = 2.4f,
                Diet = "Всеядный",
                State = "Стабильное",
                Population = 54,
                PopulationHistory = [17, 21, 25, 28, 32, 36, 41, 46, 50, 54]
            },
            new DemoEntity
            {
                Id = "demo-plant-01",
                Kind = DemoEntityKind.Plant,
                Name = "Viridia Minor",
                Species = "Viridia",
                Subspecies = "Viridia Minor",
                WorldPosition = plantSpawn,
                AgeDays = 8,
                Health = 0.94f,
                Energy = 0,
                Size = 0.8f,
                Speed = 0,
                State = "Стабильное",
                PlantType = "Наземное",
                Population = 71,
                PopulationHistory = [25, 31, 36, 40, 48, 53, 59, 64, 68, 71]
            }
        ];

        SeedLegacyHistory();
    }

    public IReadOnlyList<DemoEntity> Entities => _entities;
    public IReadOnlyList<DemoSpeciesRecord> Species => _species;
    public IReadOnlyList<DemoChronicleEntry> Chronicle => _chronicle;
    public IReadOnlyList<DemoEventEntry> Events => _events;

    public IReadOnlyList<WorldHistorySample> History => _history;

    public int CreatureCount => _entities.Where(entity => entity.Kind == DemoEntityKind.Creature).Sum(entity => entity.Population);
    public int PlantCount => _entities.Where(entity => entity.Kind == DemoEntityKind.Plant).Sum(entity => entity.Population);
    public int SpeciesCount => _species.Count(species => species.ParentId == "origin" && species.Status == DemoSpeciesStatus.Active);
    public int SubspeciesCount => _species.Count(species => species.ParentId != "origin" && species.ParentId.Length > 0 && species.Status == DemoSpeciesStatus.Active);

    public float GetMovementSpeedMultiplier(DemoEntity entity)
    {
        return WorldMovementRules.SpeedMultiplier(
            Map.GetCellAtWorld(entity.WorldPosition),
            entity.Kind);
    }

    public double AverageAdaptability => _species
        .Where(species => species.Kind != DemoSpeciesKind.Origin && species.Status == DemoSpeciesStatus.Active)
        .Select(species => (double)species.Adaptability)
        .DefaultIfEmpty(0)
        .Average();

    public void AdvanceDemoDay(int day)
    {
        if (day <= LastSimulatedDay)
            return;

        for (var current = LastSimulatedDay + 1; current <= day; current++)
            AdvanceSingleDay(current);

        LastSimulatedDay = day;
        HistoryChanged?.Invoke();
        DataChanged?.Invoke();
    }

    public void AddEvent(
        int day,
        string time,
        DemoEventCategory category,
        string title,
        string description,
        string iconPath,
        DemoEventKind kind = DemoEventKind.General,
        DemoEventSeverity severity = DemoEventSeverity.Info,
        string relatedEntityId = "")
    {
        var entry = new DemoEventEntry
        {
            Day = day,
            Time = time,
            Category = category,
            Kind = kind,
            Severity = severity,
            Title = title,
            Description = description,
            RelatedEntityId = relatedEntityId,
            IconPath = iconPath
        };

        InsertEvent(entry, true);
        DataChanged?.Invoke();
    }

    private void AdvanceSingleDay(int day)
    {
        var creature = _entities.First(entity => entity.Kind == DemoEntityKind.Creature);
        var plant = _entities.First(entity => entity.Kind == DemoEntityKind.Plant);

        var creatureDelta = 1 + day % 3;
        var plantDelta = 2 + (day + 1) % 3;

        creature.Population = Math.Max(1, creature.Population + creatureDelta);
        plant.Population = Math.Max(1, plant.Population + plantDelta);
        creature.AgeDays++;
        plant.AgeDays++;

        creature.Health = Mathf.Clamp(creature.Health + (day % 2 == 0 ? 0.015f : -0.008f), 0.55f, 0.98f);
        creature.Energy = Mathf.Clamp(creature.Energy + (day % 3 == 0 ? -0.025f : 0.018f), 0.40f, 0.95f);
        plant.Health = Mathf.Clamp(plant.Health + (day % 4 == 0 ? -0.012f : 0.010f), 0.65f, 0.99f);

        Append(creature.PopulationHistory, creature.Population);
        Append(plant.PopulationHistory, plant.Population);

        var motilisMinor = _species.First(species => species.Id == "motilis_minor");
        var viridiaMinor = _species.First(species => species.Id == "viridia_minor");
        motilisMinor.Population = creature.Population;
        viridiaMinor.Population = plant.Population;
        motilisMinor.Adaptability = Mathf.Clamp(motilisMinor.Adaptability + 0.004f, 0.1f, 0.98f);
        viridiaMinor.Adaptability = Mathf.Clamp(viridiaMinor.Adaptability + 0.003f, 0.1f, 0.98f);
        Append(motilisMinor.PopulationHistory, motilisMinor.Population);
        Append(viridiaMinor.PopulationHistory, viridiaMinor.Population);

        AppendHistory(new WorldHistorySample(
            _nextHistorySequence++,
            day,
            "00:00",
            CreatureCount,
            PlantCount,
            SpeciesCount,
            SubspeciesCount));

        if (day > 4)
        {
            _chronicle.Add(new DemoChronicleEntry
            {
                Day = day,
                Time = "00:00",
                Category = DemoEventCategory.World,
                Kind = DemoEventKind.Observation,
                Severity = DemoEventSeverity.Info,
                Title = "Наблюдение обновлено",
                Description = "Демо-runtime добавил новую точку статистики без запуска биологической симуляции.",
                IconPath = "simulation/history.svg"
            });
        }

        InsertEvent(
            new DemoEventEntry
            {
                Day = day,
                Time = "00:00",
                Category = DemoEventCategory.World,
                Kind = DemoEventKind.Observation,
                Severity = DemoEventSeverity.Info,
                Title = $"Начался день {day}",
                Description = "Демо-популяции получили новую точку истории.",
                IconPath = "simulation/event.svg"
            },
            true);
    }

    private void InsertEvent(DemoEventEntry entry, bool notify)
    {
        _events.Insert(0, entry);

        if (_events.Count > 40)
            _events.RemoveRange(40, _events.Count - 40);

        if (notify)
            EventAdded?.Invoke(entry);
    }

    private void SeedLegacyHistory()
    {
        int[] creatures = [22, 27, 31, 35, 38, 42, 46, 49, 52, 54];
        int[] plants = [35, 39, 44, 48, 53, 57, 61, 65, 68, 71];
        int[] species = [1, 1, 1, 2, 2, 2, 2, 2, 2, 2];
        int[] subspecies = [0, 1, 1, 2, 2, 3, 3, 4, 4, 4];

        for (var i = 0; i < creatures.Length; i++)
        {
            _history.Add(new WorldHistorySample(
                i,
                null,
                null,
                creatures[i],
                plants[i],
                species[i],
                subspecies[i]));
        }

        _nextHistorySequence = _history.Count;
    }

    private void AppendHistory(WorldHistorySample sample)
    {
        _history.Add(sample);
        if (_history.Count > MaxHistorySamples)
            _history.RemoveRange(0, _history.Count - MaxHistorySamples);
    }

    private static void Append(List<float> values, float value)
    {
        values.Add(value);
        while (values.Count > 24)
            values.RemoveAt(0);
    }
}
