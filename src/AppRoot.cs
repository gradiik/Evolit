using System;
using Evolit.Flow;
using Evolit.Game;
using Evolit.Input;
using Evolit.Save;
using Evolit.Session;
using Evolit.Settings;
using Evolit.UI;
using Evolit.Versioning;
using Godot;

namespace Evolit;

public sealed partial class AppRoot : Control
{
    private readonly GameFlowController _flow = new();
    private readonly SaveManager _saves = new();
    private readonly SettingsStore _settings = new();
    private readonly VersionSelectionStore _versionSelection = new();

    private Control? _screenHost;
    private ToastHost? _toasts;
    private EvolitConfirmDialog? _confirm;
    private DebugOverlay? _debug;

    private GameSession? _session;
    private SimulationSpeedState? _simulationSpeed;
    private GameTimeController? _gameTime;
    private DemoWorldDataProvider? _demoWorld;

    private GameFlowState _returnFromSettings = GameFlowState.MainMenu;
    private GameFlowState _returnFromSaves = GameFlowState.MainMenu;
    private int _cachedSaveCount;
    private double _debugTimer;

    public override void _Ready()
    {
        Theme = NatureTechTheme.Create();
        InputBindings.EnsureDefaults();

        _screenHost = new Control { Name = "ScreenHost" };
        _screenHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_screenHost);

