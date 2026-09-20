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

    private readonly string[] _categories = ["Графика", "Звук", "Интерфейс", "Геймплей", "Управление"];
    private readonly Dictionary<string, Button> _categoryButtons = new();
    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        ["Графика"] = "settings/graphics.svg",
        ["Звук"] = "settings/sound.svg",
        ["Интерфейс"] = "settings/interface.svg",
        ["Геймплей"] = "settings/gameplay.svg",
        ["Управление"] = "settings/controls.svg"
    };

    private SettingsStore? _store;
    private AppSettings _state = AppSettings.Default();
    private VBoxContainer? _content;
    private Label? _sectionTitle;
    private Label? _status;
    private string _currentCategory = "Графика";

    public void Configure(SettingsStore store)
    {
        _store = store;
        _state = store.Load().Clone();
    }

    public override void _Ready()
    {
        Build();
        ShowCategory(_currentCategory);
    }

    private void Build()
    {
        var dim = new ColorRect
        {
            Color = new Color(0.012f, 0.052f, 0.064f, 0.74f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 64);
        outer.AddThemeConstantOverride("margin_right", 64);
        outer.AddThemeConstantOverride("margin_top", 48);
        outer.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.96f));
        outer.AddChild(card);

        var cardMargin = new MarginContainer();
        cardMargin.AddThemeConstantOverride("margin_left", 30);
        cardMargin.AddThemeConstantOverride("margin_right", 30);
        cardMargin.AddThemeConstantOverride("margin_top", 24);
        cardMargin.AddThemeConstantOverride("margin_bottom", 22);
        card.AddChild(cardMargin);

        var root = new VBoxContainer();
        cardMargin.AddChild(root);

        var heading = new HBoxContainer();
        root.AddChild(heading);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(titles);

        var title = new Label { Text = "Настройки" };
        title.AddThemeFontSizeOverride("font_size", 34);
        titles.AddChild(title);

        var subtitle = new Label { Text = "Сохраняются отдельно от игровых миров" };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var topBack = new Button { Text = "Назад", Icon = EvolitIcons.Load("actions/back.svg"), CustomMinimumSize = new Vector2(118, 44) };
        topBack.Pressed += () => BackRequested?.Invoke();
        heading.AddChild(topBack);

        root.AddChild(new HSeparator());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(body);

        var navPanel = new PanelContainer { CustomMinimumSize = new Vector2(220, 0) };
        navPanel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(navPanel);

        var navMargin = new MarginContainer();
        navMargin.AddThemeConstantOverride("margin_left", 12);
        navMargin.AddThemeConstantOverride("margin_right", 12);
        navMargin.AddThemeConstantOverride("margin_top", 12);
        navMargin.AddThemeConstantOverride("margin_bottom", 12);
        navPanel.AddChild(navMargin);

        var nav = new VBoxContainer();
        navMargin.AddChild(nav);

        foreach (var category in _categories)
        {
            var button = new Button
            {
                Text = category,
                Icon = EvolitIcons.Load(CategoryIcons[category]),
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0, 46)
            };

            var captured = category;
            button.Pressed += () => ShowCategory(captured);
            _categoryButtons[category] = button;
            nav.AddChild(button);
        }

        var contentPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentPanel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(contentPanel);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 26);
        contentMargin.AddThemeConstantOverride("margin_right", 26);
        contentMargin.AddThemeConstantOverride("margin_top", 22);
        contentMargin.AddThemeConstantOverride("margin_bottom", 22);
        contentPanel.AddChild(contentMargin);

        var contentRoot = new VBoxContainer();
        contentMargin.AddChild(contentRoot);

        _sectionTitle = new Label();
        _sectionTitle.AddThemeFontSizeOverride("font_size", 25);
        _sectionTitle.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        contentRoot.AddChild(_sectionTitle);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        contentRoot.AddChild(scroll);

        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 13);
        scroll.AddChild(_content);

        root.AddChild(new HSeparator());

        var footer = new HBoxContainer();
        root.AddChild(footer);

        _status = new Label
        {
            Text = "Изменения записываются только по кнопке «Применить».",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _status.AddThemeFontSizeOverride("font_size", 13);
        _status.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        footer.AddChild(_status);

        var reset = new Button { Text = "Сбросить", Icon = EvolitIcons.Load("actions/reset.svg"), CustomMinimumSize = new Vector2(120, 44) };
        reset.Pressed += ResetSettings;
        footer.AddChild(reset);

        var apply = new Button { Text = "Применить", Icon = EvolitIcons.Load("actions/apply.svg"), CustomMinimumSize = new Vector2(130, 44) };
        apply.Pressed += ApplySettings;
        footer.AddChild(apply);

    }

    private void ShowCategory(string category)
    {
        _currentCategory = category;
        foreach (var pair in _categoryButtons)
            pair.Value.ButtonPressed = pair.Key == category;

        if (_sectionTitle is not null)
            _sectionTitle.Text = category;

        if (_content is null)
            return;

        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        switch (category)
        {
            case "Графика": BuildGraphics(); break;
            case "Звук": BuildSound(); break;
            case "Интерфейс": BuildInterface(); break;
            case "Геймплей": BuildGameplay(); break;
            case "Управление": BuildControls(); break;
        }
    }

    private void BuildGraphics()
    {
        if (_content is null) return;
        _content.AddChild(OptionRow("Режим экрана", ["Оконный", "Без рамки", "Полноэкранный"], _state.DisplayMode, value => _state.DisplayMode = value));
        _content.AddChild(OptionRow("Разрешение", ["1280×720", "1920×1080", "2560×1440"], _state.Resolution, value => _state.Resolution = value));
        _content.AddChild(ToggleRow("Вертикальная синхронизация", _state.VSync, value => _state.VSync = value));
        _content.AddChild(OptionRow("Уровень детализации", ["Низкий", "Средний", "Высокий"], _state.DetailLevel, value => _state.DetailLevel = value));
        _content.AddChild(OptionRow("Качество воды", ["Низкое", "Среднее", "Высокое"], _state.WaterQuality, value => _state.WaterQuality = value));
        _content.AddChild(OptionRow("Качество теней", ["Выкл.", "Низкое", "Среднее", "Высокое"], _state.ShadowQuality, value => _state.ShadowQuality = value));
        _content.AddChild(OptionRow("Эффекты", ["Низкие", "Средние", "Высокие"], _state.EffectsQuality, value => _state.EffectsQuality = value));
        _content.AddChild(Hint("Часть графических значений пока сохраняется как конфигурация и не меняет renderer."));
    }

    private void BuildSound()
    {
        if (_content is null) return;
        _content.AddChild(SliderRow("Общая громкость", _state.MasterVolume, 0, 100, value => _state.MasterVolume = value, "%"));
        _content.AddChild(SliderRow("Музыка", _state.MusicVolume, 0, 100, value => _state.MusicVolume = value, "%"));
        _content.AddChild(SliderRow("Эффекты", _state.EffectsVolume, 0, 100, value => _state.EffectsVolume = value, "%"));
        _content.AddChild(SliderRow("Интерфейс", _state.UiVolume, 0, 100, value => _state.UiVolume = value, "%"));
        _content.AddChild(ToggleRow("Без звука", _state.Mute, value => _state.Mute = value));
    }

    private void BuildInterface()
    {
        if (_content is null) return;
        _content.AddChild(OptionRow("Масштаб интерфейса", ["75%", "100%", "125%", "150%"], _state.UiScale, value => _state.UiScale = value));
        _content.AddChild(OptionRow("Размер текста", ["Маленький", "Обычный", "Большой"], _state.TextSize, value => _state.TextSize = value));
        _content.AddChild(ToggleRow("Подсказки", _state.Tooltips, value => _state.Tooltips = value));
        _content.AddChild(ToggleRow("Показывать FPS", _state.ShowFps, value => _state.ShowFps = value));
        _content.AddChild(ToggleRow("Показывать статистику производительности", _state.ShowPerformance, value => _state.ShowPerformance = value));
    }

    private void BuildGameplay()
    {
        if (_content is null) return;
        _content.AddChild(ToggleRow("Автопауза при открытии меню", _state.AutoPause, value => _state.AutoPause = value));
        _content.AddChild(SliderRow("Скорость камеры", _state.CameraSpeed, 1, 10, value => _state.CameraSpeed = value, "×"));
        _content.AddChild(ToggleRow("Плавное приближение камеры", _state.SmoothZoom, value => _state.SmoothZoom = value));
        _content.AddChild(Hint("Баланс, эволюция и параметры симуляции намеренно отсутствуют."));
    }

    private void BuildControls()
    {
        if (_content is null) return;
        _content.AddChild(KeyRow("Перемещение камеры", $"{InputBindings.Describe("camera_up")} / {InputBindings.Describe("camera_left")} / {InputBindings.Describe("camera_down")} / {InputBindings.Describe("camera_right")}"));
        _content.AddChild(KeyRow("Приближение", $"{InputBindings.Describe("camera_zoom_in")} / {InputBindings.Describe("camera_zoom_out")}"));
        _content.AddChild(KeyRow("Пауза", InputBindings.Describe("game_pause")));
        _content.AddChild(KeyRow("Скорость времени", $"{InputBindings.Describe("simulation_speed_1")} / {InputBindings.Describe("simulation_speed_2")} / {InputBindings.Describe("simulation_speed_3")}"));
        _content.AddChild(KeyRow("Выбор объекта", InputBindings.Describe("select")));
        _content.AddChild(KeyRow("Отмена", InputBindings.Describe("cancel")));
        _content.AddChild(Hint("Переназначение клавиш будет добавлено позже. Отображаются реальные default Input Actions."));
    }

    private Control OptionRow(string text, string[] items, int selected, Action<int> setter)
    {
        var row = Row(text);
        var option = new OptionButton { CustomMinimumSize = new Vector2(260, 42) };
        foreach (var item in items) option.AddItem(item);
        option.Select(Math.Clamp(selected, 0, items.Length - 1));
        option.ItemSelected += index => setter((int)index);
        row.AddChild(option);
        return row;
    }

    private Control ToggleRow(string text, bool value, Action<bool> setter)
    {
        var row = Row(text);
        var toggle = new CheckButton
        {
            Text = value ? "Вкл." : "Выкл.",
            ButtonPressed = value,
            CustomMinimumSize = new Vector2(118, 42)
        };
        toggle.Toggled += pressed =>
        {
            toggle.Text = pressed ? "Вкл." : "Выкл.";
            setter(pressed);
        };
        row.AddChild(toggle);
        return row;
    }

    private Control SliderRow(string text, double value, double min, double max, Action<double> setter, string suffix)
    {
        var row = Row(text);
        var controls = new HBoxContainer { CustomMinimumSize = new Vector2(360, 42) };
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(250, 0)
        };
        slider.Modulate = EvolitPalette.SoftAqua;

        var valueLabel = new Label
        {
            Text = $"{value:0}{suffix}",
            CustomMinimumSize = new Vector2(62, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        slider.ValueChanged += next =>
        {
            setter(next);
            valueLabel.Text = $"{next:0}{suffix}";
        };

        controls.AddChild(slider);
        controls.AddChild(valueLabel);
        row.AddChild(controls);
        return row;
    }

    private Control KeyRow(string action, string key)
    {
        var row = Row(action);
        var button = new Button { Text = key, CustomMinimumSize = new Vector2(260, 42) };
        button.Pressed += () => SetStatus("Переназначение клавиш пока не реализовано.");
        row.AddChild(button);
        return row;
    }

    private static HBoxContainer Row(string text)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 48),
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(360, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(label);
        return row;
    }

    private static Label Hint(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.78f));
        return label;
    }

    private void ResetSettings()
    {
        _state = AppSettings.Default();
        ShowCategory(_currentCategory);
        SetStatus("Возвращены значения по умолчанию. Нажмите «Применить», чтобы сохранить.");
    }

    private void ApplySettings()
    {
        if (_store is null)
        {
            SetStatus("SettingsStore не инициализирован.");
            return;
        }

        if (_store.Save(_state, out var error))
        {
            SetStatus("Настройки сохранены.");
            NotificationRequested?.Invoke("Настройки сохранены", ToastKind.Success);
        }
        else
        {
            SetStatus($"Ошибка: {error}");
            NotificationRequested?.Invoke("Не удалось сохранить настройки", ToastKind.Error);
        }
    }

    private void SetStatus(string text)
    {
        if (_status is not null)
            _status.Text = text;
    }
}
