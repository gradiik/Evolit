using System;

namespace Evolit.Flow;

public enum GameFlowState
{
    Startup,
    MainMenu,
    NewGame,
    Saves,
    Loading,
    Game,
    Pause,
    Settings,
    Versions,
    Encyclopedia
}

public sealed class GameFlowController
{
    public GameFlowState Current { get; private set; } = GameFlowState.Startup;
    public event Action<GameFlowState>? Changed;

    public void Transition(GameFlowState next)
    {
        if (Current == next)
            return;

        Current = next;
        Changed?.Invoke(next);
    }
}