        _toasts = new ToastHost { Name = "ToastHost" };
        _toasts.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_toasts);

        _confirm = new EvolitConfirmDialog { Name = "ConfirmDialog" };
        _confirm.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_confirm);

        _debug = new DebugOverlay { Name = "DebugOverlay" };
        AddChild(_debug);

        RefreshSaveCount();
        ShowMainMenu();
    }

    public override void _Process(double delta)
    {
        if (_session is not null && _flow.Current == GameFlowState.Game)
            _session.Tick(delta);

        _debugTimer += delta;
        if (_debugTimer >= 0.25)
        {
            _debugTimer = 0;
            RefreshDebugOverlay();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_overlay"))
        {
            _debug?.Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!@event.IsActionPressed("game_pause"))
            return;

        if (_flow.Current == GameFlowState.Game)
        {
            ShowPause();
            GetViewport().SetInputAsHandled();
        }
        else if (_flow.Current == GameFlowState.Pause)
        {
            ShowGame();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ShowMainMenu()
    {
        RefreshSaveCount();

        var menu = new MainMenu();
        menu.Configure(_saves.GetLatestLoadable() is not null);
        menu.ContinueRequested += ContinueLatest;
        menu.NewGameRequested += ShowNewGame;
        menu.SavesRequested += () => ShowSaves(GameFlowState.MainMenu);
        menu.SettingsRequested += () => ShowSettings(GameFlowState.MainMenu);
        menu.EncyclopediaRequested += ShowEncyclopedia;
        menu.VersionsRequested += ShowVersions;
        menu.ExitRequested += () => GetTree().Quit();

        SwitchScreen(menu, GameFlowState.MainMenu);
    }

    private void ShowEncyclopedia()
    {
        var screen = new EncyclopediaScreen();
        screen.BackRequested += ShowMainMenu;
        SwitchScreen(screen, GameFlowState.Encyclopedia);
    }

    private void ShowVersions()
    {
        var screen = new VersionsScreen();
        screen.Configure(_versionSelection.LoadTarget());
        screen.BackRequested += ShowMainMenu;
        screen.RollbackRequested += record =>
        {
            _confirm?.ShowDialog(
                "Подготовить откат?",
                $"Вы хотите выбрать Evolit {record.Version} для отката? Исходный код не будет изменён автоматически.",
                "Выбрать версию",
                () =>
                {
                    var result = _versionSelection.SaveTarget(record.Version);
                    if (result == Error.Ok)
                    {
                        _toasts?.ShowToast($"Evolit {record.Version} выбран как цель отката", ToastKind.Success);
                        ShowVersions();
                    }
                    else
                    {
                        _toasts?.ShowToast($"Не удалось сохранить цель отката: {result}", ToastKind.Error);
                    }
                });
        };

        SwitchScreen(screen, GameFlowState.Versions);
    }

    private void ShowNewGame()
    {
        var screen = new NewGameScreen();
        screen.BackRequested += ShowMainMenu;
        screen.CreateRequested += CreateNewSession;
        SwitchScreen(screen, GameFlowState.NewGame);
    }

    private void CreateNewSession(NewGameRequest request)
    {
        var session = GameSession.CreateNew(request.WorldName, request.Seed, request.WorldSize);
        var manual = _saves.SaveManual(session);

        if (!manual.Success)
        {
            _toasts?.ShowToast($"Не удалось создать сохранение: {manual.Error}", ToastKind.Error);
            return;
        }

        var autosave = _saves.CreateAutosave(session);
        if (!autosave.Success)
            _toasts?.ShowToast("Первый autosave не создан, manual save сохранён.", ToastKind.Warning);

        _session = session;
        PrepareGameRuntime();
        RefreshSaveCount();
        ShowLoading("Создание сессии", ShowGame);
    }

    private void ContinueLatest()
    {
        var latest = _saves.GetLatestLoadable();
        if (latest is null)
        {
            _toasts?.ShowToast("Валидных сохранений не найдено.", ToastKind.Warning);
            ShowMainMenu();
            return;
        }

        LoadSlot(latest);
    }

    private void ShowSaves(GameFlowState returnState)
    {
        _returnFromSaves = returnState;

        var screen = new SaveScreen();
        screen.Configure(_saves, _session, returnState == GameFlowState.MainMenu ? SaveScreenMode.Manage : SaveScreenMode.Load);
        screen.BackRequested += ReturnFromSaves;
        screen.LoadRequested += LoadSlot;
        screen.DeleteRequested += slot =>
        {
            _confirm?.ShowDialog(
                "Удалить сохранение?",
                "Основной файл и его backup будут удалены без возможности восстановления.",
                "Удалить",
                () =>
                {
                    if (_saves.Delete(slot, out var error))
                    {
                        RefreshSaveCount();
                        _toasts?.ShowToast("Сохранение удалено", ToastKind.Success);
                        screen.Refresh();
                    }
                    else
                    {
                        _toasts?.ShowToast($"Не удалось удалить: {error}", ToastKind.Error);
                    }
                });
        };
        screen.OverwriteRequested += _ =>
        {
            if (_session is null)
                return;

            var result = _saves.SaveManual(_session);
            if (result.Success)
            {
                RefreshSaveCount();
                RecordSystemEvent("Сохранение перезаписано", "Обновлена ручная точка сохранения.", "actions/apply.svg");
                _toasts?.ShowToast("Сохранение перезаписано", ToastKind.Success);
                screen.Refresh();
            }
            else
            {
                _toasts?.ShowToast($"Ошибка сохранения: {result.Error}", ToastKind.Error);
            }
        };

        SwitchScreen(screen, GameFlowState.Saves);
    }

    private void ReturnFromSaves()
    {
        if (_returnFromSaves == GameFlowState.Pause && _session is not null)
            ShowPause();
        else
            ShowMainMenu();
    }

    private void LoadSlot(SaveSlot slot)
    {
        if (!_saves.TryLoad(slot, out var document, out var recovered, out var error))
        {
            _toasts?.ShowToast($"Не удалось загрузить сохранение: {error}", ToastKind.Error);
            return;
        }

        _session = GameSession.FromSave(document);
        PrepareGameRuntime();
        RefreshSaveCount();

        if (recovered)
            _toasts?.ShowToast("Основной save восстановлен из backup", ToastKind.Warning);

        ShowLoading("Загрузка сохранения", ShowGame);
    }

    private void ShowLoading(string operation, Action next)
    {
        var loading = new LoadingScreen { Operation = operation };
        SwitchScreen(loading, GameFlowState.Loading);

        Callable.From(() =>
        {
            loading.SetProgress(100);
            next();
        }).CallDeferred();
    }

    private void ShowGame()
    {
        if (_session is null)
        {
            ShowMainMenu();
            return;
        }

        if (_simulationSpeed is null || _gameTime is null || _demoWorld is null)
            PrepareGameRuntime();

        if (_simulationSpeed is null || _gameTime is null || _demoWorld is null)
            return;

        var game = new GameScreen();
        game.Configure(_session, _settings.Load(), _simulationSpeed, _gameTime, _demoWorld);
        game.SaveRequested += SaveCurrentManual;
        game.SettingsRequested += () => ShowSettings(GameFlowState.Game);
        game.PauseRequested += ShowPause;
        SwitchScreen(game, GameFlowState.Game);
    }

    private void ShowPause()
    {
        if (_session is null)
            return;

        var pause = new PauseMenu();
        pause.ResumeRequested += ShowGame;
        pause.SaveRequested += SaveCurrentManual;
        pause.LoadRequested += () => ShowSaves(GameFlowState.Pause);
        pause.SettingsRequested += () => ShowSettings(GameFlowState.Pause);
        pause.MainMenuRequested += AskReturnToMainMenu;
        pause.ExitRequested += AskExitGame;
        SwitchScreen(pause, GameFlowState.Pause);
    }

    private void SaveCurrentManual()
    {
        if (_session is null)
            return;

        var result = _saves.SaveManual(_session);
        if (result.Success)
        {
            RefreshSaveCount();
            RecordSystemEvent("Игра сохранена", "Создана или обновлена ручная точка сохранения.", "actions/apply.svg");
            _toasts?.ShowToast("Игра сохранена", ToastKind.Success);
        }
        else
        {
            _toasts?.ShowToast($"Не удалось сохранить: {result.Error}", ToastKind.Error);
        }
    }

    private void ShowSettings(GameFlowState returnState)
    {
        _returnFromSettings = returnState;

        var settings = new SettingsMenu();
        settings.Configure(_settings);
        settings.NotificationRequested += (message, kind) => _toasts?.ShowToast(message, kind);
        settings.BackRequested += ReturnFromSettings;
        SwitchScreen(settings, GameFlowState.Settings);
    }

    private void ReturnFromSettings()
    {
        if (_session is not null && _returnFromSettings == GameFlowState.Pause)
            ShowPause();
        else if (_session is not null && _returnFromSettings == GameFlowState.Game)
            ShowGame();
        else
            ShowMainMenu();
    }

    private void AskReturnToMainMenu()
    {
        _confirm?.ShowDialog(
            "Вернуться в главное меню?",
            "Перед выходом будет создано автосохранение текущей сессии.",
            "Выйти в меню",
            () =>
            {
                TryAutosaveCurrent();
                _session = null;
                ClearGameRuntime();
                ShowMainMenu();
            });
    }

    private void AskExitGame()
    {
        _confirm?.ShowDialog(
            "Выйти из Evolit?",
            "Перед закрытием будет предпринята попытка создать автосохранение.",
            "Выйти",
            () =>
            {
                TryAutosaveCurrent();
                GetTree().Quit();
            });
    }

    private void TryAutosaveCurrent()
    {
        if (_session is null)
            return;

        var result = _saves.CreateAutosave(_session);
        if (result.Success)
            RecordSystemEvent("Автосохранение создано", "Текущее состояние сессии записано в кольцевой autosave.", "simulation/history.svg");
        else
            _toasts?.ShowToast($"Autosave не создан: {result.Error}", ToastKind.Error);

        RefreshSaveCount();
    }

    private void PrepareGameRuntime()
    {
        if (_session is null)
            return;

        _simulationSpeed = new SimulationSpeedState();
        _gameTime = new GameTimeController(_simulationSpeed);
        _demoWorld = new DemoWorldDataProvider(_session);
    }

    private void ClearGameRuntime()
    {
        _simulationSpeed = null;
        _gameTime = null;
        _demoWorld = null;
    }

    private void RecordSystemEvent(string title, string description, string icon)
    {
        if (_demoWorld is null || _gameTime is null)
            return;

        _demoWorld.AddEvent(
            _gameTime.Day,
            _gameTime.FormattedTime,
            DemoEventCategory.System,
            title,
            description,
            icon);
    }

    private void SwitchScreen(Control screen, GameFlowState state)
    {
        if (_screenHost is null)
            return;

        foreach (var child in _screenHost.GetChildren())
        {
            _screenHost.RemoveChild(child);
            child.QueueFree();
        }

        screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _screenHost.AddChild(screen);
        _flow.Transition(state);
        RefreshDebugOverlay();
    }

    private void RefreshSaveCount()
    {
        _cachedSaveCount = _saves.ListSaves().Count;
    }

    private void RefreshDebugOverlay()
    {
        if (_debug is null)
            return;

        var session = _session;
        var playtime = session is null ? "—" : FormatPlaytime(session.PlaytimeSeconds);
        var worldTime = _gameTime is null ? "—" : $"День {_gameTime.Day} · {_gameTime.FormattedTime}";

        _debug.SetData(
            $"EVOLIT {AppVersionCatalog.CurrentVersion} DEBUG\n" +
            $"FPS: {Engine.GetFramesPerSecond():0}\n" +
            $"State: {_flow.Current}\n" +
            $"World time: {worldTime}\n" +
            $"TPS: {(_gameTime?.Tps ?? 0):0} · Tick: {_gameTime?.TickCount ?? 0}\n" +
            $"Save ID: {session?.SaveId ?? "—"}\n" +
            $"World: {session?.WorldName ?? "—"}\n" +
            $"Seed: {session?.Seed ?? "—"}\n" +
            $"Playtime: {playtime}\n" +
            $"Saves: {_cachedSaveCount}\n" +
            $"Save schema: {SaveManager.SchemaVersion}");
    }

    private static string FormatPlaytime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
}
