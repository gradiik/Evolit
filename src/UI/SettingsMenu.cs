using System;
using System.Collections.Generic;
using Godot;

namespace Evolit.UI;

public sealed partial class SettingsMenu : Control
{
    public event Action? BackRequested;

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

    private VBoxContainer? _content;
    private Label? _sectionTitle;
    private Label? _status;
    private string _currentCategory = "Графика";
    private SettingsState _state = SettingsState.Default();

    public override void _Ready()
    {
        Build();
        ShowCategory(_currentCategory);
    }

    public void RefreshCurrentCategory()
    {
        if (_content is null) return;
        ShowCategory(_currentCategory);
    }

    private void Build()
    {
        var dim = new ColorRect
        {
            Color = new Color(0.012f, 0.052f, 0.064f, 0.52f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 64);
        outer.AddThemeConstantOverride("margin_right", 64);
        outer.AddThemeConstantOverride("margin_top", 54);
        outer.AddThemeConstantOverride("margin_bottom", 54);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.95f));
        outer.AddChild(card);

        var cardMargin = new MarginContainer();
        cardMargin.AddThemeConstantOverride("margin_left", 32);
        cardMargin.AddThemeConstantOverride("margin_right", 32);
        cardMargin.AddThemeConstantOverride("margin_top", 26);
        cardMargin.AddThemeConstantOverride("margin_bottom", 24);
        card.AddChild(cardMargin);

        var root = new VBoxContainer();
        cardMargin.AddChild(root);

        var headingRow = new HBoxContainer();
        root.AddChild(headingRow);

        var titleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingRow.AddChild(titleBox);

        var title = new Label { Text = "Настройки" };
        title.AddThemeFontSizeOverride("font_size", 34);
        titleBox.AddChild(title);

        var subtitle = new Label { Text = "Базовый интерфейс · значения пока не сохраняются на диск" };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titleBox.AddChild(subtitle);

        var close = new Button
        {
            Text = "Назад",
            CustomMinimumSize = new Vector2(118f, 44f)
        };
        SetIcon(close, "actions/back.svg");
        close.Pressed += () => BackRequested?.Invoke();
        headingRow.AddChild(close);

        root.AddChild(new HSeparator());

        var body = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(body);

