using System;
using System.Linq;
using Evolit.Flow;
using Evolit.Input;
using Evolit.Save;
using Evolit.Session;
using Evolit.Settings;
using Evolit.UI;
using Godot;

namespace Evolit;

public sealed partial class AppRoot : Control
{
    private readonly GameFlowController _flow = new();
    private readonly SaveManager _saves = new();
    private readonly SettingsStore _settings = new();

    private Control? _screenHost;
    private ToastHost? _toasts;
    private EvolitConfirmDialog? _confirm;
    private DebugOverlay? _debug;
    private GameSession? _session;
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
        menu.EncyclopediaRequested += () => _toasts?.ShowToast("Энциклопедия будет добавлена позже.");
        menu.ExitRequested += () => GetTree().Quit();

        SwitchScreen(menu, GameFlowState.MainMenu);
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

        var game = new GameScreen();
        game.Configure(_session, _settings.Load());
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
        if (!result.Success)
            _toasts?.ShowToast($"Autosave не создан: {result.Error}", ToastKind.Error);

        RefreshSaveCount();
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

        _debug.SetData(
            $"EVOLIT DEBUG\n" +
            $"FPS: {Engine.GetFramesPerSecond():0}\n" +
            $"State: {_flow.Current}\n" +
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
