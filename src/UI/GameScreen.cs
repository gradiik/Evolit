using System;
using Evolit.Game;
using Evolit.Game.Core;
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
    private CoreSimulationHost? _core;
    private DemoSimulationController? _demoSimulation;
    private GameHud? _hud;
    private DemoWorldView? _flatWorldView;
    private PlanetWorldView? _planetWorldView;
    private GameViewState? _initialViewState;

    public void Configure(
        GameSession session,
        AppSettings settings,
        SimulationSpeedState speed,
        GameTimeController time,
        DemoWorldDataProvider world,
        CoreSimulationHost core,
        GameViewState? initialViewState = null)
    {
        _session = session;
        _settings = settings.Clone();
        _speed = speed;
        _time = time;
        _world = world;
        _core = core;
        _initialViewState = initialViewState;
    }

    public override void _Ready()
    {
        if (_session is null || _speed is null || _time is null || _world is null)
            return;

        _demoSimulation = new DemoSimulationController(_world, _time);
        var stats = new DemoSimulationStatsProvider(_session, _world, _time);
        var quality = GraphicsQualityProfile.From(_settings);

        if (_session.WorldShape == Evolit.Core.WorldShape.Planet)
        {
            _planetWorldView = new PlanetWorldView();
            _planetWorldView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _planetWorldView.Configure(_world, _core!, _settings.CameraSpeed, _settings.SmoothZoom, quality);
            AddChild(_planetWorldView);
        }
        else
        {
            _flatWorldView = new DemoWorldView();
            _flatWorldView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _flatWorldView.Configure(_world, _core!, _settings.CameraSpeed, _settings.SmoothZoom, quality);
            AddChild(_flatWorldView);
        }

        _hud = new GameHud();
        _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _hud.Configure(stats, _world, _speed, _settings);
        _hud.SaveRequested += () => SaveRequested?.Invoke();
        _hud.SettingsRequested += () => SettingsRequested?.Invoke();
        _hud.PauseRequested += () => PauseRequested?.Invoke();
        _hud.CameraZoomInRequested += ZoomIn;
        _hud.CameraZoomOutRequested += ZoomOut;
        _hud.CameraCenterRequested += CenterCamera;
        _hud.CameraResetRequested += ResetCamera;
        AddChild(_hud);

        if (_flatWorldView is not null)
            _flatWorldView.SelectionChanged += entity => _hud.SetSelectedEntity(entity);
        if (_planetWorldView is not null)
            _planetWorldView.SelectionChanged += entity => _hud.SetSelectedEntity(entity);

        if (_initialViewState.HasValue)
        {
            _flatWorldView?.RestoreViewState(_initialViewState.Value);
            _planetWorldView?.RestoreViewState(_initialViewState.Value);
        }
    }

    public override void _Process(double delta)
    {
        if (_core is not null && _speed is not null && _time is not null)
        {
            var completedSteps = _core.AdvanceFrame(delta, _speed);
            _time.AdvanceFromSimulation(delta, completedSteps);
        }
        else
        {
            _time?.Advance(delta);
        }

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
        if (_planetWorldView is not null)
            return _planetWorldView.CaptureViewState();
        return _flatWorldView?.CaptureViewState();
    }

    public WorldRenderDiagnostics? GetRenderDiagnostics()
    {
        if (_planetWorldView is not null)
            return _planetWorldView.GetDiagnostics();
        return _flatWorldView?.GetDiagnostics();
    }

    private void ZoomIn()
    {
        _planetWorldView?.ZoomIn();
        _flatWorldView?.ZoomIn();
    }

    public void ApplyPlanetZoomSteps(int steps)
    {
        if (_planetWorldView is null)
            return;

        for (var step = 0; step < Math.Clamp(steps, 0, 6); step++)
            _planetWorldView.ZoomIn();
    }

    private void ZoomOut()
    {
        _planetWorldView?.ZoomOut();
        _flatWorldView?.ZoomOut();
    }

    private void CenterCamera()
    {
        _planetWorldView?.CenterCamera();
        _flatWorldView?.CenterCamera();
    }

    private void ResetCamera()
    {
        _planetWorldView?.ResetCamera();
        _flatWorldView?.ResetCamera();
    }
}
