using Evolit.Session;
using Godot;

namespace Evolit.Game;

public readonly struct SimulationStatsSnapshot
{
    public int Day { get; init; }
    public string GameTime { get; init; }
    public int CreatureCount { get; init; }
    public int PlantCount { get; init; }
    public int SpeciesCount { get; init; }
    public int SubspeciesCount { get; init; }
    public double Fps { get; init; }
    public double Tps { get; init; }
    public long Tick { get; init; }
    public double PlaytimeSeconds { get; init; }
}

public interface ISimulationStatsProvider
{
    SimulationStatsSnapshot GetSnapshot();
}

public sealed class DemoSimulationStatsProvider : ISimulationStatsProvider
{
    private readonly GameSession _session;
    private readonly DemoWorldDataProvider _world;

    public DemoSimulationStatsProvider(GameSession session, DemoWorldDataProvider world)
    {
        _session = session;
        _world = world;
    }

    public SimulationStatsSnapshot GetSnapshot()
    {
        return new SimulationStatsSnapshot
        {
            Day = 1,
            GameTime = "00:00",
            CreatureCount = _world.CreatureCount,
            PlantCount = _world.PlantCount,
            SpeciesCount = _world.SpeciesCount,
            SubspeciesCount = _world.SubspeciesCount,
            Fps = Engine.GetFramesPerSecond(),
            Tps = 0,
            Tick = 0,
            PlaytimeSeconds = _session.PlaytimeSeconds
        };
    }
}
