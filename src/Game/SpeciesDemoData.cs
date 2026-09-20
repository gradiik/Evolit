using System.Collections.Generic;

namespace Evolit.Game;

public enum DemoSpeciesKind
{
    Origin,
    Plant,
    Creature
}

public enum DemoSpeciesStatus
{
    Active,
    Extinct
}

public enum DemoEventCategory
{
    World,
    System,
    Evolution
}

public sealed class DemoSpeciesRecord
{
    public string Id { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DemoSpeciesKind Kind { get; init; }
    public DemoSpeciesStatus Status { get; set; } = DemoSpeciesStatus.Active;
    public int DayAppeared { get; init; }
    public int Population { get; set; }
    public float Adaptability { get; set; }
    public string Description { get; init; } = string.Empty;
    public List<float> PopulationHistory { get; init; } = new();
}

public sealed class DemoChronicleEntry
{
    public int Day { get; init; }
    public DemoEventCategory Category { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconPath { get; init; } = "simulation/event.svg";
}

public sealed class DemoEventEntry
{
    public int Day { get; init; }
    public string Time { get; init; } = "00:00";
    public DemoEventCategory Category { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconPath { get; init; } = "simulation/event.svg";
}

public static class SpeciesDemoData
{
    public static List<DemoSpeciesRecord> CreateSpecies()
    {
        return
        [
            new DemoSpeciesRecord
            {
                Id = "origin",
                ParentId = "",
                Name = "Первичная форма",
                Kind = DemoSpeciesKind.Origin,
                DayAppeared = 0,
                Population = 0,
                Adaptability = 0.72f,
                Description = "Условная исходная форма демо-дерева.",
                PopulationHistory = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
            },
            new DemoSpeciesRecord
            {
                Id = "viridia",
                ParentId = "origin",
                Name = "Viridia",
                Kind = DemoSpeciesKind.Plant,
                DayAppeared = 0,
                Population = 114,
                Adaptability = 0.81f,
                Description = "Растительная линия с упором на поглощение и устойчивый рост.",
                PopulationHistory = [41, 48, 56, 62, 70, 78, 89, 96, 105, 114]
            },
            new DemoSpeciesRecord
            {
                Id = "viridia_minor",
                ParentId = "viridia",
                Name = "Viridia Minor",
                Kind = DemoSpeciesKind.Plant,
                DayAppeared = 1,
                Population = 71,
                Adaptability = 0.86f,
                Description = "Низкая компактная форма, хорошо удерживающая площадь.",
                PopulationHistory = [25, 31, 36, 40, 48, 53, 59, 64, 68, 71]
            },
            new DemoSpeciesRecord
            {
                Id = "viridia_aqua",
                ParentId = "viridia",
                Name = "Viridia Aqua",
                Kind = DemoSpeciesKind.Plant,
                DayAppeared = 1,
                Population = 43,
                Adaptability = 0.74f,
                Description = "Демо-ветвь, ориентированная на влажные участки.",
                PopulationHistory = [16, 17, 20, 22, 22, 25, 30, 32, 37, 43]
            },
            new DemoSpeciesRecord
            {
                Id = "motilis",
                ParentId = "origin",
                Name = "Motilis",
                Kind = DemoSpeciesKind.Creature,
                DayAppeared = 0,
                Population = 87,
                Adaptability = 0.77f,
                Description = "Подвижная линия простых организмов.",
                PopulationHistory = [29, 35, 41, 45, 49, 56, 64, 70, 79, 87]
            },
            new DemoSpeciesRecord
            {
                Id = "motilis_minor",
                ParentId = "motilis",
                Name = "Motilis Minor",
                Kind = DemoSpeciesKind.Creature,
                DayAppeared = 1,
                Population = 54,
                Adaptability = 0.83f,
                Description = "Компактная подвижная форма с низкими затратами энергии.",
                PopulationHistory = [17, 21, 25, 28, 32, 36, 41, 46, 50, 54]
            },
            new DemoSpeciesRecord
            {
                Id = "motilis_longa",
                ParentId = "motilis",
                Name = "Motilis Longa",
                Kind = DemoSpeciesKind.Creature,
                DayAppeared = 1,
                Population = 33,
                Adaptability = 0.69f,
                Description = "Удлинённая форма с повышенной скоростью перемещения.",
                PopulationHistory = [12, 14, 16, 17, 17, 20, 23, 24, 29, 33]
            },
            new DemoSpeciesRecord
            {
                Id = "motilis_brevis",
                ParentId = "motilis",
                Name = "Motilis Brevis",
                Kind = DemoSpeciesKind.Creature,
                Status = DemoSpeciesStatus.Extinct,
                DayAppeared = 1,
                Population = 0,
                Adaptability = 0.38f,
                Description = "Вымершая демонстрационная ветвь.",
                PopulationHistory = [8, 10, 9, 7, 5, 3, 2, 1, 0, 0]
            }
        ];
    }

    public static List<DemoChronicleEntry> CreateChronicle()
    {
        return
        [
            new DemoChronicleEntry
            {
                Day = 0,
                Category = DemoEventCategory.World,
                Title = "Мир создан",
                Description = "Сессия получила seed и начала собственную историю.",
                IconPath = "environment/world.svg"
            },
            new DemoChronicleEntry
            {
                Day = 0,
                Category = DemoEventCategory.Evolution,
                Title = "Появились первые линии",
                Description = "Условные линии Viridia и Motilis отделились от первичной формы.",
                IconPath = "biology/evolution.svg"
            },
            new DemoChronicleEntry
            {
                Day = 1,
                Category = DemoEventCategory.Evolution,
                Title = "Viridia Minor",
                Description = "Зафиксировано первое растительное ответвление демо-набора.",
                IconPath = "biology/plant.svg"
            },
            new DemoChronicleEntry
            {
                Day = 1,
                Category = DemoEventCategory.Evolution,
                Title = "Motilis Minor и Motilis Longa",
                Description = "Подвижная линия разделилась на две активные формы.",
                IconPath = "biology/creature.svg"
            },
            new DemoChronicleEntry
            {
                Day = 1,
                Category = DemoEventCategory.Evolution,
                Title = "Motilis Brevis угасла",
                Description = "Демо-ветвь отмечена как вымершая для визуализации дерева.",
                IconPath = "biology/extinction.svg"
            }
        ];
    }

    public static List<DemoEventEntry> CreateEvents(string worldName)
    {
        return
        [
            new DemoEventEntry
            {
                Day = 1,
                Time = "00:00",
                Category = DemoEventCategory.World,
                Title = $"Сессия «{worldName}» готова",
                Description = "Игровая оболочка и демо-данные подключены.",
                IconPath = "environment/world.svg"
            },
            new DemoEventEntry
            {
                Day = 1,
                Time = "00:00",
                Category = DemoEventCategory.Evolution,
                Title = "Демо-линии загружены",
                Description = "Viridia и Motilis используются только для проверки UI.",
                IconPath = "biology/evolution.svg"
            },
            new DemoEventEntry
            {
                Day = 1,
                Time = "00:00",
                Category = DemoEventCategory.System,
                Title = "Runtime готов",
                Description = "Время, HUD и инструменты наблюдения активны.",
                IconPath = "status/info.svg"
            }
        ];
    }
}
