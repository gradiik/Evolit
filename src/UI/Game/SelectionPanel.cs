using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class SelectionPanel : PanelContainer
{
    private DemoEntity? _entity;
    private Label? _title;
    private Label? _kind;
    private VBoxContainer? _details;
    private PopulationGraph? _graph;

    public override void _Ready()
    {
        Visible = false;
        AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.96f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        _kind = new Label { Text = "ОБЪЕКТ", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _kind.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        _kind.AddThemeColorOverride("font_color", EvolitPalette.EvolutionCyan);
        header.AddChild(_kind);

        var liveBadge = new Label { Text = "LIVE" };
        liveBadge.AddThemeFontSizeOverride("font_size", UiMetrics.Font(9));
        liveBadge.AddThemeColorOverride("font_color", EvolitPalette.YoungLeaf);
        header.AddChild(liveBadge);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(21));
        root.AddChild(_title);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        content.AddChild(SectionTitle("Основное"));

        _details = new VBoxContainer();
        _details.AddThemeConstantOverride("separation", 5);
        content.AddChild(_details);

        content.AddChild(SectionTitle("Популяция"));

        _graph = new PopulationGraph { CustomMinimumSize = UiMetrics.Size(0, 112) };
        content.AddChild(_graph);

        content.AddChild(SectionTitle("Генетика"));

        var genetics = new PanelContainer();
        genetics.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        content.AddChild(genetics);

        var geneticsMargin = new MarginContainer();
        geneticsMargin.AddThemeConstantOverride("margin_left", 10);
        geneticsMargin.AddThemeConstantOverride("margin_right", 10);
        geneticsMargin.AddThemeConstantOverride("margin_top", 8);
        geneticsMargin.AddThemeConstantOverride("margin_bottom", 8);
        genetics.AddChild(geneticsMargin);

        var geneticsText = new Label
        {
            Text = "Геном появится после подключения настоящей симуляции. Сейчас отображается только связанный demo-вид.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        geneticsText.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        geneticsText.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        geneticsMargin.AddChild(geneticsText);
    }

    public void SetEntity(DemoEntity? entity)
    {
        _entity = entity;

        if (entity is null)
        {
            if (!Visible)
                return;

            var tween = CreateTween();
            tween.TweenProperty(this, "modulate", new Color(1, 1, 1, 0), 0.12);
            tween.TweenCallback(Callable.From(() =>
            {
                Visible = false;
                Modulate = Colors.White;
            }));
            return;
        }

        RefreshCurrent();

        if (Visible)
            return;

        Visible = true;
        Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.16);
    }

    public void RefreshCurrent()
    {
        if (_entity is null || _title is null || _kind is null || _details is null || _graph is null)
            return;

        var entity = _entity;
        _title.Text = entity.Name;
        _kind.Text = entity.Kind == DemoEntityKind.Creature ? "СУЩЕСТВО" : "РАСТЕНИЕ";
        _kind.AddThemeColorOverride("font_color", entity.Kind == DemoEntityKind.Creature ? EvolitPalette.SoftAqua : EvolitPalette.YoungLeaf);

        foreach (var child in _details.GetChildren())
        {
            _details.RemoveChild(child);
            child.QueueFree();
        }

        AddBadgeRow(entity);
        AddDetail("ID", entity.Id);
        AddDetail("Вид", entity.Species);
        AddDetail("Подвид", entity.Subspecies);
        AddDetail("Возраст", $"{entity.AgeDays} дн.");
        AddDetail("Размер", $"{entity.Size:0.0}");

        if (entity.Kind == DemoEntityKind.Creature)
        {
            AddProgress("Здоровье", entity.Health, EvolitPalette.YoungLeaf);
            AddProgress("Энергия", entity.Energy, EvolitPalette.WarmSand);
            AddDetail("Скорость", $"{entity.Speed:0.0}");
            AddDetail("Питание", entity.Diet);
        }
        else
        {
            AddProgress("Состояние", entity.Health, EvolitPalette.YoungLeaf);
            AddDetail("Тип растения", entity.PlantType);
            AddDetail("Статус", entity.State);
        }

        AddDetail("Популяция", entity.Population.ToString());
        _graph.SetValues(entity.PopulationHistory);
    }

    private void AddBadgeRow(DemoEntity entity)
    {
        if (_details is null)
            return;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        row.AddChild(Badge(entity.Kind == DemoEntityKind.Creature ? "ПОДВИЖНАЯ ФОРМА" : "РАСТИТЕЛЬНАЯ ФОРМА",
            entity.Kind == DemoEntityKind.Creature ? EvolitPalette.SoftAqua : EvolitPalette.YoungLeaf));
        row.AddChild(Badge("DEMO", EvolitPalette.EvolutionCyan));
        _details.AddChild(row);
    }

    private static Control Badge(string text, Color color)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(color, 0.10f),
            BorderColor = new Color(color, 0.42f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        });

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 7);
        margin.AddThemeConstantOverride("margin_right", 7);
        margin.AddThemeConstantOverride("margin_top", 3);
        margin.AddThemeConstantOverride("margin_bottom", 3);
        panel.AddChild(margin);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(9));
        label.AddThemeColorOverride("font_color", color);
        margin.AddChild(label);
        return panel;
    }

    private void AddDetail(string name, string value)
    {
        if (_details is null)
            return;

        var row = new HBoxContainer { CustomMinimumSize = UiMetrics.Size(0, 27) };
        var label = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(label);

        var data = new Label { Text = value };
        data.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        row.AddChild(data);
        _details.AddChild(row);
    }

    private void AddProgress(string name, float value, Color accent)
    {
        if (_details is null)
            return;

        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 3);

        var row = new HBoxContainer();
        block.AddChild(row);

        var label = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(label);

        var percent = new Label { Text = $"{value * 100f:0}%" };
        percent.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        percent.AddThemeColorOverride("font_color", accent);
        row.AddChild(percent);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = value * 100f,
            ShowPercentage = false,
            CustomMinimumSize = UiMetrics.Size(0, 7)
        };
        block.AddChild(bar);
        _details.AddChild(block);
    }

    private static Label SectionTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", UiMetrics.Font(13));
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        return label;
    }
}
