using System;
using Evolit.Game;
using Evolit.Session;
using Evolit.Settings;
using Evolit.UI.Game;
using Godot;

namespace Evolit.UI;

public sealed partial class GameScreen : Control
{
    public event Action? SaveRequested;
    public event Action? SettingsRequested;
    public event Action? PauseRequested;

    private GameSession? _session;
    private AppSettings _settings = AppSettings.Default();
    private SimulationSpeedState? _speed;
    private GameHud? _hud;

    public void Configure(GameSession session, AppSettings settings)
    {
        _session = session;
        _settings = settings.Clone();
    }

    public override void _Ready()
    {
        if (_session is null)
            return;

        var demoWorld = new DemoWorldDataProvider(_session);
        _speed = new SimulationSpeedState();
        var stats = new DemoSimulationStatsProvider(_session, demoWorld);

        var worldView = new DemoWorldView();
        worldView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        worldView.Configure(demoWorld, _settings.CameraSpeed, _settings.SmoothZoom);
        AddChild(worldView);

        _hud = new GameHud();
        _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _hud.Configure(stats, demoWorld, _speed);
        _hud.SaveRequested += () => SaveRequested?.Invoke();
        _hud.SettingsRequested += () => SettingsRequested?.Invoke();
        _hud.PauseRequested += () => PauseRequested?.Invoke();
        AddChild(_hud);

        worldView.SelectionChanged += entity => _hud.SetSelectedEntity(entity);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("simulation_speed_1"))
        {
            _hud?.SetSpeedFromAction(1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("simulation_speed_2"))
        {
            _hud?.SetSpeedFromAction(2);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("simulation_speed_3"))
        {
            _hud?.SetSpeedFromAction(4);
            GetViewport().SetInputAsHandled();
        }
    }
}
