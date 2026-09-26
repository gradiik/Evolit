using System;
using System.Collections.Generic;

namespace Evolit.Core;

public sealed class SimulationClock
{
    public SimulationClock(double fixedDeltaSeconds = 0.1)
    {
        if (!double.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
        FixedDeltaSeconds = fixedDeltaSeconds;
    }

    public long TickCount { get; private set; }
    public double FixedDeltaSeconds { get; }
    public double SimulationSeconds => TickCount * FixedDeltaSeconds;

    internal void Advance() => TickCount++;

    internal void Restore(long tickCount)
    {
        TickCount = Math.Max(0, tickCount);
    }
}

public interface ISimulationSystem
{
    string Name { get; }
    int IntervalTicks { get; }
    void Execute(CoreSimulation simulation);
}

public sealed class SimulationScheduler
{
    private readonly List<ISimulationSystem> _systems = new();

    public IReadOnlyList<ISimulationSystem> Systems => _systems;

    public void Register(ISimulationSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (system.IntervalTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(system), "System interval must be positive.");
        _systems.Add(system);
    }

    internal void RunDue(CoreSimulation simulation)
    {
        var tick = simulation.Clock.TickCount;
        for (var index = 0; index < _systems.Count; index++)
        {
            var system = _systems[index];
            if (tick % system.IntervalTicks == 0)
                system.Execute(simulation);
        }
    }
}

public sealed class OrganismFoundationSystem : ISimulationSystem
{
    public string Name => "organism.foundation";
    public int IntervalTicks => 1;

    public void Execute(CoreSimulation simulation)
    {
        simulation.Organisms.AdvanceFoundation(simulation.Clock.FixedDeltaSeconds, simulation.Genomes);
    }
}

public static class EnvironmentSchedule
{
    public const int ClimateTicks = 2;
    public const int AtmosphereTicks = 4;
    public const int HumidityTicks = 5;
    public const int HydrologyTicks = 10;
    public const int ResourceTicks = 20;
    public const int LightTicks = 10;
}

public sealed class ClimateSystem : ISimulationSystem
{
    public string Name => "environment.climate";
    public int IntervalTicks => EnvironmentSchedule.ClimateTicks;
    public void Execute(CoreSimulation simulation) => simulation.Environment.UpdateClimate(simulation.Clock.SimulationSeconds, simulation.Mode == SimulationMode.Bootstrap ? 4f : 1f);
}
public sealed class AtmosphereSystem : ISimulationSystem
{
    public string Name => "environment.atmosphere";
    public int IntervalTicks => EnvironmentSchedule.AtmosphereTicks;
    public void Execute(CoreSimulation simulation) => simulation.Environment.UpdateAtmosphere(simulation.Mode == SimulationMode.Bootstrap ? 4f : 1f);
}
public sealed class HumiditySystem : ISimulationSystem
{
    public string Name => "environment.humidity";
    public int IntervalTicks => EnvironmentSchedule.HumidityTicks;
    public void Execute(CoreSimulation simulation) => simulation.Environment.UpdateHumidity(simulation.Mode == SimulationMode.Bootstrap ? 4f : 1f);
}
public sealed class HydrologySystem : ISimulationSystem
{
    public string Name => "environment.hydrology";
    public int IntervalTicks => EnvironmentSchedule.HydrologyTicks;
    public void Execute(CoreSimulation simulation) => simulation.Environment.UpdateHydrology(simulation.Mode == SimulationMode.Bootstrap ? 4f : 1f);
}
public sealed class ResourceSystem : ISimulationSystem
{
    public string Name => "environment.resources";
    public int IntervalTicks => EnvironmentSchedule.ResourceTicks;
    public void Execute(CoreSimulation simulation) => simulation.Environment.UpdateResources(simulation.Mode == SimulationMode.Bootstrap ? 4f : 1f);
}
public sealed class LightSystem : ISimulationSystem
{
    public string Name => "environment.light";
    public int IntervalTicks => EnvironmentSchedule.LightTicks;
    public void Execute(CoreSimulation simulation) => simulation.Environment.UpdateLight(simulation.Clock.SimulationSeconds);
}

public sealed class CoreSimulation
{
    public const int SnapshotVersion = 2;

    public CoreSimulation(
        WorldTopology topology,
        EnvironmentStore environment,
        ulong seed,
        SimulationMode mode = SimulationMode.Live,
        double fixedDeltaSeconds = 0.1)
    {
        Topology = topology ?? throw new ArgumentNullException(nameof(topology));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        if (environment.Count != topology.Count)
            throw new ArgumentException("Environment store must match topology cell count.", nameof(environment));

        Mode = mode;
        Clock = new SimulationClock(fixedDeltaSeconds);
        Random = new CoreRandomStreams(seed);
        Genomes = new GenomeStore();
        Lineages = new LineageStore();
        Organisms = new OrganismStore();
        Scheduler = CreateDefaultScheduler();
    }

