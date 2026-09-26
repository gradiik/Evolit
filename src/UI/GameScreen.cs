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
    private DemoWorldView? _worldView;
    private GameViewState? _initialViewState;

    public void Configure(
        GameSession session,
        AppSettings settings,
        SimulationSpeedState speed,
        GameTimeController time,
        DemoWorldDataProvider world,
        GameViewState? initialViewState = null)
    {
        _session = session;
        _settings = settings.Clone();
        _speed = speed;
        _time = time;
        _world = world;
        _initialViewState = initialViewState;
    }

    public override void _Ready()
    {
        if (_session is null || _speed is null || _time is null || _world is null)
            return;

        _demoSimulation = new DemoSimulationController(_world, _time);
        var stats = new DemoSimulationStatsProvider(_session, _world, _time);
        var quality = GraphicsQualityProfile.From(_settings);

        _worldView = new DemoWorldView();
        _worldView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _worldView.Configure(_world, _settings.CameraSpeed, _settings.SmoothZoom, quality);
        AddChild(_worldView);

        _hud = new GameHud();
        _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _hud.Configure(stats, _world, _speed, _settings);
        _hud.SaveRequested += () => SaveRequested?.Invoke();
        _hud.SettingsRequested += () => SettingsRequested?.Invoke();
        _hud.PauseRequested += () => PauseRequested?.Invoke();
        _hud.CameraZoomInRequested += _worldView.ZoomIn;
        _hud.CameraZoomOutRequested += _worldView.ZoomOut;
        _hud.CameraCenterRequested += _worldView.CenterCamera;
        _hud.CameraResetRequested += _worldView.ResetCamera;
        AddChild(_hud);

        _worldView.SelectionChanged += entity => _hud.SetSelectedEntity(entity);

        if (_initialViewState.HasValue)
            _worldView.RestoreViewState(_initialViewState.Value);
    }

    public override void _Process(double delta)
    {
        _time?.Advance(delta);
        _demoSimulation?.Tick();
    }

    public override void _Input(InputEvent @event)
    {
        if (!@event.IsActionPressed("game_pause"))
            return;

        if (TryCloseActiveTool())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        PauseRequested?.Invoke();
        GetViewport().SetInputAsHandled();
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
            _hud?.SetSpeedFromAction(4);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("simulation_speed_3"))
        {
            _hud?.SetSpeedFromAction(16);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("simulation_speed_max"))
        {
            _hud?.SetSpeedFromAction(SimulationSpeedState.MaxMultiplier);
            GetViewport().SetInputAsHandled();
        }
    }

    public bool TryCloseActiveTool()
    {
        return _hud?.TryCloseActiveTool() ?? false;
    }

    public GameViewState? CaptureViewState()
    {
        return _worldView?.CaptureViewState();
    }

    public WorldRenderDiagnostics? GetRenderDiagnostics()
    {
        return _worldView?.GetDiagnostics();
    }
}
