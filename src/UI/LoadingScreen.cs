using Godot;

namespace Evolit.UI;

public sealed partial class LoadingScreen : Control
{
    private ProgressBar? _progress;
    private Label? _operation;
    private Label? _hint;

    public string Operation { get; set; } = "Подготовка мира";

    public override void _Ready()
    {
        var background = new MenuBackground();
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
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

        var title = new Label { Text = "Evolit" };
        title.AddThemeFontSizeOverride("font_size", 38);
        root.AddChild(title);

        _operation = new Label { Text = Operation };
        _operation.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        root.AddChild(_operation);

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 5,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10)
        };
        root.AddChild(_progress);

        _hint = new Label { Text = "Подготовка…" };
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _hint.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(_hint);
    }

    public void SetStage(string operation, double progress, string? hint = null)
    {
        if (_operation is not null)
            _operation.Text = operation;
        if (_progress is not null)
            _progress.Value = Mathf.Clamp((float)progress, 0, 100);
        if (_hint is not null && hint is not null)
            _hint.Text = hint;
    }

    public void SetProgress(double value)
    {
        if (_progress is not null)
            _progress.Value = Mathf.Clamp((float)value, 0, 100);
    }
}
