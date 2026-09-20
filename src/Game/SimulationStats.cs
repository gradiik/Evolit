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
    private readonly GameTimeController _time;

    public DemoSimulationStatsProvider(GameSession session, DemoWorldDataProvider world, GameTimeController time)
    {
        _session = session;
        _world = world;
        _time = time;
    }

    public SimulationStatsSnapshot GetSnapshot()
    {
        return new SimulationStatsSnapshot
        {
            Day = _time.Day,
            GameTime = _time.FormattedTime,
            CreatureCount = _world.CreatureCount,
            PlantCount = _world.PlantCount,
            SpeciesCount = _world.SpeciesCount,
            SubspeciesCount = _world.SubspeciesCount,
            Fps = Engine.GetFramesPerSecond(),
            Tps = _time.Tps,
            Tick = _time.TickCount,
            PlaytimeSeconds = _session.PlaytimeSeconds
        };
    }
}
