using System;
using System.Collections.Generic;
using Evolit.Game;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class GameHud : Control
{
    public event Action? SaveRequested;
    public event Action? SettingsRequested;
    public event Action? PauseRequested;
    public event Action? CameraZoomInRequested;
    public event Action? CameraZoomOutRequested;
    public event Action? CameraCenterRequested;
    public event Action? CameraResetRequested;

    private ISimulationStatsProvider? _statsProvider;
    private DemoWorldDataProvider? _world;
    private SimulationSpeedState? _speed;
    private bool _showFps;
    private bool _showPerformance;

    private Label? _day;
    private Label? _time;
    private Label? _creatures;
    private Label? _plants;
    private Label? _species;
    private Label? _subspecies;
    private Label? _fps;
    private Label? _tps;
    private Label? _tick;
    private Label? _realSpeed;

    private Control? _techPanel;
    private Control? _fpsBox;
    private Control? _tpsBox;
    private Control? _tickBox;

    private Button? _pauseSpeed;
    private Button? _speed1;
    private Button? _speed4;
    private Button? _speed16;
    private Button? _speedMax;

    private SelectionPanel? _selectionPanel;
    private WorldEventPopupHost? _eventPopups;
    private Control? _activeTool;
    private readonly Dictionary<string, Button> _toolButtons = new();
    private DemoEntity? _selectedEntity;
    private double _refreshTimer;

    public bool HasActiveTool => _activeTool is not null;

    public bool TryCloseActiveTool()
    {
        if (_activeTool is null)
            return false;

        CloseActiveToolAnimated();
        return true;
    }

    public void Configure(
        ISimulationStatsProvider statsProvider,
        DemoWorldDataProvider world,
        SimulationSpeedState speed,
        AppSettings settings)
    {
        _statsProvider = statsProvider;
        _world = world;
        _speed = speed;
        _showFps = settings.ShowFps;
        _showPerformance = settings.ShowPerformance;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        BuildTopBar();
        BuildBottomBar();
        BuildSelectionPanel();
        BuildEventPopups();

        if (_speed is not null)
            _speed.Changed += RefreshSpeedButtons;

        if (_world is not null)
        {
            _world.DataChanged += RefreshSelection;
            _world.EventAdded += HandleWorldEvent;
        }

        ApplyDiagnosticVisibility();
        RefreshStats();
        RefreshSpeedButtons();
    }

    public override void _ExitTree()
    {
        if (_speed is not null)
            _speed.Changed -= RefreshSpeedButtons;

        if (_world is not null)
        {
            _world.DataChanged -= RefreshSelection;
            _world.EventAdded -= HandleWorldEvent;
        }
    }

    public override void _Process(double delta)
    {
        _refreshTimer += delta;
        if (_refreshTimer < 0.20)
            return;

        _refreshTimer = 0;
        RefreshStats();
    }

    public void SetSelectedEntity(DemoEntity? entity)
    {
        _selectedEntity = entity;
        _selectionPanel?.SetEntity(entity);
    }

    public void SetSpeedFromAction(int multiplier)
    {
        SetSimulationSpeed(multiplier);
    }

    private void BuildTopBar()
    {
        var top = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        top.AnchorLeft = 0.018f;
        top.AnchorRight = 0.982f;
        top.AnchorTop = 0.014f;
        top.AnchorBottom = 0.105f;
        top.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.94f));
        AddChild(top);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 7);
        margin.AddThemeConstantOverride("margin_bottom", 7);
        top.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        var stats = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(410, 0)
        };
        stats.AddThemeConstantOverride("separation", 6);
        row.AddChild(stats);

        _day = AddStat(stats, "ДЕНЬ", "1");
        _time = AddStat(stats, "ВРЕМЯ", "00:00");
        _creatures = AddStat(stats, "СУЩЕСТВА", "0");
        _plants = AddStat(stats, "РАСТЕНИЯ", "0");
        _species = AddStat(stats, "ВИДЫ", "0");
        _subspecies = AddStat(stats, "ПОДВИДЫ", "0");

        row.AddChild(Separator());

        var primary = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        primary.AddThemeConstantOverride("separation", 8);
        row.AddChild(primary);

        AddGameTool(primary, "Эволюция", "biology/evolution.svg", ShowEvolution, 170, true);
        AddGameTool(primary, "Древо", "biology/lineage.svg", ShowLineage, 170, true);
        AddGameTool(primary, "Статистика", "settings/performance.svg", ShowWorldStats, 170, true);

        row.AddChild(Separator());

        var speedPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(304, 0)
        };
        speedPanel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        row.AddChild(speedPanel);

        var speedMargin = new MarginContainer();
        speedMargin.AddThemeConstantOverride("margin_left", 8);
        speedMargin.AddThemeConstantOverride("margin_right", 8);
        speedMargin.AddThemeConstantOverride("margin_top", 4);
        speedMargin.AddThemeConstantOverride("margin_bottom", 4);
        speedPanel.AddChild(speedMargin);

        var speedRoot = new VBoxContainer();
        speedRoot.AddThemeConstantOverride("separation", 2);
        speedMargin.AddChild(speedRoot);

        var speedRow = new HBoxContainer();
        speedRow.AddThemeConstantOverride("separation", 5);
        speedRoot.AddChild(speedRow);

        _pauseSpeed = SpeedButton("", "Остановить игровое время", ToggleSimulationPause, 40, "simulation/pause.svg");
        _speed1 = SpeedButton("1×", "Скорость времени 1×", () => SetSimulationSpeed(1), 46);
        _speed4 = SpeedButton("4×", "Скорость времени 4×", () => SetSimulationSpeed(4), 46);
        _speed16 = SpeedButton("16×", "Скорость времени 16×", () => SetSimulationSpeed(16), 50);
        _speedMax = SpeedButton("MAX", $"Максимальная скорость {SimulationSpeedState.MaxMultiplier}×", () => SetSimulationSpeed(SimulationSpeedState.MaxMultiplier), 58);

        speedRow.AddChild(_pauseSpeed);
        speedRow.AddChild(_speed1);
        speedRow.AddChild(_speed4);
        speedRow.AddChild(_speed16);
        speedRow.AddChild(_speedMax);

        _realSpeed = new Label
        {
            Text = "Реальная скорость: 1.0×",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _realSpeed.AddThemeFontSizeOverride("font_size", 10);
        _realSpeed.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        speedRoot.AddChild(_realSpeed);
    }

    private void BuildBottomBar()
    {
        var toolbar = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        toolbar.AnchorLeft = 0.018f;
        toolbar.AnchorRight = 0.982f;
        toolbar.AnchorTop = 0.900f;
        toolbar.AnchorBottom = 0.985f;
        toolbar.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.96f));
        AddChild(toolbar);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 7);
        margin.AddThemeConstantOverride("margin_bottom", 7);
        toolbar.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        margin.AddChild(row);

        var secondary = new HBoxContainer();
        secondary.AddThemeConstantOverride("separation", 7);
        row.AddChild(secondary);

        AddGameTool(secondary, "Летопись", "simulation/history.svg", ShowChronicle, 155);
        AddGameTool(secondary, "События", "simulation/event.svg", ShowEvents, 155);

        row.AddChild(Separator());

        var camera = new PanelContainer
        {
            CustomMinimumSize = new Vector2(320, 0)
        };
        camera.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        row.AddChild(camera);

        var cameraMargin = new MarginContainer();
        cameraMargin.AddThemeConstantOverride("margin_left", 8);
        cameraMargin.AddThemeConstantOverride("margin_right", 8);
        cameraMargin.AddThemeConstantOverride("margin_top", 4);
        cameraMargin.AddThemeConstantOverride("margin_bottom", 4);
        camera.AddChild(cameraMargin);

        var cameraRow = new HBoxContainer();
        cameraRow.AddThemeConstantOverride("separation", 5);
        cameraMargin.AddChild(cameraRow);

        var cameraLabel = new Label
        {
            Text = "Камера",
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(54, 0)
        };
        cameraLabel.AddThemeFontSizeOverride("font_size", 10);
        cameraLabel.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        cameraRow.AddChild(cameraLabel);

        cameraRow.AddChild(ActionButton("−", "Отдалить", () => CameraZoomOutRequested?.Invoke(), 36));
        cameraRow.AddChild(ActionButton("+", "Приблизить", () => CameraZoomInRequested?.Invoke(), 36));
        cameraRow.AddChild(ActionButton("Центр", "Вернуть камеру к центру", () => CameraCenterRequested?.Invoke(), 64));
        cameraRow.AddChild(ActionButton("Сброс", "Сбросить положение и масштаб", () => CameraResetRequested?.Invoke(), 64));

        var tech = new PanelContainer
        {
            CustomMinimumSize = new Vector2(160, 0)
        };
        tech.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        row.AddChild(tech);
        _techPanel = tech;

        var techMargin = new MarginContainer();
        techMargin.AddThemeConstantOverride("margin_left", 8);
        techMargin.AddThemeConstantOverride("margin_right", 8);
        techMargin.AddThemeConstantOverride("margin_top", 4);
        techMargin.AddThemeConstantOverride("margin_bottom", 4);
        tech.AddChild(techMargin);

        var techRow = new HBoxContainer();
        techRow.AddThemeConstantOverride("separation", 8);
        techMargin.AddChild(techRow);

        _fps = AddTechStat(techRow, "FPS", out _fpsBox);
        _tps = AddTechStat(techRow, "TPS", out _tpsBox);
        _tick = AddTechStat(techRow, "TICK", out _tickBox);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var systemTools = new HBoxContainer();
        systemTools.AddThemeConstantOverride("separation", 6);
        row.AddChild(systemTools);

        systemTools.AddChild(ToolButton("Сохранить", "actions/apply.svg", () => SaveRequested?.Invoke(), 118));
        systemTools.AddChild(ToolButton("Настройки", "menu/settings.svg", () => SettingsRequested?.Invoke(), 126));
        systemTools.AddChild(ToolButton("Меню", "actions/more.svg", () => PauseRequested?.Invoke(), 92));
    }

    private void BuildSelectionPanel()
    {
        _selectionPanel = new SelectionPanel { MouseFilter = MouseFilterEnum.Stop };
        _selectionPanel.AnchorLeft = 0.79f;
        _selectionPanel.AnchorRight = 0.98f;
        _selectionPanel.AnchorTop = 0.118f;
        _selectionPanel.AnchorBottom = 0.885f;
        AddChild(_selectionPanel);
    }

    private void BuildEventPopups()
    {
        _eventPopups = new WorldEventPopupHost { ZIndex = 20 };
        _eventPopups.AnchorLeft = 0.10f;
        _eventPopups.AnchorRight = 0.46f;
        _eventPopups.AnchorTop = 0.54f;
        _eventPopups.AnchorBottom = 0.89f;
        AddChild(_eventPopups);
    }

    private static Label AddStat(Container parent, string caption, string value)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(58, 0),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        box.AddThemeConstantOverride("separation", 0);

        var name = new Label
        {
            Text = caption,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        name.AddThemeFontSizeOverride("font_size", 8);
        name.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.86f));
        box.AddChild(name);

        var data = new Label
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        data.AddThemeFontSizeOverride("font_size", 16);
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        box.AddChild(data);

        parent.AddChild(box);
        return data;
    }

    private static Label AddTechStat(Container parent, string caption, out Control boxControl)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(40, 0),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        box.AddThemeConstantOverride("separation", 0);
        boxControl = box;

        var name = new Label { Text = caption, HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 8);
        name.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        box.AddChild(name);

        var value = new Label { Text = "0", HorizontalAlignment = HorizontalAlignment.Center };
        value.AddThemeFontSizeOverride("font_size", 13);
        value.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        box.AddChild(value);

        parent.AddChild(box);
        return value;
    }

    private static Button SpeedButton(
        string text,
        string tooltip,
        Action callback,
        float width,
        string? iconPath = null)
    {
        var button = new Button
        {
            Text = text,
            Icon = iconPath is null ? null : EvolitIcons.Load(iconPath),
            TooltipText = tooltip,
            ToggleMode = true,
            Alignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(width, 36)
        };
        button.Pressed += callback;
        return button;
    }

    private void AddGameTool(
        Container parent,
        string text,
        string icon,
        Action callback,
        float width,
        bool expand = false)
    {
        var button = ToolButton(text, icon, () =>
        {
            callback();
            RefreshToolSelection(text);
        }, width);

        button.ToggleMode = true;
        if (expand)
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _toolButtons[text] = button;
        parent.AddChild(button);
    }

    private static Button ToolButton(
        string text,
        string icon,
        Action callback,
        float width)
    {
        var button = new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(icon),
            TooltipText = text,
            Alignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(width, 42)
        };
        button.Pressed += callback;
        return button;
    }

    private static Button ActionButton(
        string text,
        string tooltip,
        Action callback,
        float width)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            Alignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(width, 34)
        };
        button.Pressed += callback;
        return button;
    }

    private static ColorRect Separator()
    {
        return new ColorRect
        {
            Color = new Color(EvolitPalette.FogBlue, 0.16f),
            CustomMinimumSize = new Vector2(1, 30),
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

    private void ApplyDiagnosticVisibility()
    {
        var showAny = _showFps || _showPerformance;
        if (_techPanel is not null) _techPanel.Visible = showAny;
        if (_fpsBox is not null) _fpsBox.Visible = showAny;
        if (_tpsBox is not null) _tpsBox.Visible = _showPerformance;
        if (_tickBox is not null) _tickBox.Visible = _showPerformance;
    }

    private void RefreshStats()
    {
        if (_statsProvider is null)
            return;

        var stats = _statsProvider.GetSnapshot();
        if (_day is not null) _day.Text = stats.Day.ToString();
        if (_time is not null) _time.Text = stats.GameTime;
        if (_creatures is not null) _creatures.Text = stats.CreatureCount.ToString();
        if (_plants is not null) _plants.Text = stats.PlantCount.ToString();
        if (_species is not null) _species.Text = stats.SpeciesCount.ToString();
        if (_subspecies is not null) _subspecies.Text = stats.SubspeciesCount.ToString();
        if (_fps is not null && (_showFps || _showPerformance)) _fps.Text = $"{stats.Fps:0}";
        if (_tps is not null && _showPerformance) _tps.Text = $"{stats.Tps:0}";
        if (_tick is not null && _showPerformance) _tick.Text = stats.Tick.ToString();

        if (_realSpeed is not null)
        {
            var effective = stats.Tps / GameTimeController.BaseTicksPerSecond;
            _realSpeed.Text = $"Реальная скорость: {effective:0.0}×";
        }
    }

    private void ToggleSimulationPause()
    {
        if (_speed is null)
            return;

        _speed.TogglePause();
        RecordSpeedEvent(
            _speed.Paused
                ? "Игровое время приостановлено"
                : $"Игровое время продолжено на {_speed.DisplayMode}");
    }

    private void SetSimulationSpeed(int multiplier)
    {
        if (_speed is null)
            return;

        _speed.SetMultiplier(multiplier);
        RecordSpeedEvent($"Скорость времени: {_speed.DisplayMode}");
    }

    private void RecordSpeedEvent(string title)
    {
        if (_world is null)
            return;

        var stats = _statsProvider?.GetSnapshot() ?? default;
        _world.AddEvent(
            stats.Day,
            stats.GameTime,
            DemoEventCategory.System,
            title,
            "Изменено управление временем.",
            "simulation/timeline.svg",
            DemoEventKind.Speed,
            DemoEventSeverity.Info);
    }

    private void RefreshSpeedButtons()
    {
        if (_speed is null)
            return;

        if (_pauseSpeed is not null) _pauseSpeed.ButtonPressed = _speed.Paused;
        if (_speed1 is not null) _speed1.ButtonPressed = !_speed.Paused && _speed.Multiplier == 1;
        if (_speed4 is not null) _speed4.ButtonPressed = !_speed.Paused && _speed.Multiplier == 4;
        if (_speed16 is not null) _speed16.ButtonPressed = !_speed.Paused && _speed.Multiplier == 16;
        if (_speedMax is not null) _speedMax.ButtonPressed = !_speed.Paused && _speed.Multiplier == SimulationSpeedState.MaxMultiplier;
    }

    private void RefreshSelection()
    {
        if (_selectedEntity is not null)
            _selectionPanel?.RefreshCurrent();
    }

    private void HandleWorldEvent(DemoEventEntry entry)
    {
        _eventPopups?.ShowEvent(entry);
    }

    private void ShowEvolution()
    {
        if (_world is null || _statsProvider is null)
            return;

        OpenTool(new EvolutionPanel(), panel =>
        {
            panel.Configure(_world, _statsProvider);
            panel.CloseRequested += CloseActiveToolAnimated;
        });
    }

    private void ShowLineage()
    {
        if (_world is null)
            return;

        OpenTool(new LineagePanel(), panel =>
        {
            panel.Configure(_world);
            panel.CloseRequested += CloseActiveToolAnimated;
        });
    }

    private void ShowChronicle()
    {
        if (_world is null)
            return;

        OpenTool(new ChroniclePanel(), panel =>
        {
            panel.Configure(_world);
            panel.CloseRequested += CloseActiveToolAnimated;
        });
    }

    private void ShowEvents()
    {
        if (_world is null)
            return;

        OpenTool(new EventsPanel(), panel =>
        {
            panel.Configure(_world);
            panel.CloseRequested += CloseActiveToolAnimated;
        });
    }

    private void ShowWorldStats()
    {
        if (_world is null || _statsProvider is null)
            return;

        OpenTool(new WorldStatisticsPanel(), panel =>
        {
            panel.Configure(_world, _statsProvider);
            panel.CloseRequested += CloseActiveToolAnimated;
        });
    }

    private void OpenTool<T>(T panel, Action<T> configure)
        where T : Control
    {
        RemoveActiveToolImmediately();

        configure(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.Modulate = new Color(1, 1, 1, 0);
        AddChild(panel);
        _activeTool = panel;

        CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad)
            .TweenProperty(panel, "modulate", Colors.White, 0.16);
    }

    private void CloseActiveToolAnimated()
    {
        if (_activeTool is null)
            return;

        var old = _activeTool;
        _activeTool = null;
        RefreshToolSelection(null);

        var tween = CreateTween()
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(old, "modulate", new Color(1, 1, 1, 0), 0.12);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!IsInstanceValid(old))
                return;
            RemoveChild(old);
            old.QueueFree();
        }));
    }

    private void RemoveActiveToolImmediately()
    {
        if (_activeTool is null)
            return;

        var old = _activeTool;
        _activeTool = null;
        if (IsInstanceValid(old))
        {
            RemoveChild(old);
            old.QueueFree();
        }
        RefreshToolSelection(null);
    }

    private void RefreshToolSelection(string? selected)
    {
        foreach (var pair in _toolButtons)
            pair.Value.ButtonPressed = pair.Key == selected;
    }
}
