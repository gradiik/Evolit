using System;

namespace Evolit.Game;

public sealed class SimulationSpeedState
{
    public const int MaxMultiplier = 64;

    public bool Paused { get; private set; }
    public int Multiplier { get; private set; } = 1;
    public string DisplayMode => Multiplier == MaxMultiplier ? "MAX" : $"{Multiplier}×";

    public event Action? Changed;

    public void TogglePause()
    {
        Paused = !Paused;
        Changed?.Invoke();
    }

    public void SetPaused(bool paused)
    {
        if (Paused == paused)
            return;

        Paused = paused;
        Changed?.Invoke();
    }

    public void SetMultiplier(int multiplier)
    {
        var next = multiplier switch
        {
            <= 1 => 1,
            <= 4 => 4,
            <= 16 => 16,
            _ => MaxMultiplier
        };

        if (Multiplier == next && !Paused)
            return;

        Multiplier = next;
        Paused = false;
        Changed?.Invoke();
    }
}
