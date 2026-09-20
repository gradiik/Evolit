using System;

namespace Evolit.Game;

public sealed class GameTimeController
{
    private const double MinutesPerRealSecond = 12.0;
    private const double BaseTicksPerSecond = 10.0;
    private const double MinutesPerDay = 24.0 * 60.0;

    private readonly SimulationSpeedState _speed;
    private double _minuteOfDay;
    private double _tickFraction;

    public int Day { get; private set; } = 1;
    public long TickCount { get; private set; }

    public double Tps => _speed.Paused ? 0.0 : BaseTicksPerSecond * _speed.Multiplier;

    public string FormattedTime
    {
        get
        {
            var totalMinutes = Math.Clamp((int)Math.Floor(_minuteOfDay), 0, 1439);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return $"{hours:00}:{minutes:00}";
        }
    }

    public GameTimeController(SimulationSpeedState speed)
    {
        _speed = speed;
    }

    public void Advance(double delta)
    {
        if (delta <= 0 || _speed.Paused)
            return;

        var multiplier = _speed.Multiplier;
        _minuteOfDay += delta * MinutesPerRealSecond * multiplier;

        while (_minuteOfDay >= MinutesPerDay)
        {
            _minuteOfDay -= MinutesPerDay;
            Day++;
        }

        _tickFraction += delta * BaseTicksPerSecond * multiplier;
        var completedTicks = (long)Math.Floor(_tickFraction);
        if (completedTicks > 0)
        {
            TickCount += completedTicks;
            _tickFraction -= completedTicks;
        }
    }
}
