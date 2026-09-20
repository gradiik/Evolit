using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class SelectionPanel : PanelContainer
{
    private Label? _title;
    private Label? _kind;
    private VBoxContainer? _details;
    private PopulationGraph? _graph;
    private Label? _genetics;

    public override void _Ready()
    {
        Visible = false;
        AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.94f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        _kind = new Label { Text = "ОБЪЕКТ" };
        _kind.AddThemeFontSizeOverride("font_size", 11);
        _kind.AddThemeColorOverride("font_color", EvolitPalette.EvolutionCyan);
        root.AddChild(_kind);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(_title);

        root.AddChild(new HSeparator());

        _details = new VBoxContainer();
        _details.AddThemeConstantOverride("separation", 7);
        root.AddChild(_details);

        var graphTitle = new Label { Text = "Популяция" };
        graphTitle.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        root.AddChild(graphTitle);

        _graph = new PopulationGraph();
        root.AddChild(_graph);

        var geneticsTitle = new Label { Text = "Генетика" };
        geneticsTitle.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        root.AddChild(geneticsTitle);

        _genetics = new Label
        {
            Text = "Генетические данные появятся после подключения симуляции.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _genetics.AddThemeFontSizeOverride("font_size", 12);
        _genetics.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(_genetics);
    }

    public void SetEntity(DemoEntity? entity)
    {
        if (entity is null)
        {
            Visible = false;
            return;
        }

        if (_title is null || _kind is null || _details is null || _graph is null)
            return;

        _title.Text = entity.Name;
        _kind.Text = entity.Kind == DemoEntityKind.Creature ? "СУЩЕСТВО" : "РАСТЕНИЕ";

        foreach (var child in _details.GetChildren())
        {
            _details.RemoveChild(child);
            child.QueueFree();
        }

        AddDetail("ID", entity.Id);
        AddDetail("Вид", entity.Species);
        AddDetail("Подвид", entity.Subspecies);
        AddDetail("Возраст", $"{entity.AgeDays} дн.");
        AddDetail("Размер", $"{entity.Size:0.0}");

        if (entity.Kind == DemoEntityKind.Creature)
        {
            AddProgress("Здоровье", entity.Health);
            AddProgress("Энергия", entity.Energy);
            AddDetail("Скорость", $"{entity.Speed:0.0}");
            AddDetail("Питание", entity.Diet);
        }
        else
        {
            AddProgress("Состояние", entity.Health);
            AddDetail("Тип", entity.PlantType);
            AddDetail("Статус", entity.State);
        }

        AddDetail("Популяция вида", entity.Population.ToString());
        _graph.SetValues(entity.PopulationHistory);

        Visible = true;
        Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.16);
    }

    private void AddDetail(string name, string value)
    {
        if (_details is null)
            return;

        var row = new HBoxContainer();
        var label = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(label);

        var data = new Label { Text = value };
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        row.AddChild(data);
        _details.AddChild(row);
    }

    private void AddProgress(string name, float value)
    {
        if (_details is null)
            return;

        var block = new VBoxContainer();
        var row = new HBoxContainer();
        var label = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(label);

        var percent = new Label { Text = $"{value * 100f:0}%" };
        row.AddChild(percent);
        block.AddChild(row);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = value * 100f,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 7)
        };
        block.AddChild(bar);
        _details.AddChild(block);
    }
}
