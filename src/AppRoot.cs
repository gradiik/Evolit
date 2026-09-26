using System;
using Evolit.Flow;
using Evolit.Game;
using Evolit.Game.Core;
using Evolit.Input;
using Evolit.Save;
using Evolit.Session;
using Evolit.Settings;
using Evolit.UI;
using Evolit.UI.Game;
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
    private CoreSimulationHost? _coreRuntime;
    private GameScreen? _activeGameScreen;
    private GameViewState? _gameViewState;

    private GameFlowState _returnFromSettings = GameFlowState.MainMenu;
    private GameFlowState _returnFromSaves = GameFlowState.MainMenu;
    private int _cachedSaveCount;
    private double _debugTimer;

    public override void _Ready()
    {
        Theme = NatureTechTheme.Create();
        InputBindings.EnsureDefaults();
        SettingsRuntime.Apply(_settings.Load());

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

        if (_confirm?.TryCancel() == true)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_flow.Current == GameFlowState.Game)
        {
            if (_activeGameScreen?.TryCloseActiveTool() == true)
            {
                GetViewport().SetInputAsHandled();
                return;
            }

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
        _session = GameSession.CreateNew(request.WorldName, request.Seed, request.WorldSize);
        ClearGameRuntime();
        _gameViewState = null;

        ShowLoading(
            "Создание мира",
            "Генерация карты и подготовка окружения…",
            () =>
            {
                PrepareGameRuntime();
                if (_session is null)
                    return;

                var manual = _saves.SaveManual(_session, _gameTime, _simulationSpeed, _demoWorld, _coreRuntime?.CaptureSnapshot());
                if (!manual.Success)
                {
                    _toasts?.ShowToast($"Не удалось создать сохранение: {manual.Error}", ToastKind.Error);
                    return;
                }

                var autosave = _saves.CreateAutosave(_session, _gameTime, _simulationSpeed, _demoWorld, _coreRuntime?.CaptureSnapshot());
                if (!autosave.Success)
                    _toasts?.ShowToast("Первый autosave не создан, manual save сохранён.", ToastKind.Warning);

                RefreshSaveCount();
            },
            ShowGame);
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

            var result = _saves.SaveManual(_session, _gameTime, _simulationSpeed, _demoWorld, _coreRuntime?.CaptureSnapshot());
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
        ClearGameRuntime();
        _gameViewState = null;

        if (recovered)
            _toasts?.ShowToast("Основной save восстановлен из backup", ToastKind.Warning);

        ShowLoading(
            "Загрузка сохранения",
            document.Runtime?.Core is null
                ? "Сохранение до Evolit Core: карта будет восстановлена по seed, Core создаст совместимый foundation-state."
                : "Генерация карты по seed и восстановление demo + Core runtime-состояния…",
            () =>
            {
                PrepareGameRuntime(document);
                RefreshSaveCount();
            },
            ShowGame);
    }

    private void ShowLoading(string operation, string workHint, Action work, Action next)
    {
        var loading = new LoadingScreen { Operation = operation };
        SwitchScreen(loading, GameFlowState.Loading);

        Callable.From(() =>
        {
            loading.SetStage("Подготовка сессии", 15, "Интерфейс загрузки готов.");
            Callable.From(() =>
            {
                loading.SetStage("Генерация мира", 45, workHint);
                Callable.From(() =>
                {
                    try
                    {
                        work();
                    }
                    catch (Exception ex)
                    {
                        loading.SetStage("Ошибка", 100, "Не удалось подготовить мир.");
                        _toasts?.ShowToast($"Ошибка подготовки мира: {ex.Message}", ToastKind.Error);
                        Callable.From(ShowMainMenu).CallDeferred();
                        return;
                    }

                    loading.SetStage("Подготовка интерфейса", 85, "Связываем runtime с игровым экраном.");
                    Callable.From(() =>
                    {
                        loading.SetStage("Готово", 100, "Мир готов к наблюдению.");
                        Callable.From(next).CallDeferred();
                    }).CallDeferred();
                }).CallDeferred();
            }).CallDeferred();
        }).CallDeferred();
    }

    private void ShowGame()
    {
        if (_session is null)
        {
            ShowMainMenu();
            return;
        }

        if (_simulationSpeed is null || _gameTime is null || _demoWorld is null || _coreRuntime is null)
            PrepareGameRuntime();

        if (_simulationSpeed is null || _gameTime is null || _demoWorld is null || _coreRuntime is null)
            return;

        var game = new GameScreen();
        game.Configure(_session, _settings.Load(), _simulationSpeed, _gameTime, _demoWorld, _coreRuntime, _gameViewState);
        game.SaveRequested += SaveCurrentManual;
        game.SettingsRequested += () => ShowSettings(GameFlowState.Game);
        game.PauseRequested += ShowPause;
        _activeGameScreen = game;
        SwitchScreen(game, GameFlowState.Game);
    }

    private void ShowPause()
    {
        if (_session is null)
            return;

        CaptureGameViewState();

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

        var result = _saves.SaveManual(_session, _gameTime, _simulationSpeed, _demoWorld, _coreRuntime?.CaptureSnapshot());
        if (result.Success)
        {
            RefreshSaveCount();
            RecordSystemEvent("Игра сохранена", "Создана или обновлена ручная точка сохранения.", "actions/apply.svg");
            _toasts?.ShowToast("Сохранение создано", ToastKind.Success);
        }
        else
        {
            _toasts?.ShowToast($"Не удалось сохранить: {result.Error}", ToastKind.Error);
        }
    }

    private void ShowSettings(GameFlowState returnState)
    {
        _returnFromSettings = returnState;
        if (returnState == GameFlowState.Game)
            CaptureGameViewState();

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
                _gameViewState = null;
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

        var result = _saves.CreateAutosave(_session, _gameTime, _simulationSpeed, _demoWorld, _coreRuntime?.CaptureSnapshot());
        if (result.Success)
            RecordSystemEvent("Автосохранение создано", "Текущее состояние сессии записано в кольцевой autosave.", "simulation/history.svg");
        else
            _toasts?.ShowToast($"Autosave не создан: {result.Error}", ToastKind.Error);

        RefreshSaveCount();
    }

    private void PrepareGameRuntime(SaveDocument? document = null)
    {
        if (_session is null)
            return;

        _simulationSpeed = new SimulationSpeedState();
        _gameTime = new GameTimeController(_simulationSpeed);
        _demoWorld = new DemoWorldDataProvider(_session);

        var runtime = document?.Runtime;
        if (runtime is not null)
        {
            _simulationSpeed.Restore(runtime.Speed.Paused, runtime.Speed.Multiplier);
            _gameTime.Restore(runtime.Time.Day, runtime.Time.MinuteOfDay, runtime.Time.TickCount);
            _demoWorld.RestoreSaveState(runtime.World);
        }

        _coreRuntime = CoreSimulationHost.Create(_demoWorld, _session.Seed, runtime?.Core);
    }

    private void ClearGameRuntime()
    {
        _activeGameScreen = null;
        _simulationSpeed = null;
        _gameTime = null;
        _demoWorld = null;
        _coreRuntime = null;
    }

    private void CaptureGameViewState()
    {
        if (_activeGameScreen is null)
            return;

        _gameViewState = _activeGameScreen.CaptureViewState() ?? _gameViewState;
        _activeGameScreen = null;
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
            icon,
            DemoEventKind.Save,
            DemoEventSeverity.Info);
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
        var render = _activeGameScreen?.GetRenderDiagnostics();
        var renderText = render.HasValue
            ? $"\nMap: {render.Value.VisibleHexes}/{render.Value.TotalHexes} hex · {render.Value.VisibleChunks}/{render.Value.TotalChunks} chunks" +
              $"\nMap cmds≈{render.Value.EstimatedDrawCommands} · rebuilds {render.Value.TerrainRebuilds} · overlay redraw/s {render.Value.OverlayRedrawsPerSecond:0}"
            : string.Empty;
        var core = _coreRuntime?.GetDiagnostics();
        var coreText = core.HasValue
            ? $"\nCore: tick {core.Value.Tick} · t={core.Value.SimulationSeconds:0.0}s · org {core.Value.OrganismCount} · genomes {core.Value.GenomeCount} · lineages {core.Value.LineageCount}" +
              $"\nCore cells {core.Value.CellCount} · last {core.Value.LastTickMilliseconds:0.000} ms · avg {core.Value.AverageTickMilliseconds:0.000} ms · alloc/tick {core.Value.AllocatedBytesPerTick:0} B"
            : string.Empty;

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
            $"Save schema: {SaveManager.SchemaVersion}" +
            renderText +
            coreText);
    }

    private static string FormatPlaytime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
}
