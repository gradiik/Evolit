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
    private GameTimeController? _time;
    private DemoWorldDataProvider? _world;
    private DemoSimulationController? _demoSimulation;
    private GameHud? _hud;

    public void Configure(
        GameSession session,
        AppSettings settings,
        SimulationSpeedState speed,
        GameTimeController time,
        DemoWorldDataProvider world)
    {
        _session = session;
        _settings = settings.Clone();
        _speed = speed;
        _time = time;
        _world = world;
    }

    public override void _Ready()
    {
        if (_session is null || _speed is null || _time is null || _world is null)
            return;

        _demoSimulation = new DemoSimulationController(_world, _time);
        var stats = new DemoSimulationStatsProvider(_session, _world, _time);

        var worldView = new DemoWorldView();
        worldView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        worldView.Configure(_world, _settings.CameraSpeed, _settings.SmoothZoom);
        AddChild(worldView);

        _hud = new GameHud();
        _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _hud.Configure(stats, _world, _speed);
        _hud.SaveRequested += () => SaveRequested?.Invoke();
        _hud.SettingsRequested += () => SettingsRequested?.Invoke();
        _hud.PauseRequested += () => PauseRequested?.Invoke();
        AddChild(_hud);

        worldView.SelectionChanged += entity => _hud.SetSelectedEntity(entity);
    }

    public override void _Process(double delta)
    {
        _time?.Advance(delta);
        _demoSimulation?.Tick();
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
