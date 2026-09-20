using System;
using Godot;

namespace Evolit.UI;

public sealed class NewGameRequest
{
    public string WorldName { get; init; } = string.Empty;
    public string Seed { get; init; } = string.Empty;
    public string WorldSize { get; init; } = "Средний";
}

public sealed partial class NewGameScreen : Control
{
    public event Action? BackRequested;
    public event Action<NewGameRequest>? CreateRequested;

    private LineEdit? _worldName;
    private LineEdit? _seed;
    private OptionButton? _worldSize;
    private Label? _error;

    public override void _Ready()
    {
        var background = new MenuBackground();
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.96f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 36);
        margin.AddThemeConstantOverride("margin_right", 36);
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        card.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var title = new Label { Text = "Новая игра" };
        title.AddThemeFontSizeOverride("font_size", 34);
        root.AddChild(title);

        var subtitle = new Label { Text = "Пока создаётся только сессия и метаданные будущего мира." };
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(subtitle);

        root.AddChild(new HSeparator());

        root.AddChild(FieldLabel("Название мира"));
        _worldName = new LineEdit { Text = "Мир 1", PlaceholderText = "Название мира", CustomMinimumSize = new Vector2(0, 44) };
        root.AddChild(_worldName);

        root.AddChild(FieldLabel("Seed"));
        var seedRow = new HBoxContainer();
        root.AddChild(seedRow);

        _seed = new LineEdit
        {
            Text = Random.Shared.Next(1, int.MaxValue).ToString(),
            PlaceholderText = "Число или строка seed",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 44)
        };
        seedRow.AddChild(_seed);

        var randomize = new Button { Text = "Случайный seed", CustomMinimumSize = new Vector2(160, 44) };
        randomize.Pressed += () =>
        {
            if (_seed is not null)
                _seed.Text = Random.Shared.Next(1, int.MaxValue).ToString();
        };
        seedRow.AddChild(randomize);

        root.AddChild(FieldLabel("Размер мира"));
        _worldSize = new OptionButton { CustomMinimumSize = new Vector2(0, 44) };
        _worldSize.AddItem("Маленький");
        _worldSize.AddItem("Средний");
        _worldSize.AddItem("Большой");
        _worldSize.Select(1);
        root.AddChild(_worldSize);

        var hint = new Label
        {
            Text = "Размер пока сохраняется как metadata и не запускает генерацию карты.",
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

        var back = new Button { Text = "Назад", Icon = EvolitIcons.Load("actions/back.svg"), CustomMinimumSize = new Vector2(120, 46) };
        back.Pressed += () => BackRequested?.Invoke();
        buttons.AddChild(back);

        var create = new Button { Text = "Создать мир", Icon = EvolitIcons.Load("menu/new_game.svg"), CustomMinimumSize = new Vector2(160, 46) };
        create.Pressed += Create;
        buttons.AddChild(create);
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        return label;
    }

    private void Create()
    {
        if (_worldName is null || _seed is null || _worldSize is null || _error is null)
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
            WorldSize = _worldSize.GetItemText(_worldSize.Selected)
        });
    }
}
