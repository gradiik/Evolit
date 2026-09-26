using System;
using Evolit.Core;
using Godot;

namespace Evolit.UI;

public sealed class NewGameRequest
{
    public string WorldName { get; init; } = string.Empty;
    public string Seed { get; init; } = string.Empty;
    public string WorldSize { get; init; } = "Средний";
    public WorldLandAmount LandAmount { get; init; } = WorldLandAmount.Normal;
    public WorldClimate Climate { get; init; } = WorldClimate.Temperate;
    public GeologicalActivity Geology { get; init; } = GeologicalActivity.Normal;
}

public sealed partial class NewGameScreen : Control
{
    public event Action? BackRequested;
    public event Action<NewGameRequest>? CreateRequested;

    private LineEdit? _worldName;
    private LineEdit? _seed;
    private OptionButton? _worldSize;
    private OptionButton? _landAmount;
    private OptionButton? _climate;
    private OptionButton? _geology;
    private Label? _error;

    public override void _Ready()
    {
        var background = new MenuBackground { MotionScale = 0.45f };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(720, 0) };
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.97f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 42);
        margin.AddThemeConstantOverride("margin_right", 42);
        margin.AddThemeConstantOverride("margin_top", 34);
        margin.AddThemeConstantOverride("margin_bottom", 34);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var title = new Label { Text = "Новая игра" };
        title.AddThemeFontSizeOverride("font_size", 36);
        root.AddChild(title);

        var subtitle = new Label { Text = "Создаём сессию и детерминированную гексагональную карту по seed." };
        subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(subtitle);

        root.AddChild(new HSeparator());

        root.AddChild(FieldLabel("Название мира"));
        _worldName = new LineEdit { Text = "Мир 1", PlaceholderText = "Название мира", CustomMinimumSize = new Vector2(0, 48) };
        root.AddChild(_worldName);

        root.AddChild(FieldLabel("Seed"));
        var seedRow = new HBoxContainer();
        root.AddChild(seedRow);

        _seed = new LineEdit
        {
            Text = Random.Shared.Next(1, int.MaxValue).ToString(),
            PlaceholderText = "Число или строка seed",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 48)
        };
        seedRow.AddChild(_seed);

        var randomize = new Button { Text = "Случайный seed", CustomMinimumSize = new Vector2(175, 48) };
        randomize.Pressed += () =>
        {
            if (_seed is not null)
                _seed.Text = Random.Shared.Next(1, int.MaxValue).ToString();
        };
        seedRow.AddChild(randomize);

        root.AddChild(FieldLabel("Размер мира"));
        _worldSize = new OptionButton { CustomMinimumSize = new Vector2(0, 48) };
        _worldSize.AddItem("Маленький");
        _worldSize.AddItem("Средний");
        _worldSize.AddItem("Большой");
        _worldSize.Select(1);
        root.AddChild(_worldSize);

        var generationGrid = new GridContainer { Columns = 3 };
        generationGrid.AddThemeConstantOverride("h_separation", 12);
        root.AddChild(generationGrid);

        var landColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        landColumn.AddChild(FieldLabel("Количество суши"));
        _landAmount = new OptionButton { CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _landAmount.AddItem("Мало");
        _landAmount.AddItem("Обычно");
        _landAmount.AddItem("Много");
        _landAmount.Select(1);
        landColumn.AddChild(_landAmount);
        generationGrid.AddChild(landColumn);

        var climateColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        climateColumn.AddChild(FieldLabel("Климат"));
        _climate = new OptionButton { CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _climate.AddItem("Холодный");
        _climate.AddItem("Умеренный");
        _climate.AddItem("Тёплый");
        _climate.Select(1);
        climateColumn.AddChild(_climate);
        generationGrid.AddChild(climateColumn);

        var geologyColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        geologyColumn.AddChild(FieldLabel("Геология"));
        _geology = new OptionButton { CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _geology.AddItem("Спокойная");
        _geology.AddItem("Обычная");
        _geology.AddItem("Активная");
        _geology.Select(1);
        geologyColumn.AddChild(_geology);
        generationGrid.AddChild(geologyColumn);

        var hint = new Label
        {
            Text = "Размер определяет радиус гексагонального мира. Один и тот же seed и размер воспроизводят ту же карту.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(hint);

        _error = new Label();
        _error.AddThemeColorOverride("font_color", EvolitPalette.WarmAlert);
        root.AddChild(_error);

        root.AddChild(new HSeparator());

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        root.AddChild(buttons);

        var back = new Button { Text = "Назад", Icon = EvolitIcons.Load("actions/back.svg"), CustomMinimumSize = new Vector2(125, 48) };
        back.Pressed += () => BackRequested?.Invoke();
        buttons.AddChild(back);

        var create = new Button { Text = "Создать мир", Icon = EvolitIcons.Load("menu/new_game.svg"), CustomMinimumSize = new Vector2(175, 48) };
        create.Pressed += Create;
        buttons.AddChild(create);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("game_pause"))
            return;

        BackRequested?.Invoke();
        GetViewport().SetInputAsHandled();
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        return label;
    }

    private void Create()
    {
        if (_worldName is null || _seed is null || _worldSize is null || _landAmount is null || _climate is null || _geology is null || _error is null)
            return;

        var name = _worldName.Text.Trim();
        var seed = _seed.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            _error.Text = "Введите название мира.";
            return;
        }

        if (string.IsNullOrWhiteSpace(seed))
        {
            _error.Text = "Введите seed.";
            return;
        }

        _error.Text = string.Empty;
        CreateRequested?.Invoke(new NewGameRequest
        {
            WorldName = name,
            Seed = seed,
            WorldSize = _worldSize.GetItemText(_worldSize.Selected),
            LandAmount = (WorldLandAmount)_landAmount.Selected,
            Climate = (WorldClimate)_climate.Selected,
            Geology = (GeologicalActivity)_geology.Selected
        });
    }
}
