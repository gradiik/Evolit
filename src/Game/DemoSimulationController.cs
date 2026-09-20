namespace Evolit.Game;

public sealed class DemoSimulationController
{
    private readonly DemoWorldDataProvider _world;
    private readonly GameTimeController _time;
    private int _lastProcessedDay;

    public DemoSimulationController(DemoWorldDataProvider world, GameTimeController time)
    {
        _world = world;
        _time = time;
        _lastProcessedDay = world.LastSimulatedDay;
    }

    public void Tick()
    {
        if (_time.Day <= _lastProcessedDay)
            return;

        _world.AdvanceDemoDay(_time.Day);
        _lastProcessedDay = _time.Day;
    }
}
