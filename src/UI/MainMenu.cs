using System;
using Godot;

namespace Evolit.UI;

public sealed partial class MainMenu : Control
{
    public event Action? ContinueRequested;
    public event Action? NewGameRequested;
    public event Action? SavesRequested;
    public event Action? SettingsRequested;
    public event Action? EncyclopediaRequested;
    public event Action? ExitRequested;

    private bool _continueAvailable;

    public void Configure(bool continueAvailable)
    {
        _continueAvailable = continueAvailable;
    }

    public override void _Ready()
    {
        var background = new MenuBackground { Name = "MenuBackground" };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var card = new PanelContainer
        {
            Name = "MenuCard",
            MouseFilter = MouseFilterEnum.Stop
        };
        card.AnchorLeft = 0.055f;
        card.AnchorRight = 0.365f;
        card.AnchorTop = 0.075f;
        card.AnchorBottom = 0.925f;
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.88f));
        AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 34);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        card.AddChild(margin);

        var column = new VBoxContainer();
        column.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(column);

        var eyebrow = new Label { Text = "NATURE  ·  TIME  ·  LIFE" };
        eyebrow.AddThemeFontSizeOverride("font_size", 12);
        eyebrow.AddThemeColorOverride("font_color", new Color(EvolitPalette.EvolutionCyan, 0.82f));
        column.AddChild(eyebrow);

        var title = new Label { Text = "Evolit" };
        title.AddThemeFontSizeOverride("font_size", 56);
        column.AddChild(title);

        var subtitle = new Label { Text = "Эволюция продолжается" };
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.92f));
        column.AddChild(subtitle);

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });

        var continueButton = MenuButton("Продолжить", "menu/continue.svg");
        continueButton.Disabled = !_continueAvailable;
        continueButton.TooltipText = _continueAvailable ? "Загрузить последнее сохранение" : "Сохранений пока нет";
        continueButton.Pressed += () => ContinueRequested?.Invoke();
        column.AddChild(continueButton);

        var newGame = MenuButton("Новая игра", "menu/new_game.svg");
        newGame.Pressed += () => NewGameRequested?.Invoke();
        column.AddChild(newGame);

        var saves = MenuButton("Сохранения", "menu/saves.svg");
        saves.Pressed += () => SavesRequested?.Invoke();
        column.AddChild(saves);

        var settings = MenuButton("Настройки", "menu/settings.svg");
        settings.Pressed += () => SettingsRequested?.Invoke();
        column.AddChild(settings);

        var encyclopedia = MenuButton("Энциклопедия", "menu/encyclopedia.svg");
        encyclopedia.Pressed += () => EncyclopediaRequested?.Invoke();
        column.AddChild(encyclopedia);

        var exit = MenuButton("Выход", "menu/exit.svg");
        exit.Pressed += () => ExitRequested?.Invoke();
        column.AddChild(exit);

        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        var status = new Label
        {
            Text = "Системный каркас · мир пока не симулируется",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        status.AddThemeFontSizeOverride("font_size", 13);
        status.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.90f));
        column.AddChild(status);

        BuildRightMessage();

        Modulate = new Color(1, 1, 1, 0);
        CreateTween()
            .TweenProperty(this, "modulate", Colors.White, 0.35)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void BuildRightMessage()
    {
        var accent = new ColorRect
        {
            Color = new Color(EvolitPalette.EvolutionCyan, 0.42f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        accent.AnchorLeft = 0.805f;
        accent.AnchorRight = 0.808f;
        accent.AnchorTop = 0.18f;
        accent.AnchorBottom = 0.35f;
        AddChild(accent);

        var rightNote = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rightNote.AnchorLeft = 0.63f;
        rightNote.AnchorRight = 0.79f;
        rightNote.AnchorTop = 0.17f;
        rightNote.AnchorBottom = 0.39f;
        rightNote.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(rightNote);

        var noteTitle = new Label
        {
            Text = "МАЛЫЕ ФОРМЫ.\nБОЛЬШИЕ МИРЫ.",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        noteTitle.AddThemeFontSizeOverride("font_size", 25);
        noteTitle.AddThemeColorOverride("font_color", new Color(EvolitPalette.MistWhite, 0.86f));
        rightNote.AddChild(noteTitle);

        var note = new Label
        {
            Text = "Наблюдай за жизнью,\nменяющейся со временем.",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        note.AddThemeFontSizeOverride("font_size", 14);
        note.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.80f));
        rightNote.AddChild(note);

        var micro = new Label
        {
            Text = "ЖИВАЯ СИСТЕМА  /  EVOLIT",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        micro.AddThemeFontSizeOverride("font_size", 10);
        micro.AddThemeColorOverride("font_color", new Color(EvolitPalette.EvolutionCyan, 0.55f));
        rightNote.AddChild(micro);
    }

    private static Button MenuButton(string text, string iconPath)
    {
        return new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(iconPath),
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 50),
            FocusMode = FocusModeEnum.All
        };
    }
}
