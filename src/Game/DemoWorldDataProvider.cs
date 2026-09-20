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
    public int AgeDays { get; init; }
    public float Health { get; init; }
    public float Energy { get; init; }
    public float Size { get; init; }
    public float Speed { get; init; }
    public string Diet { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PlantType { get; init; } = string.Empty;
    public int Population { get; init; }
    public IReadOnlyList<float> PopulationHistory { get; init; } = Array.Empty<float>();
}

public sealed class DemoWorldDataProvider
{
    private readonly List<DemoEntity> _entities;
    private readonly List<string> _chronicle = new();
    private readonly List<string> _events = new();

    public DemoWorldDataProvider(GameSession session)
    {
        _entities =
        [
            new DemoEntity
            {
                Id = "demo-creature-01",
                Kind = DemoEntityKind.Creature,
                Name = "Тестовое существо",
                Species = "Forma prima",
                Subspecies = "Aqua minor",
                WorldPosition = new Vector2(-170, 40),
                AgeDays = 3,
                Health = 0.86f,
                Energy = 0.63f,
                Size = 1.2f,
                Speed = 2.4f,
                Diet = "Всеядный",
                Population = 24,
                PopulationHistory = [9, 11, 12, 15, 14, 18, 21, 20, 23, 24]
            },
            new DemoEntity
            {
                Id = "demo-plant-01",
                Kind = DemoEntityKind.Plant,
                Name = "Тестовое растение",
                Species = "Viridia prima",
                Subspecies = "Moss form",
                WorldPosition = new Vector2(210, -90),
                AgeDays = 8,
                Health = 0.94f,
                Energy = 0,
                Size = 0.8f,
                Speed = 0,
                State = "Стабильное",
                PlantType = "Наземное",
                Population = 61,
                PopulationHistory = [30, 34, 39, 37, 43, 49, 52, 55, 58, 61]
            }
        ];

        _chronicle.Add("День 0 — Мир создан");
        _events.Add($"Сессия «{session.WorldName}» готова");
        _events.Add("Демо-объекты загружены только для проверки интерфейса");
    }

    public IReadOnlyList<DemoEntity> Entities => _entities;
    public IReadOnlyList<string> Chronicle => _chronicle;
    public IReadOnlyList<string> Events => _events;

    public int CreatureCount => _entities.Count(entity => entity.Kind == DemoEntityKind.Creature);
    public int PlantCount => _entities.Count(entity => entity.Kind == DemoEntityKind.Plant);
    public int SpeciesCount => _entities.Select(entity => entity.Species).Distinct().Count();
    public int SubspeciesCount => _entities.Select(entity => entity.Subspecies).Distinct().Count();
}
