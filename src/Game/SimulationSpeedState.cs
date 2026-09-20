using System;

namespace Evolit.Game;

public sealed class SimulationSpeedState
{
    public bool Paused { get; private set; }
    public int Multiplier { get; private set; } = 1;

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
            2 => 2,
            _ => 4
        };

        if (Multiplier == next && !Paused)
            return;

        Multiplier = next;
        Paused = false;
        Changed?.Invoke();
    }
}
