using System;
using System.Collections.Generic;
using Evolit.Input;
using Evolit.Settings;
using Godot;

namespace Evolit.UI;

public sealed partial class SettingsMenu : Control
{
    public event Action? BackRequested;
    public event Action<string, ToastKind>? NotificationRequested;
    public event Action<AppSettings>? SettingsApplied;

    private readonly string[] _categories =
        ["Графика", "Звук", "Интерфейс", "Геймплей", "Управление"];

    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        ["Графика"] = "settings/graphics.svg",
        ["Звук"] = "settings/sound.svg",
        ["Интерфейс"] = "settings/interface.svg",
        ["Геймплей"] = "settings/gameplay.svg",
        ["Управление"] = "settings/controls.svg"
    };

    private readonly Dictionary<string, Button> _categoryButtons = new();
    private readonly Dictionary<string, Button> _bindingButtons = new();

    private SettingsStore? _store;
    private AppSettings _state = AppSettings.Default();
    private AppSettings _original = AppSettings.Default();
    private VBoxContainer? _content;
    private Label? _sectionTitle;
    private Label? _status;
    private Button? _applyButton;
    private EvolitConfirmDialog? _confirm;
    private string _currentCategory = "Графика";
    private string? _captureAction;
    private bool _dirty;

    public void Configure(SettingsStore store)
    {
        _store = store;
        _original = store.Load().Clone();
        _state = _original.Clone();
    }

    public override void _Ready()
    {
        Build();
        ShowCategory(_currentCategory);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_captureAction is not null)
            return;

        if (@event.IsActionPressed("cancel"))
        {
            RequestBack();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_captureAction is null)
            return;

        if (@event is InputEventKey key)
        {
            if (!key.Pressed || key.Echo)
                return;

            var code = key.PhysicalKeycode != Key.None
                ? key.PhysicalKeycode
                : key.Keycode;

            if (code == OS.FindKeycodeFromString("Escape"))
            {
                CancelCapture();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        else if (@event is InputEventMouseButton mouse)
        {
            if (!mouse.Pressed)
                return;
        }
        else
        {
            return;
        }

        var encoded = InputBindings.Encode(@event);
        if (encoded is null)
            return;

        var action = _captureAction;
        var conflict = InputBindings.FindConflict(
            action,
            encoded,
            _state.KeyBindings);

        if (conflict is null)
        {
            CommitBinding(action, encoded, null);
        }
        else
        {
            _captureAction = null;
            _confirm?.ShowDialog(
                "Конфликт управления",
                $"«{InputBindings.DescribeEncoded(encoded)}» уже используется действием «{InputBindings.FriendlyName(conflict)}». Поменять назначения местами?",
                "Поменять",
                () => CommitBinding(action, encoded, conflict));
        }

        GetViewport().SetInputAsHandled();
    }

    private void Build()
    {
        var dim = new ColorRect
        {
            Color = new Color(0.010f, 0.045f, 0.055f, 0.82f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", UiMetrics.Space(46));
        outer.AddThemeConstantOverride("margin_right", UiMetrics.Space(46));
        outer.AddThemeConstantOverride("margin_top", UiMetrics.Space(34));
        outer.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(34));
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.97f));
        outer.AddChild(card);

        var cardMargin = new MarginContainer();
        cardMargin.AddThemeConstantOverride("margin_left", UiMetrics.Space(24));
        cardMargin.AddThemeConstantOverride("margin_right", UiMetrics.Space(24));
        cardMargin.AddThemeConstantOverride("margin_top", UiMetrics.Space(20));
        cardMargin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(18));
        card.AddChild(cardMargin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", UiMetrics.Space(10));
        cardMargin.AddChild(root);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", UiMetrics.Space(16));
        root.AddChild(heading);

        var titles = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        heading.AddChild(titles);

        var eyebrow = new Label { Text = "SYSTEM  ·  PREFERENCES" };
        eyebrow.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        eyebrow.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.EvolutionCyan, 0.78f));
        titles.AddChild(eyebrow);

        var title = new Label { Text = "Настройки" };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(32));
        titles.AddChild(title);

        var subtitle = new Label
        {
            Text = "Только реальные параметры. Изменения сохраняются отдельно от миров."
        };
        subtitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var topBack = new Button
        {
            Text = "Назад",
            Icon = EvolitIcons.Load("actions/back.svg"),
            CustomMinimumSize = UiMetrics.Size(108, 40)
        };
        topBack.Pressed += RequestBack;
        UiMotion.BindButton(topBack);
        heading.AddChild(topBack);

        root.AddChild(new HSeparator());

        var body = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", UiMetrics.Space(12));
        root.AddChild(body);

        var navPanel = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(196, 0)
        };
        navPanel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.SectionStyle());
        body.AddChild(navPanel);

        var navMargin = new MarginContainer();
        navMargin.AddThemeConstantOverride("margin_left", UiMetrics.Space(10));
        navMargin.AddThemeConstantOverride("margin_right", UiMetrics.Space(10));
        navMargin.AddThemeConstantOverride("margin_top", UiMetrics.Space(10));
        navMargin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(10));
        navPanel.AddChild(navMargin);

        var nav = new VBoxContainer();
        nav.AddThemeConstantOverride("separation", UiMetrics.Space(5));
        navMargin.AddChild(nav);

        foreach (var category in _categories)
        {
            var button = new Button
            {
                Text = category,
                Icon = EvolitIcons.Load(CategoryIcons[category]),
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                CustomMinimumSize = UiMetrics.Size(0, 42)
            };

            var captured = category;
            button.Pressed += () => ShowCategory(captured);
            UiMotion.BindButton(button);
            _categoryButtons[category] = button;
            nav.AddChild(button);
        }

        var contentPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentPanel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.SectionStyle());
        body.AddChild(contentPanel);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", UiMetrics.Space(20));
        contentMargin.AddThemeConstantOverride("margin_right", UiMetrics.Space(20));
        contentMargin.AddThemeConstantOverride("margin_top", UiMetrics.Space(16));
        contentMargin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(16));
        contentPanel.AddChild(contentMargin);

        var contentRoot = new VBoxContainer();
        contentRoot.AddThemeConstantOverride("separation", UiMetrics.Space(8));
        contentMargin.AddChild(contentRoot);

        _sectionTitle = new Label();
        _sectionTitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(23));
        _sectionTitle.AddThemeColorOverride(
            "font_color",
            EvolitPalette.SoftAqua);
        contentRoot.AddChild(_sectionTitle);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        contentRoot.AddChild(scroll);

        _content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", UiMetrics.Space(8));
        scroll.AddChild(_content);

        root.AddChild(new HSeparator());

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", UiMetrics.Space(7));
        root.AddChild(footer);

        _status = new Label
        {
            Text = "Нет несохранённых изменений.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _status.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        _status.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        footer.AddChild(_status);

        var revert = new Button
        {
            Text = "Отменить изменения",
            CustomMinimumSize = UiMetrics.Size(158, 40)
        };
        revert.Pressed += RevertChanges;
        UiMotion.BindButton(revert);
        footer.AddChild(revert);

        var defaults = new Button
        {
            Text = "По умолчанию",
            Icon = EvolitIcons.Load("actions/reset.svg"),
            CustomMinimumSize = UiMetrics.Size(142, 40)
        };
        defaults.Pressed += ResetSettings;
        UiMotion.BindButton(defaults);
        footer.AddChild(defaults);

        _applyButton = new Button
        {
            Text = "Применить",
            Icon = EvolitIcons.Load("actions/apply.svg"),
            CustomMinimumSize = UiMetrics.Size(124, 40),
            Disabled = true
        };
        _applyButton.Pressed += ApplySettings;
        UiMotion.BindButton(_applyButton);
        footer.AddChild(_applyButton);

        _confirm = new EvolitConfirmDialog();
        _confirm.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_confirm);
    }

    private void ShowCategory(string category)
    {
        _currentCategory = category;
        _bindingButtons.Clear();

        foreach (var pair in _categoryButtons)
            pair.Value.ButtonPressed = pair.Key == category;

        if (_sectionTitle is not null)
            _sectionTitle.Text = category;

        if (_content is null)
            return;

        ClearChildren(_content);

        switch (category)
        {
            case "Графика":
                BuildGraphics();
                break;
            case "Звук":
                BuildSound();
                break;
            case "Интерфейс":
                BuildInterface();
                break;
            case "Геймплей":
                BuildGameplay();
                break;
            case "Управление":
                BuildControls();
                break;
        }

        UiMotion.BindTree(_content);
        UiMotion.FadeIn(_content, new Vector2(UiMetrics.Px(10), 0));
    }

    private void BuildGraphics()
    {
        if (_content is null)
            return;

        _content.AddChild(SectionLabel(
            "Отображение",
            "Параметры окна применяются сразу после сохранения."));

        _content.AddChild(OptionRow(
            "Режим экрана",
            "Оконный, без рамки или полноэкранный режим.",
            ["Оконный", "Без рамки", "Полноэкранный"],
            _state.DisplayMode,
            value => _state.DisplayMode = value));

        _content.AddChild(OptionRow(
            "Разрешение",
            "Размер окна в оконном и безрамочном режиме.",
            ["1280×720", "1920×1080", "2560×1440"],
            _state.Resolution,
            value => _state.Resolution = value));

        _content.AddChild(ToggleRow(
            "Вертикальная синхронизация",
            "Синхронизировать вывод кадров с монитором.",
            _state.VSync,
            value => _state.VSync = value));

        _content.AddChild(OptionRow(
            "Детализация мира",
            "Количество декоративных деталей карты и объектов.",
            ["Низкая", "Средняя", "Высокая"],
            _state.DetailLevel,
            value => _state.DetailLevel = value));

        _content.AddChild(OptionRow(
            "Качество воды",
            "Детализация визуальных слоёв воды.",
            ["Низкое", "Среднее", "Высокое"],
            _state.WaterQuality,
            value => _state.WaterQuality = value));

        _content.AddChild(Hint(
            "Тени и отдельный уровень пост-эффектов пока не показываются: соответствующих renderer-систем в Evolit ещё нет."));
    }

    private void BuildSound()
    {
        if (_content is null)
            return;

        _content.AddChild(SectionLabel(
            "Громкость",
            "Показываются только реально существующие audio buses."));

        _content.AddChild(SliderRow(
            "Общая громкость",
            "Master bus.",
            _state.MasterVolume,
            0,
            100,
            value => _state.MasterVolume = value,
            "%"));

        _content.AddChild(ToggleRow(
            "Без звука",
            "Отключить все доступные аудиоканалы.",
            _state.Mute,
            value => _state.Mute = value));

        var extraBusCount = 0;

        if (SettingsRuntime.HasAudioBus("Music"))
        {
            extraBusCount++;
            _content.AddChild(SliderRow(
                "Музыка",
                "Громкость Music bus.",
                _state.MusicVolume,
                0,
                100,
                value => _state.MusicVolume = value,
                "%"));
        }

        if (SettingsRuntime.HasAudioBus("Effects"))
        {
            extraBusCount++;
            _content.AddChild(SliderRow(
                "Эффекты",
                "Громкость Effects bus.",
                _state.EffectsVolume,
                0,
                100,
                value => _state.EffectsVolume = value,
                "%"));
        }

        if (SettingsRuntime.HasAudioBus("UI"))
        {
            extraBusCount++;
            _content.AddChild(SliderRow(
                "Интерфейс",
                "Громкость UI bus.",
                _state.UiVolume,
                0,
                100,
                value => _state.UiVolume = value,
                "%"));
        }

        if (extraBusCount == 0)
        {
            _content.AddChild(Hint(
                "В проекте пока есть только Master. Music / Effects / UI появятся здесь автоматически после добавления соответствующих audio buses."));
        }
    }

    private void BuildInterface()
    {
        if (_content is null)
            return;

        _content.AddChild(SectionLabel(
            "Внешний вид",
            "Масштаб и текст применяются ко вновь построенным экранам без перезапуска приложения."));

        _content.AddChild(OptionRow(
            "Масштаб интерфейса",
            "Размер интерактивных элементов и основных панелей.",
            ["90%", "100%", "110%", "125%"],
            _state.UiScale,
            value => _state.UiScale = value));

        _content.AddChild(OptionRow(
            "Размер текста",
            "Единая типографическая шкала интерфейса.",
            ["Компактный", "Обычный", "Крупный"],
            _state.TextSize,
            value => _state.TextSize = value));

        _content.AddChild(OptionRow(
            "Анимации интерфейса",
            "Полные переходы, reduced motion или мгновенный UI.",
            ["Выключены", "Уменьшенные", "Полные"],
            _state.MotionMode,
            value => _state.MotionMode = value));

        _content.AddChild(ToggleRow(
            "Подсказки",
            "Показывать tooltip для интерактивных элементов.",
            _state.Tooltips,
            value => _state.Tooltips = value));

        _content.AddChild(SectionLabel(
            "Диагностика",
            "Технические показатели можно показывать прямо в игровом HUD."));

        _content.AddChild(ToggleRow(
            "Показывать FPS",
            "Отображать текущую частоту кадров.",
            _state.ShowFps,
            value => _state.ShowFps = value));

        _content.AddChild(ToggleRow(
            "Расширенная производительность",
            "Дополнительно показывать TPS и номер simulation tick.",
            _state.ShowPerformance,
            value => _state.ShowPerformance = value));
    }

    private void BuildGameplay()
    {
        if (_content is null)
            return;

        _content.AddChild(SectionLabel(
            "Камера",
            "Параметры, которые реально используются WorldView."));

        _content.AddChild(SliderRow(
            "Скорость камеры",
            "Скорость перемещения с клавиатуры.",
            _state.CameraSpeed,
            1,
            10,
            value => _state.CameraSpeed = value,
            "×"));

        _content.AddChild(ToggleRow(
            "Плавное приближение",
            "Интерполировать zoom вместо мгновенного скачка.",
            _state.SmoothZoom,
            value => _state.SmoothZoom = value));

        _content.AddChild(Hint(
            "Отдельный toggle автопаузы скрыт: текущая flow-архитектура останавливает GameScreen при выходе из игрового состояния автоматически."));
    }

    private void BuildControls()
    {
        if (_content is null)
            return;

        _content.AddChild(SectionLabel(
            "Клавиатура и мышь",
            "Нажмите назначение, затем новую клавишу или кнопку мыши. Esc отменяет захват."));

        foreach (var action in InputBindings.RebindableActions)
            _content.AddChild(BindingRow(action));

        var resetAll = new Button
        {
            Text = "Сбросить всё управление",
            Icon = EvolitIcons.Load("actions/reset.svg"),
            CustomMinimumSize = UiMetrics.Size(210, 40),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        resetAll.Pressed += () =>
        {
            _state.KeyBindings.Clear();
            MarkDirty("Управление возвращено к значениям по умолчанию.");
            ShowCategory("Управление");
        };
        UiMotion.BindButton(resetAll);
        _content.AddChild(resetAll);
    }

    private Control OptionRow(
        string title,
        string description,
        string[] items,
        int selected,
        Action<int> setter)
    {
        var option = new OptionButton
        {
            CustomMinimumSize = UiMetrics.Size(230, 38)
        };

        foreach (var item in items)
            option.AddItem(item);

        option.Select(Math.Clamp(selected, 0, items.Length - 1));
        option.ItemSelected += index =>
        {
            setter((int)index);
            MarkDirty();
        };

        return SettingRow(title, description, option);
    }

    private Control ToggleRow(
        string title,
        string description,
        bool value,
        Action<bool> setter)
    {
        var toggle = new CheckButton
        {
            Text = value ? "Вкл." : "Выкл.",
            ButtonPressed = value,
            CustomMinimumSize = UiMetrics.Size(112, 38)
        };

        toggle.Toggled += pressed =>
        {
            toggle.Text = pressed ? "Вкл." : "Выкл.";
            setter(pressed);
            MarkDirty();
        };

        return SettingRow(title, description, toggle);
    }

    private Control SliderRow(
        string title,
        string description,
        double value,
        double min,
        double max,
        Action<double> setter,
        string suffix)
    {
        var controls = new HBoxContainer
        {
            CustomMinimumSize = UiMetrics.Size(330, 38)
        };

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = UiMetrics.Size(238, 0)
        };
        slider.Modulate = EvolitPalette.SoftAqua;

        var valueLabel = new Label
        {
            Text = $"{value:0}{suffix}",
            CustomMinimumSize = UiMetrics.Size(58, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        slider.ValueChanged += next =>
        {
            setter(next);
            valueLabel.Text = $"{next:0}{suffix}";
            MarkDirty();
        };

        controls.AddChild(slider);
        controls.AddChild(valueLabel);
        return SettingRow(title, description, controls);
    }

    private Control BindingRow(RebindableAction action)
    {
        var controls = new HBoxContainer
        {
            CustomMinimumSize = UiMetrics.Size(292, 38)
        };
        controls.AddThemeConstantOverride("separation", UiMetrics.Space(6));

        var binding = new Button
        {
            Text = InputBindings.Describe(action.Id, _state.KeyBindings),
            CustomMinimumSize = UiMetrics.Size(196, 38),
            TooltipText = "Изменить назначение"
        };
        binding.Pressed += () => BeginCapture(action.Id);
        UiMotion.BindButton(binding);
        controls.AddChild(binding);
        _bindingButtons[action.Id] = binding;

        var reset = new Button
        {
            Text = "↺",
            TooltipText = "Сбросить только это действие",
            CustomMinimumSize = UiMetrics.Size(42, 38)
        };
        reset.Pressed += () =>
        {
            _state.KeyBindings.Remove(action.Id);
            MarkDirty($"{action.Label}: значение по умолчанию.");
            ShowCategory("Управление");
        };
        UiMotion.BindButton(reset);
        controls.AddChild(reset);

        return SettingRow(
            action.Label,
            action.Id == "terrain_inspect"
                ? "Удерживайте кнопку для просмотра параметров гекса."
                : "Изменяемое назначение InputMap.",
            controls);
    }

    private Control SettingRow(
        string title,
        string description,
        Control control)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(0, 58)
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.SubtleSectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(12));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(12));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(8));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(8));
        panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiMetrics.Space(18));
        margin.AddChild(row);

        var copy = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        copy.AddThemeConstantOverride("separation", UiMetrics.Space(1));
        row.AddChild(copy);

        var name = new Label { Text = title };
        name.AddThemeFontSizeOverride("font_size", UiMetrics.Font(14));
        copy.AddChild(name);

        var hint = new Label
        {
            Text = description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        hint.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.FogBlue, 0.86f));
        copy.AddChild(hint);

        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(control);
        return panel;
    }

    private static Control SectionLabel(string title, string text)
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", UiMetrics.Space(1));

        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(16));
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        root.AddChild(label);

        var subtitle = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        subtitle.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.FogBlue, 0.76f));
        root.AddChild(subtitle);

        return root;
    }

    private static Label Hint(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        label.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.FogBlue, 0.76f));
        return label;
    }

    private void BeginCapture(string action)
    {
        _captureAction = action;

        foreach (var pair in _bindingButtons)
            pair.Value.Disabled = pair.Key != action;

        if (_bindingButtons.TryGetValue(action, out var button))
            button.Text = "Нажмите клавишу…";

        SetStatus("Ожидается новая клавиша или кнопка мыши. Esc — отмена.");
    }

    private void CancelCapture()
    {
        _captureAction = null;
        ShowCategory("Управление");
        SetStatus(_dirty
            ? "Есть несохранённые изменения."
            : "Нет несохранённых изменений.");
    }

    private void CommitBinding(
        string action,
        string encoded,
        string? conflict)
    {
        var previous = InputBindings.GetPrimaryEncoding(
            action,
            _state.KeyBindings);

        if (conflict is not null)
        {
            if (previous is null)
                _state.KeyBindings.Remove(conflict);
            else
                _state.KeyBindings[conflict] = previous;
        }

        _state.KeyBindings[action] = encoded;
        _captureAction = null;

        MarkDirty(
            $"{InputBindings.FriendlyName(action)}: {InputBindings.DescribeEncoded(encoded)}.");
        ShowCategory("Управление");
    }

    private void ResetSettings()
    {
        _state = AppSettings.Default();
        _captureAction = null;
        MarkDirty("Загружены значения по умолчанию. Нажмите «Применить».");
        ShowCategory(_currentCategory);
    }

    private void RevertChanges()
    {
        _state = _original.Clone();
        _captureAction = null;
        _dirty = false;

        if (_applyButton is not null)
            _applyButton.Disabled = true;

        SetStatus("Несохранённые изменения отменены.");
        ShowCategory(_currentCategory);
    }

    private void ApplySettings()
    {
        if (_store is null)
        {
            SetStatus("SettingsStore не инициализирован.");
            return;
        }

        if (!_store.Save(_state, out var error))
        {
            SetStatus($"Ошибка: {error}");
            NotificationRequested?.Invoke(
                "Не удалось сохранить настройки",
                ToastKind.Error);
            return;
        }

        InputBindings.ApplyOverrides(_state.KeyBindings);
        SettingsRuntime.Apply(_state);
        UiMetrics.Configure(_state);
        UiMotion.Configure(_state);

        _original = _state.Clone();
        _dirty = false;

        if (_applyButton is not null)
            _applyButton.Disabled = true;

        SettingsApplied?.Invoke(_state.Clone());
        SetStatus("Настройки сохранены и применены.");
        NotificationRequested?.Invoke(
            "Настройки применены",
            ToastKind.Success);
    }

    private void RequestBack()
    {
        if (!_dirty)
        {
            BackRequested?.Invoke();
            return;
        }

        _confirm?.ShowDialog(
            "Есть несохранённые изменения",
            "Выйти из настроек и отбросить изменения?",
            "Выйти без сохранения",
            () =>
            {
                _state = _original.Clone();
                _dirty = false;
                BackRequested?.Invoke();
            });
    }

    private void MarkDirty(string? status = null)
    {
        _dirty = true;
        if (_applyButton is not null)
            _applyButton.Disabled = false;

        SetStatus(status ?? "Есть несохранённые изменения.");
    }

    private void SetStatus(string text)
    {
        if (_status is not null)
            _status.Text = text;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
