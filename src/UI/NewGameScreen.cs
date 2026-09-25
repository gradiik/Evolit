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
        var background = new MenuBackground { MotionScale = 0.45f };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", UiMetrics.Space(58));
        outer.AddThemeConstantOverride("margin_right", UiMetrics.Space(58));
        outer.AddThemeConstantOverride("margin_top", UiMetrics.Space(42));
        outer.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(42));
        AddChild(outer);

        var center = new CenterContainer();
        outer.AddChild(center);

        var card = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(690, 0)
        };
        card.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.97f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(34));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(34));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(28));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(28));
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", UiMetrics.Space(10));
        margin.AddChild(root);

        var eyebrow = new Label { Text = "WORLD  ·  GENERATION" };
        eyebrow.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        eyebrow.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.EvolutionCyan, 0.76f));
        root.AddChild(eyebrow);

        var title = new Label { Text = "Новый мир" };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(34));
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = "Название, seed и размер определяют новую детерминированную карту.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(subtitle);

        root.AddChild(new HSeparator());

        root.AddChild(FieldLabel("Название мира"));
        _worldName = new LineEdit
        {
            Text = "Мир 1",
            PlaceholderText = "Название мира",
            CustomMinimumSize = UiMetrics.Size(0, 44)
        };
        root.AddChild(_worldName);

        root.AddChild(FieldLabel("Seed"));
        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", UiMetrics.Space(7));
        root.AddChild(seedRow);

        _seed = new LineEdit
        {
            Text = Random.Shared.Next(1, int.MaxValue).ToString(),
            PlaceholderText = "Число или строка seed",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = UiMetrics.Size(0, 44)
        };
        seedRow.AddChild(_seed);

        var randomize = new Button
        {
            Text = "Новый seed",
            TooltipText = "Сгенерировать другое значение seed",
            CustomMinimumSize = UiMetrics.Size(132, 44)
        };
        randomize.Pressed += RandomizeSeed;
        UiMotion.BindButton(randomize);
        seedRow.AddChild(randomize);

        root.AddChild(FieldLabel("Размер мира"));
        _worldSize = new OptionButton
        {
            CustomMinimumSize = UiMetrics.Size(0, 44)
        };
        _worldSize.AddItem("Маленький");
        _worldSize.AddItem("Средний");
        _worldSize.AddItem("Большой");
        _worldSize.Select(1);
        root.AddChild(_worldSize);

        var hintPanel = new PanelContainer();
        hintPanel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.SubtleSectionStyle());
        root.AddChild(hintPanel);

        var hintMargin = new MarginContainer();
        hintMargin.AddThemeConstantOverride("margin_left", UiMetrics.Space(12));
        hintMargin.AddThemeConstantOverride("margin_right", UiMetrics.Space(12));
        hintMargin.AddThemeConstantOverride("margin_top", UiMetrics.Space(9));
        hintMargin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(9));
        hintPanel.AddChild(hintMargin);

        var hint = new Label
        {
            Text = "Одинаковые seed и размер воспроизводят ту же карту. Размер меняет радиус мира, но не набор игровых правил.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        hint.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        hintMargin.AddChild(hint);

        _error = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _error.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        _error.AddThemeColorOverride("font_color", EvolitPalette.WarmAlert);
        root.AddChild(_error);

        root.AddChild(new HSeparator());

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        buttons.AddThemeConstantOverride("separation", UiMetrics.Space(7));
        root.AddChild(buttons);

        var back = new Button
        {
            Text = "Назад",
            Icon = EvolitIcons.Load("actions/back.svg"),
            CustomMinimumSize = UiMetrics.Size(116, 42)
        };
        back.Pressed += () => BackRequested?.Invoke();
        UiMotion.BindButton(back);
        buttons.AddChild(back);

        var create = new Button
        {
            Text = "Создать мир",
            Icon = EvolitIcons.Load("menu/new_game.svg"),
            CustomMinimumSize = UiMetrics.Size(166, 42)
        };
        create.Pressed += Create;
        NatureTechTheme.MarkPrimary(create);
        UiMotion.BindButton(create);
        buttons.AddChild(create);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("cancel"))
        {
            BackRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    private void RandomizeSeed()
    {
        if (_seed is null)
            return;

        _seed.Text = Random.Shared.Next(1, int.MaxValue).ToString();
        _seed.CaretColumn = _seed.Text.Length;
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(13));
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        return label;
    }

    private void Create()
    {
        if (_worldName is null
            || _seed is null
            || _worldSize is null
            || _error is null)
        {
            return;
        }

        var name = _worldName.Text.Trim();
        var seed = _seed.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            _error.Text = "Введите название мира.";
            UiMotion.FadeIn(_error);
            return;
        }

        if (string.IsNullOrWhiteSpace(seed))
        {
            _error.Text = "Введите seed.";
            UiMotion.FadeIn(_error);
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
