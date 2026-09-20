using Godot;

namespace Evolit.UI;

public sealed partial class MainMenu : Control
{
    private Control? _menuLayer;
    private SettingsMenu? _settingsMenu;
    private Label? _statusLabel;

    public override void _Ready()
    {
        Theme = NatureTechTheme.Create();

        var background = new MenuBackground
        {
            Name = "MenuBackground"
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        MoveChild(background, 0);

        BuildMainMenu();

        _settingsMenu = new SettingsMenu
        {
            Name = "SettingsMenu",
            Visible = false
        };
        _settingsMenu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _settingsMenu.BackRequested += ShowMainMenu;
        AddChild(_settingsMenu);

        Modulate = new Color(1f, 1f, 1f, 0f);
        CreateTween()
            .TweenProperty(this, "modulate", Colors.White, 0.42)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void BuildMainMenu()
    {
        _menuLayer = new Control
        {
            Name = "MainMenuLayer"
        };
        _menuLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_menuLayer);

        var card = new PanelContainer
        {
            Name = "MenuCard",
            MouseFilter = MouseFilterEnum.Stop
        };
        card.AnchorLeft = 0.055f;
        card.AnchorRight = 0.365f;
        card.AnchorTop = 0.095f;
        card.AnchorBottom = 0.905f;
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.87f));
        _menuLayer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 38);
        margin.AddThemeConstantOverride("margin_bottom", 32);
        card.AddChild(margin);

        var column = new VBoxContainer();
        column.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(column);

        var eyebrow = new Label
        {
            Text = "NATURE  ·  TIME  ·  LIFE"
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 12);
        eyebrow.AddThemeColorOverride("font_color", new Color(EvolitPalette.EvolutionCyan, 0.72f));
        column.AddChild(eyebrow);

        var title = new Label
        {
            Text = "Evolit"
        };
        title.AddThemeFontSizeOverride("font_size", 56);
        title.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        column.AddChild(title);

        var subtitle = new Label
        {
            Text = "Эволюция продолжается"
        };
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        column.AddChild(subtitle);

        var spacer = new Control { CustomMinimumSize = new Vector2(0f, 26f) };
        column.AddChild(spacer);

        var continueButton = MenuButton("▶   Продолжить");
        continueButton.Disabled = true;
        continueButton.TooltipText = "Сохранений пока нет";
        column.AddChild(continueButton);

        var newGame = MenuButton("✦   Новая игра");
        newGame.Pressed += () => ShowStatus("Создание мира будет добавлено на следующем этапе.");
        column.AddChild(newGame);

        var settings = MenuButton("⚙   Настройки");
        settings.Pressed += ShowSettings;
        column.AddChild(settings);

        var encyclopedia = MenuButton("▤   Энциклопедия");
        encyclopedia.Pressed += () => ShowStatus("Энциклопедия появится позже.");
        column.AddChild(encyclopedia);

        var exit = MenuButton("⏻   Выход");
        exit.Pressed += () => GetTree().Quit();
        column.AddChild(exit);

        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        _statusLabel = new Label
        {
            Text = "Первый визуальный каркас · без симуляции",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _statusLabel.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.78f));
        column.AddChild(_statusLabel);

        var rightNote = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        rightNote.AnchorLeft = 0.68f;
        rightNote.AnchorRight = 0.94f;
        rightNote.AnchorTop = 0.17f;
        rightNote.AnchorBottom = 0.42f;
        _menuLayer.AddChild(rightNote);

        var noteTitle = new Label
        {
            Text = "МАЛЫЕ ФОРМЫ.\nБОЛЬШИЕ МИРЫ."
        };
        noteTitle.HorizontalAlignment = HorizontalAlignment.Right;
        noteTitle.AddThemeFontSizeOverride("font_size", 24);
        noteTitle.AddThemeColorOverride("font_color", new Color(EvolitPalette.MistWhite, 0.70f));
        rightNote.AddChild(noteTitle);

        var note = new Label
        {
            Text = "Спокойный живой мир,\nкоторый будет развиваться шаг за шагом."
        };
        note.HorizontalAlignment = HorizontalAlignment.Right;
        note.AddThemeFontSizeOverride("font_size", 14);
        note.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.62f));
        rightNote.AddChild(note);
    }

    private Button MenuButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0f, 56f),
            FocusMode = FocusModeEnum.All
        };

        button.MouseEntered += () =>
        {
            if (button.Disabled) return;
            CreateTween()
                .TweenProperty(button, "modulate", new Color(1f, 1f, 1f, 1f), 0.10)
                .SetEase(Tween.EaseType.Out);
        };

        button.MouseExited += () =>
        {
            if (button.Disabled) return;
            CreateTween()
                .TweenProperty(button, "modulate", new Color(0.96f, 0.98f, 0.98f, 1f), 0.14)
                .SetEase(Tween.EaseType.Out);
        };

        return button;
    }

    private void ShowSettings()
    {
        if (_menuLayer is null || _settingsMenu is null) return;
        _menuLayer.Visible = false;
        _settingsMenu.Visible = true;
        _settingsMenu.RefreshCurrentCategory();
    }

    private void ShowMainMenu()
    {
        if (_menuLayer is null || _settingsMenu is null) return;
        _settingsMenu.Visible = false;
        _menuLayer.Visible = true;
        ShowStatus("Настройки сохранены только для текущего сеанса.");
    }

    private void ShowStatus(string text)
    {
        if (_statusLabel is null) return;
        _statusLabel.Text = text;
        _statusLabel.Modulate = Colors.White;

        var tween = CreateTween();
        tween.TweenInterval(2.6);
        tween.TweenProperty(_statusLabel, "modulate", new Color(1f, 1f, 1f, 0.54f), 0.35);
    }
}
