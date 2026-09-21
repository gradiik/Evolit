using System;
using System.Collections.Generic;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class GameHud : Control
{
    public event Action? SaveRequested;
    public event Action? SettingsRequested;
    public event Action? PauseRequested;

    private ISimulationStatsProvider? _statsProvider;
    private DemoWorldDataProvider? _world;
    private SimulationSpeedState? _speed;

    private Label? _day;
    private Label? _time;
    private Label? _creatures;
    private Label? _plants;
    private Label? _species;
    private Label? _subspecies;
    private Label? _fps;
    private Label? _tps;
    private Label? _tick;

    private Button? _pauseSpeed;
    private Button? _speed1;
    private Button? _speed2;
    private Button? _speed4;
    private SelectionPanel? _selectionPanel;
    private Control? _activeTool;
    private readonly Dictionary<string, Button> _toolButtons = new();
    private DemoEntity? _selectedEntity;
    private double _refreshTimer;

    public void Configure(ISimulationStatsProvider statsProvider, DemoWorldDataProvider world, SimulationSpeedState speed)
    {
        _statsProvider = statsProvider;
        _world = world;
        _speed = speed;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        BuildTopBar();
        BuildToolBar();
        BuildSelectionPanel();

        if (_speed is not null)
            _speed.Changed += RefreshSpeedButtons;
        if (_world is not null)
            _world.DataChanged += RefreshSelection;

        RefreshStats();
        RefreshSpeedButtons();
    }

    public override void _ExitTree()
    {
        if (_speed is not null)
            _speed.Changed -= RefreshSpeedButtons;
        if (_world is not null)
            _world.DataChanged -= RefreshSelection;
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
        top.AnchorLeft = 0.02f;
        top.AnchorRight = 0.98f;
        top.AnchorTop = 0.016f;
        top.AnchorBottom = 0.084f;
        top.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.92f));
        AddChild(top);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        top.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 13);
        margin.AddChild(row);

        _day = AddStat(row, "ДЕНЬ", "1");
        _time = AddStat(row, "ВРЕМЯ", "00:00");
        _creatures = AddStat(row, "СУЩЕСТВА", "0");
        _plants = AddStat(row, "РАСТЕНИЯ", "0");
        _species = AddStat(row, "ВИДЫ", "0");
        _subspecies = AddStat(row, "ПОДВИДЫ", "0");

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var tech = new PanelContainer();
        tech.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        row.AddChild(tech);

        var techMargin = new MarginContainer();
        techMargin.AddThemeConstantOverride("margin_left", 9);
        techMargin.AddThemeConstantOverride("margin_right", 9);
        techMargin.AddThemeConstantOverride("margin_top", 4);
        techMargin.AddThemeConstantOverride("margin_bottom", 4);
        tech.AddChild(techMargin);

        var techRow = new HBoxContainer();
        techRow.AddThemeConstantOverride("separation", 10);
        techMargin.AddChild(techRow);

        _fps = AddTechStat(techRow, "FPS");
        _tps = AddTechStat(techRow, "TPS");
        _tick = AddTechStat(techRow, "TICK");

        _pauseSpeed = SpeedButton("", "Остановить игровое время", ToggleSimulationPause, "simulation/pause.svg");
        _speed1 = SpeedButton("1×", "Скорость времени 1×", () => SetSimulationSpeed(1));
        _speed2 = SpeedButton("2×", "Скорость времени 2×", () => SetSimulationSpeed(2));
        _speed4 = SpeedButton("4×", "Скорость времени 4×", () => SetSimulationSpeed(4));

        row.AddChild(_pauseSpeed);
        row.AddChild(_speed1);
        row.AddChild(_speed2);
        row.AddChild(_speed4);
    }

    private void BuildToolBar()
    {
        var toolbar = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        toolbar.AnchorLeft = 0.02f;
        toolbar.AnchorRight = 0.98f;
        toolbar.AnchorTop = 0.915f;
        toolbar.AnchorBottom = 0.979f;
        toolbar.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.94f));
        AddChild(toolbar);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 9);
        margin.AddThemeConstantOverride("margin_right", 9);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        toolbar.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        margin.AddChild(row);

        var gameTools = new HBoxContainer();
        gameTools.AddThemeConstantOverride("separation", 5);
        row.AddChild(gameTools);

        AddGameTool(gameTools, "Эволюция", "biology/evolution.svg", ShowEvolution);
        AddGameTool(gameTools, "Древо", "biology/lineage.svg", ShowLineage);
        AddGameTool(gameTools, "Летопись", "simulation/history.svg", ShowChronicle);
        AddGameTool(gameTools, "События", "simulation/event.svg", ShowEvents);
        AddGameTool(gameTools, "Статистика", "settings/performance.svg", ShowWorldStats);

        row.AddChild(new ColorRect
        {
            Color = new Color(EvolitPalette.FogBlue, 0.16f),
            CustomMinimumSize = new Vector2(1, 28),
            MouseFilter = MouseFilterEnum.Ignore
        });

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var systemTools = new HBoxContainer();
        systemTools.AddThemeConstantOverride("separation", 5);
        row.AddChild(systemTools);

        systemTools.AddChild(ToolButton("Сохранить", "actions/apply.svg", () => SaveRequested?.Invoke()));
        systemTools.AddChild(ToolButton("Настройки", "menu/settings.svg", () => SettingsRequested?.Invoke()));
        systemTools.AddChild(ToolButton("Меню", "actions/more.svg", () => PauseRequested?.Invoke()));
    }

    private void BuildSelectionPanel()
    {
        _selectionPanel = new SelectionPanel { MouseFilter = MouseFilterEnum.Stop };
        _selectionPanel.AnchorLeft = 0.79f;
        _selectionPanel.AnchorRight = 0.98f;
        _selectionPanel.AnchorTop = 0.105f;
        _selectionPanel.AnchorBottom = 0.895f;
        AddChild(_selectionPanel);
    }

    private static Label AddStat(Container parent, string caption, string value)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(64, 0) };
        box.AddThemeConstantOverride("separation", 0);

        var name = new Label { Text = caption };
        name.AddThemeFontSizeOverride("font_size", 9);
        name.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.86f));
        box.AddChild(name);

        var data = new Label { Text = value };
        data.AddThemeFontSizeOverride("font_size", 16);
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        box.AddChild(data);

        parent.AddChild(box);
        return data;
    }

    private static Label AddTechStat(Container parent, string caption)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(43, 0) };
        box.AddThemeConstantOverride("separation", 0);

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

    private static Button SpeedButton(string text, string tooltip, Action callback, string? iconPath = null)
    {
        var button = new Button
        {
            Text = text,
            Icon = iconPath is null ? null : EvolitIcons.Load(iconPath),
            TooltipText = tooltip,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(48, 38)
        };
        button.Pressed += callback;
        return button;
    }

    private void AddGameTool(Container parent, string text, string icon, Action callback)
    {
        var button = ToolButton(text, icon, () =>
        {
            callback();
            foreach (var pair in _toolButtons)
                pair.Value.ButtonPressed = pair.Key == text;
        });
        button.ToggleMode = true;
        _toolButtons[text] = button;
        parent.AddChild(button);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("game_pause") && _activeTool is not null)
        {
            CloseActiveTool();
            GetViewport().SetInputAsHandled();
        }
    }

    private static Button ToolButton(string text, string icon, Action callback)
    {
        var button = new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(icon),
            TooltipText = text,
            CustomMinimumSize = new Vector2(0, 36)
        };
        button.Pressed += callback;
        return button;
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
        if (_fps is not null) _fps.Text = $"{stats.Fps:0}";
        if (_tps is not null) _tps.Text = $"{stats.Tps:0}";
        if (_tick is not null) _tick.Text = stats.Tick.ToString();
    }

    private void ToggleSimulationPause()
    {
        if (_speed is null)
            return;

        _speed.TogglePause();
        RecordSpeedEvent(_speed.Paused ? "Игровое время приостановлено" : $"Игровое время продолжено на {_speed.Multiplier}×");
    }

    private void SetSimulationSpeed(int multiplier)
    {
        if (_speed is null)
            return;

        _speed.SetMultiplier(multiplier);
        RecordSpeedEvent($"Скорость времени: {_speed.Multiplier}×");
    }

    private void RecordSpeedEvent(string title)
    {
        if (_world is null)
            return;

        var stats = _statsProvider?.GetSnapshot() ?? default;
        _world.AddEvent(stats.Day, stats.GameTime, DemoEventCategory.System, title, "Изменено управление временем.", "simulation/timeline.svg");
    }

    private void RefreshSpeedButtons()
    {
        if (_speed is null)
            return;

        if (_pauseSpeed is not null) _pauseSpeed.ButtonPressed = _speed.Paused;
        if (_speed1 is not null) _speed1.ButtonPressed = !_speed.Paused && _speed.Multiplier == 1;
        if (_speed2 is not null) _speed2.ButtonPressed = !_speed.Paused && _speed.Multiplier == 2;
        if (_speed4 is not null) _speed4.ButtonPressed = !_speed.Paused && _speed.Multiplier == 4;
    }

    private void RefreshSelection()
    {
        if (_selectedEntity is not null)
            _selectionPanel?.RefreshCurrent();
    }

    private void ShowEvolution()
    {
        if (_world is null || _statsProvider is null)
            return;

        CloseActiveTool();
        var panel = new EvolutionPanel();
        panel.Configure(_world, _statsProvider);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.CloseRequested += CloseActiveTool;
        AddChild(panel);
        _activeTool = panel;
    }

    private void ShowLineage()
    {
        if (_world is null)
            return;

        CloseActiveTool();
        var panel = new LineagePanel();
        panel.Configure(_world);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.CloseRequested += CloseActiveTool;
        AddChild(panel);
        _activeTool = panel;
    }

    private void ShowChronicle()
    {
        if (_world is null)
            return;

        CloseActiveTool();
        var panel = new ChroniclePanel();
        panel.Configure(_world);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.CloseRequested += CloseActiveTool;
        AddChild(panel);
        _activeTool = panel;
    }

    private void ShowEvents()
    {
        if (_world is null)
            return;

        CloseActiveTool();
        var panel = new EventsPanel();
        panel.Configure(_world);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.CloseRequested += CloseActiveTool;
        AddChild(panel);
        _activeTool = panel;
    }

    private void ShowWorldStats()
    {
        if (_world is null || _statsProvider is null)
            return;

        CloseActiveTool();
        var panel = new WorldStatisticsPanel();
        panel.Configure(_world, _statsProvider);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.CloseRequested += CloseActiveTool;
        AddChild(panel);
        _activeTool = panel;
    }

    private void CloseActiveTool()
    {
        if (_activeTool is null)
            return;

        var old = _activeTool;
        _activeTool = null;
        RemoveChild(old);
        old.QueueFree();
        foreach (var button in _toolButtons.Values)
            button.ButtonPressed = false;
    }
}
