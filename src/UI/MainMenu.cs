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
    public event Action? VersionsRequested;
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

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", UiMetrics.Space(52));
        outer.AddThemeConstantOverride("margin_right", UiMetrics.Space(52));
        outer.AddThemeConstantOverride("margin_top", UiMetrics.Space(46));
        outer.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(46));
        AddChild(outer);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiMetrics.Space(34));
        outer.AddChild(row);

        var card = new PanelContainer
        {
            Name = "MenuCard",
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = UiMetrics.Size(420, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.88f));
        row.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(32));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(32));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(28));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(24));
        card.AddChild(margin);

        var column = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", UiMetrics.Space(7));
        margin.AddChild(column);

        var eyebrow = new Label { Text = "NATURE  ·  TIME  ·  LIFE" };
        eyebrow.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        eyebrow.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.EvolutionCyan, 0.82f));
        column.AddChild(eyebrow);

        var title = new Label { Text = "Evolit" };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(52));
        column.AddChild(title);

        var subtitle = new Label { Text = "Эволюция продолжается" };
        subtitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(15));
        subtitle.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.FogBlue, 0.92f));
        column.AddChild(subtitle);

        column.AddChild(new Control
        {
            CustomMinimumSize = UiMetrics.Size(0, 12)
        });

        var continueButton = MenuButton(
            "Продолжить",
            "menu/continue.svg");
        continueButton.Disabled = !_continueAvailable;
        continueButton.TooltipText = _continueAvailable
            ? "Загрузить последнее сохранение"
            : "Сохранений пока нет";
        continueButton.Pressed += () => ContinueRequested?.Invoke();
        NatureTechTheme.MarkPrimary(continueButton);
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

        var encyclopedia = MenuButton(
            "Энциклопедия",
            "menu/encyclopedia.svg");
        encyclopedia.Pressed += () => EncyclopediaRequested?.Invoke();
        column.AddChild(encyclopedia);

        var versions = MenuButton("Версии", "simulation/history.svg");
        versions.Pressed += () => VersionsRequested?.Invoke();
        column.AddChild(versions);

        column.AddChild(new Control
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        });

        var exit = MenuButton("Выход", "menu/exit.svg");
        exit.Pressed += () => ExitRequested?.Invoke();
        NatureTechTheme.MarkDanger(exit);
        column.AddChild(exit);

        var status = new Label
        {
            Text = $"Evolit {Evolit.Versioning.AppVersionCatalog.CurrentVersion} · разработка",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        status.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        status.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.FogBlue, 0.92f));
        column.AddChild(status);

        row.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        BuildRightMessage(row);
    }

    private void BuildRightMessage(Container parent)
    {
        var note = new VBoxContainer
        {
            CustomMinimumSize = UiMetrics.Size(330, 0),
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        note.AddThemeConstantOverride("separation", UiMetrics.Space(9));
        parent.AddChild(note);

        var accent = new ColorRect
        {
            Color = new Color(EvolitPalette.EvolutionCyan, 0.36f),
            CustomMinimumSize = UiMetrics.Size(58, 3),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        note.AddChild(accent);

        var noteTitle = new Label
        {
            Text = "МАЛЫЕ ФОРМЫ.
БОЛЬШИЕ МИРЫ.",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        noteTitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(24));
        noteTitle.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.MistWhite, 0.88f));
        note.AddChild(noteTitle);

        var description = new Label
        {
            Text = "Наблюдай за жизнью,
меняющейся со временем.",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        description.AddThemeFontSizeOverride("font_size", UiMetrics.Font(14));
        description.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.FogBlue, 0.80f));
        note.AddChild(description);

        var micro = new Label
        {
            Text = "ЖИВАЯ СИСТЕМА  /  EVOLIT",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        micro.AddThemeFontSizeOverride("font_size", UiMetrics.Font(9));
        micro.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.EvolutionCyan, 0.55f));
        note.AddChild(micro);
    }

    private static Button MenuButton(string text, string iconPath)
    {
        var button = new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(iconPath),
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = UiMetrics.Size(0, 45),
            FocusMode = FocusModeEnum.All
        };
        UiMotion.BindButton(button);
        return button;
    }
}