        var navigationPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(220f, 0f)
        };
        navigationPanel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(navigationPanel);

        var navMargin = new MarginContainer();
        navMargin.AddThemeConstantOverride("margin_left", 12);
        navMargin.AddThemeConstantOverride("margin_right", 12);
        navMargin.AddThemeConstantOverride("margin_top", 12);
        navMargin.AddThemeConstantOverride("margin_bottom", 12);
        navigationPanel.AddChild(navMargin);

        var nav = new VBoxContainer();
        navMargin.AddChild(nav);

        foreach (var category in _categories)
        {
            var button = new Button
            {
                Text = category,
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0f, 46f)
            };

            SetIcon(button, CategoryIcons[category]);
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

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentRoot.AddChild(scroll);

        _content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", 13);
        scroll.AddChild(_content);

        root.AddChild(new HSeparator());

        var footer = new HBoxContainer();
        root.AddChild(footer);

        _status = new Label
        {
            Text = "Изменения применяются только к текущему UI-сеансу.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _status.AddThemeFontSizeOverride("font_size", 13);
        _status.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        footer.AddChild(_status);

        var reset = new Button { Text = "Сбросить", CustomMinimumSize = new Vector2(120f, 44f) };
        SetIcon(reset, "actions/reset.svg");
        reset.Pressed += ResetSettings;
        footer.AddChild(reset);

        var apply = new Button { Text = "Применить", CustomMinimumSize = new Vector2(130f, 44f) };
        SetIcon(apply, "actions/apply.svg");
        apply.Pressed += ApplySettings;
        footer.AddChild(apply);

        var back = new Button { Text = "Назад", CustomMinimumSize = new Vector2(110f, 44f) };
        SetIcon(back, "actions/back.svg");
        back.Pressed += () => BackRequested?.Invoke();
        footer.AddChild(back);
    }

    private static void SetIcon(Button button, string relativePath, int size = 20)
    {
        button.Icon = EvolitIcons.Load(relativePath);
    }

    private void ShowCategory(string category)
    {
        _currentCategory = category;

        foreach (var pair in _categoryButtons)
            pair.Value.ButtonPressed = pair.Key == category;

        if (_sectionTitle is not null)
            _sectionTitle.Text = category;

        if (_content is null) return;

        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

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
        _content.AddChild(Hint("Графические параметры пока служат UI-болванкой и не меняют renderer."));
    }

    private void BuildSound()
    {
        if (_content is null) return;

        _content.AddChild(SliderRow("Общая громкость", _state.MasterVolume, 0, 100, value => _state.MasterVolume = value, "%"));
        _content.AddChild(SliderRow("Музыка", _state.MusicVolume, 0, 100, value => _state.MusicVolume = value, "%"));
        _content.AddChild(SliderRow("Эффекты", _state.EffectsVolume, 0, 100, value => _state.EffectsVolume = value, "%"));
        _content.AddChild(SliderRow("Интерфейс", _state.UiVolume, 0, 100, value => _state.UiVolume = value, "%"));
        _content.AddChild(ToggleRow("Без звука", _state.Mute, value => _state.Mute = value));
        _content.AddChild(Hint("Полноценная аудиосистема намеренно не создаётся на этом этапе."));
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
        _content.AddChild(Hint("Баланс, эволюция, мутации и скорость симуляции здесь специально отсутствуют."));
    }

    private void BuildControls()
    {
        if (_content is null) return;

        _content.AddChild(KeyRow("Перемещение камеры", "WASD / стрелки"));
        _content.AddChild(KeyRow("Приближение", "Колесо мыши"));
        _content.AddChild(KeyRow("Пауза", "Space"));
        _content.AddChild(KeyRow("Ускорение времени", "1 / 2 / 3"));
        _content.AddChild(KeyRow("Выбор объекта", "ЛКМ"));
        _content.AddChild(KeyRow("Отмена", "Esc"));
        _content.AddChild(Hint("Переназначение клавиш будет добавлено позже."));
    }

    private Control OptionRow(string labelText, string[] items, int selected, Action<int> setter)
    {
        var row = Row(labelText);
        var option = new OptionButton
        {
            CustomMinimumSize = new Vector2(230f, 42f)
        };

        foreach (var item in items) option.AddItem(item);
        option.Select(Math.Clamp(selected, 0, items.Length - 1));
        option.ItemSelected += index => setter((int)index);

        row.AddChild(option);
        return row;
    }

    private Control ToggleRow(string labelText, bool value, Action<bool> setter)
    {
        var row = Row(labelText);
        var toggle = new CheckButton
        {
            Text = value ? "Вкл." : "Выкл.",
            ButtonPressed = value,
            CustomMinimumSize = new Vector2(118f, 42f)
        };

        toggle.Toggled += pressed =>
        {
            toggle.Text = pressed ? "Вкл." : "Выкл.";
            setter(pressed);
        };

        row.AddChild(toggle);
        return row;
    }

    private Control SliderRow(string labelText, double value, double min, double max, Action<double> setter, string suffix)
    {
        var row = Row(labelText);

        var controls = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(330f, 42f)
        };

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220f, 0f)
        };
        slider.Modulate = EvolitPalette.SoftAqua;

        var valueLabel = new Label
        {
            Text = $"{value:0}{suffix}",
            CustomMinimumSize = new Vector2(62f, 0f),
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
        var button = new Button
        {
            Text = key,
            CustomMinimumSize = new Vector2(230f, 42f)
        };
        button.Pressed += () => SetStatus("Переназначение клавиш будет добавлено позже.");
        row.AddChild(button);
        return row;
    }

    private HBoxContainer Row(string labelText)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 48f)
        };

        var label = new Label
        {
            Text = labelText,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        row.AddChild(label);

        return row;
    }

    private Label Hint(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.78f));
        return label;
    }

    private void ResetSettings()
    {
        _state = SettingsState.Default();
        ShowCategory(_currentCategory);
        SetStatus("Настройки сброшены к значениям по умолчанию.");
    }

    private void ApplySettings()
    {
        SetStatus("Настройки применены к текущему UI-сеансу.");
    }

    private void SetStatus(string text)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.Modulate = Colors.White;
    }

    private sealed class SettingsState
    {
        public int DisplayMode;
        public int Resolution;
        public bool VSync;
        public int DetailLevel;
        public int WaterQuality;
        public int ShadowQuality;
        public int EffectsQuality;

        public double MasterVolume;
        public double MusicVolume;
        public double EffectsVolume;
        public double UiVolume;
        public bool Mute;

        public int UiScale;
        public int TextSize;
        public bool Tooltips;
        public bool ShowFps;
        public bool ShowPerformance;

        public bool AutoPause;
        public double CameraSpeed;
        public bool SmoothZoom;

        public static SettingsState Default()
        {
            return new SettingsState
            {
                DisplayMode = 0,
                Resolution = 1,
                VSync = true,
                DetailLevel = 1,
                WaterQuality = 1,
                ShadowQuality = 2,
                EffectsQuality = 1,

                MasterVolume = 80,
                MusicVolume = 65,
                EffectsVolume = 80,
                UiVolume = 75,
                Mute = false,

                UiScale = 1,
                TextSize = 1,
                Tooltips = true,
                ShowFps = false,
                ShowPerformance = false,

                AutoPause = true,
                CameraSpeed = 5,
                SmoothZoom = true
            };
        }
    }
}
