using System;

namespace Evolit.Game;

public sealed class GameTimeController
{
    private const double MinutesPerRealSecond = 12.0;
    public const double BaseTicksPerSecond = 10.0;
    private const double MinutesPerDay = 24.0 * 60.0;

    private readonly SimulationSpeedState _speed;
    private double _minuteOfDay;
    private double _tickFraction;
    private double _measureSeconds;
    private long _measureTicks;
    private double _measuredTps;

    public int Day { get; private set; } = 1;
    public long TickCount { get; private set; }
    public double MinuteOfDay => _minuteOfDay;

    public double Tps => _measuredTps;

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

    public void Restore(int day, double minuteOfDay, long tickCount)
    {
        Day = Math.Max(1, day);
        _minuteOfDay = Math.Clamp(minuteOfDay, 0, MinutesPerDay - 0.0001);
        TickCount = Math.Max(0, tickCount);
        _tickFraction = 0;
        _measureSeconds = 0;
        _measureTicks = 0;
        _measuredTps = 0;
    }

    public void Advance(double delta)
    {
        if (delta <= 0)
            return;

        if (_speed.Paused)
        {
            UpdateMeasuredTps(delta, 0);
            return;
        }

        var multiplier = _speed.Multiplier;
        _tickFraction += delta * BaseTicksPerSecond * multiplier;
        var completedTicks = (long)Math.Floor(_tickFraction);
        if (completedTicks > 0)
            _tickFraction -= completedTicks;

        AdvanceByCompletedTicks(delta, completedTicks);
    }

    public void AdvanceFromSimulation(double delta, int completedTicks)
    {
        if (delta <= 0)
            return;

        if (_speed.Paused)
        {
            UpdateMeasuredTps(delta, 0);
            return;
        }

        _tickFraction = 0;
        AdvanceByCompletedTicks(delta, Math.Max(0, completedTicks));
    }

    private void AdvanceByCompletedTicks(double delta, long completedTicks)
    {
        if (completedTicks > 0)
        {
            TickCount += completedTicks;
            _minuteOfDay += completedTicks / BaseTicksPerSecond * MinutesPerRealSecond;

            while (_minuteOfDay >= MinutesPerDay)
            {
                _minuteOfDay -= MinutesPerDay;
                Day++;
            }
        }

        UpdateMeasuredTps(delta, completedTicks);
    }

    private void UpdateMeasuredTps(double delta, long completedTicks)
    {
        _measureSeconds += delta;
        _measureTicks += completedTicks;

        if (_measureSeconds < 0.50)
            return;

        _measuredTps = _measureSeconds <= 0
            ? 0
            : _measureTicks / _measureSeconds;
        _measureSeconds = 0;
        _measureTicks = 0;
    }
}
