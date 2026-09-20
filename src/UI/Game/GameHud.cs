using System;
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

        RefreshStats();
        RefreshSpeedButtons();
    }

    public override void _Process(double delta)
    {
        _refreshTimer += delta;
        if (_refreshTimer < 0.25)
            return;

        _refreshTimer = 0;
        RefreshStats();
    }

    public void SetSelectedEntity(DemoEntity? entity)
    {
        _selectionPanel?.SetEntity(entity);
    }

    public void SetSpeedFromAction(int multiplier)
    {
        _speed?.SetMultiplier(multiplier);
    }

    private void BuildTopBar()
    {
        var top = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        top.AnchorLeft = 0.02f;
        top.AnchorRight = 0.98f;
        top.AnchorTop = 0.018f;
        top.AnchorBottom = 0.098f;
        top.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.90f));
        AddChild(top);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        top.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        margin.AddChild(row);

        _day = AddStat(row, "День", "1");
        _time = AddStat(row, "Время", "00:00");
        _creatures = AddStat(row, "Существ", "0");
        _plants = AddStat(row, "Растений", "0");
        _species = AddStat(row, "Видов", "0");
        _subspecies = AddStat(row, "Подвидов", "0");

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _fps = AddStat(row, "FPS", "0");
        _tps = AddStat(row, "TPS", "0");
        _tick = AddStat(row, "Tick", "0");

        _pauseSpeed = SpeedButton("Ⅱ", "Пауза", () => _speed?.TogglePause());
        _speed1 = SpeedButton("1×", "Скорость 1×", () => _speed?.SetMultiplier(1));
        _speed2 = SpeedButton("2×", "Скорость 2×", () => _speed?.SetMultiplier(2));
        _speed4 = SpeedButton("4×", "Скорость 4×", () => _speed?.SetMultiplier(4));

        row.AddChild(_pauseSpeed);
        row.AddChild(_speed1);
        row.AddChild(_speed2);
        row.AddChild(_speed4);
    }

    private void BuildToolBar()
    {
        var toolbar = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        toolbar.AnchorLeft = 0.02f;
        toolbar.AnchorRight = 0.79f;
        toolbar.AnchorTop = 0.90f;
        toolbar.AnchorBottom = 0.98f;
        toolbar.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.92f));
        AddChild(toolbar);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        toolbar.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 7);
        margin.AddChild(row);

        row.AddChild(ToolButton("Эволюция", "biology/evolution.svg", ShowEvolution));
        row.AddChild(ToolButton("Древо", "biology/lineage.svg", ShowLineage));
        row.AddChild(ToolButton("Летопись", "simulation/history.svg", ShowChronicle));
        row.AddChild(ToolButton("События", "simulation/event.svg", ShowEvents));
        row.AddChild(ToolButton("Статистика", "settings/performance.svg", ShowWorldStats));

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        row.AddChild(ToolButton("Сохранить", "actions/apply.svg", () => SaveRequested?.Invoke()));
        row.AddChild(ToolButton("Настройки", "menu/settings.svg", () => SettingsRequested?.Invoke()));
        row.AddChild(ToolButton("Пауза", "simulation/pause.svg", () => PauseRequested?.Invoke()));
    }

    private void BuildSelectionPanel()
    {
        _selectionPanel = new SelectionPanel
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        _selectionPanel.AnchorLeft = 0.795f;
        _selectionPanel.AnchorRight = 0.98f;
        _selectionPanel.AnchorTop = 0.12f;
        _selectionPanel.AnchorBottom = 0.88f;
        AddChild(_selectionPanel);
    }

    private Label AddStat(Container parent, string caption, string value)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);

        var name = new Label { Text = caption };
        name.AddThemeFontSizeOverride("font_size", 10);
        name.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        box.AddChild(name);

        var data = new Label { Text = value };
        data.AddThemeFontSizeOverride("font_size", 15);
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        box.AddChild(data);

        parent.AddChild(box);
        return data;
    }

    private static Button SpeedButton(string text, string tooltip, Action callback)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(48, 38)
        };
        button.Pressed += callback;
        return button;
    }

    private static Button ToolButton(string text, string icon, Action callback)
    {
        var button = new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(icon),
            TooltipText = text,
            CustomMinimumSize = new Vector2(0, 38)
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

    private void RefreshSpeedButtons()
    {
        if (_speed is null)
            return;

        if (_pauseSpeed is not null) _pauseSpeed.ButtonPressed = _speed.Paused;
        if (_speed1 is not null) _speed1.ButtonPressed = !_speed.Paused && _speed.Multiplier == 1;
        if (_speed2 is not null) _speed2.ButtonPressed = !_speed.Paused && _speed.Multiplier == 2;
        if (_speed4 is not null) _speed4.ButtonPressed = !_speed.Paused && _speed.Multiplier == 4;
    }

    private void ShowEvolution()
    {
        ShowGenericTool(
            "Эволюция",
            "UI-точка входа готова. Биологическая система намеренно ещё не подключена.",
            ["Система эволюции будет подключена после создания симуляции.", "Здесь позже появятся управляемые параметры наблюдения и вмешательства."]);
    }

    private void ShowChronicle()
    {
        ShowGenericTool(
            "Летопись мира",
            "Постоянная история важных событий мира.",
            _world?.Chronicle ?? Array.Empty<string>());
    }

    private void ShowEvents()
    {
        ShowGenericTool(
            "События",
            "Недавние системные события текущей сессии.",
            _world?.Events ?? Array.Empty<string>());
    }

    private void ShowWorldStats()
    {
        var stats = _statsProvider?.GetSnapshot() ?? default;
        ShowGenericTool(
            "Статистика мира",
            "Пока реальные только FPS и системный playtime вне этой панели; simulation-показатели остаются нулевыми.",
            [
                $"День: {stats.Day}",
                $"Игровое время: {stats.GameTime}",
                $"Существ: {stats.CreatureCount}",
                $"Растений: {stats.PlantCount}",
                $"Видов: {stats.SpeciesCount}",
                $"Подвидов: {stats.SubspeciesCount}",
                $"FPS: {stats.Fps:0}",
                $"TPS: {stats.Tps:0}",
                $"Tick: {stats.Tick}",
                $"Playtime: {FormatPlaytime(stats.PlaytimeSeconds)}"
            ]);
    }

    private static string FormatPlaytime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private void ShowLineage()
    {
        CloseActiveTool();

        var panel = new LineagePanel();
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.CloseRequested += CloseActiveTool;
        AddChild(panel);
        _activeTool = panel;
    }

    private void ShowGenericTool(string title, string subtitle, System.Collections.Generic.IReadOnlyList<string> lines)
    {
        CloseActiveTool();

        var panel = new GameToolPanel();
        panel.Configure(title, subtitle, lines);
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
    }
}