    private CoreSimulation(
        WorldTopology topology,
        EnvironmentStore environment,
        SimulationClock clock,
        CoreRandomStreams random,
        GenomeStore genomes,
        LineageStore lineages,
        OrganismStore organisms,
        SimulationMode mode)
    {
        Topology = topology;
        Environment = environment;
        Clock = clock;
        Random = random;
        Genomes = genomes;
        Lineages = lineages;
        Organisms = organisms;
        Mode = mode;
        Scheduler = CreateDefaultScheduler();
    }

    public SimulationMode Mode { get; set; }
    public SimulationClock Clock { get; }
    public CoreRandomStreams Random { get; }
    public WorldTopology Topology { get; }
    public EnvironmentStore Environment { get; }
    public GenomeStore Genomes { get; }
    public LineageStore Lineages { get; }
    public OrganismStore Organisms { get; }
    public SimulationScheduler Scheduler { get; }

    public void Step()
    {
        Clock.Advance();
        Scheduler.RunDue(this);
    }

    public void Step(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        for (var index = 0; index < count; index++)
            Step();
    }

    public OrganismId CreateFounder(CellId cellId, Genome genome, float energy = 1f, float health = 1f)
    {
        if (!Topology.TryGetIndex(cellId, out _))
            throw new KeyNotFoundException($"Unknown cell {cellId}.");
        var genomeId = Genomes.Add(genome);
        var lineageId = Lineages.CreateFounder(genomeId, Clock.TickCount);
        return Organisms.Create(cellId, genomeId, lineageId, energy, health);
    }

    public CoreSimulationSnapshot CaptureSnapshot()
    {
        return new CoreSimulationSnapshot
        {
            Version = SnapshotVersion,
            Mode = Mode,
            FixedDeltaSeconds = Clock.FixedDeltaSeconds,
            TickCount = Clock.TickCount,
            Random = Random.CaptureSnapshot(),
            Topology = Topology.CaptureSnapshot(),
            Environment = Environment.CaptureSnapshot(),
            Genomes = Genomes.CaptureSnapshot(),
            Lineages = Lineages.CaptureSnapshot(),
            Organisms = Organisms.CaptureSnapshot()
        };
    }

    public static CoreSimulation Restore(CoreSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version is < 1 or > SnapshotVersion)
            throw new InvalidOperationException($"Unsupported Core snapshot version {snapshot.Version}.");

        var topology = WorldTopology.Restore(snapshot.Topology ?? new WorldTopologySnapshot());
        var environment = EnvironmentStore.Restore(topology, snapshot.Environment ?? new EnvironmentSnapshot());
        var clock = new SimulationClock(snapshot.FixedDeltaSeconds);
        clock.Restore(snapshot.TickCount);
        var random = new CoreRandomStreams(1);
        random.Restore(snapshot.Random ?? new CoreRandomStreamsSnapshot());
        var genomes = GenomeStore.Restore(snapshot.Genomes ?? new GenomeSnapshot());
        var lineages = LineageStore.Restore(snapshot.Lineages ?? new LineageSnapshot());
        var organisms = OrganismStore.Restore(snapshot.Organisms ?? new OrganismStoreSnapshot());

        return new CoreSimulation(
            topology,
            environment,
            clock,
            random,
            genomes,
            lineages,
            organisms,
            snapshot.Mode);
    }

    private static SimulationScheduler CreateDefaultScheduler()
    {
        var scheduler = new SimulationScheduler();
        scheduler.Register(new OrganismFoundationSystem());
        scheduler.Register(new ClimateSystem());
        scheduler.Register(new AtmosphereSystem());
        scheduler.Register(new HumiditySystem());
        scheduler.Register(new HydrologySystem());
        scheduler.Register(new ResourceSystem());
        scheduler.Register(new LightSystem());
        return scheduler;
    }
}

public sealed class CoreSimulationSnapshot
{
    public int Version { get; set; } = CoreSimulation.SnapshotVersion;
    public SimulationMode Mode { get; set; } = SimulationMode.Live;
    public double FixedDeltaSeconds { get; set; } = 0.1;
    public long TickCount { get; set; }
    public CoreRandomStreamsSnapshot? Random { get; set; }
    public WorldTopologySnapshot? Topology { get; set; }
    public EnvironmentSnapshot? Environment { get; set; }
    public GenomeSnapshot? Genomes { get; set; }
    public LineageSnapshot? Lineages { get; set; }
    public OrganismStoreSnapshot? Organisms { get; set; }
}
