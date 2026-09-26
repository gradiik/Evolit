using System;
using Evolit.Versioning;
using Godot;

namespace Evolit.UI;

public sealed partial class VersionsScreen : Control
{
    public event Action? BackRequested;
    public event Action<AppVersionRecord>? RollbackRequested;

    private string? _selectedTarget;

    public void Configure(string? selectedTarget)
    {
        _selectedTarget = selectedTarget;
    }

    public override void _Ready()
    {
        var background = new MenuBackground { Name = "MenuBackground" };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 90);
        outer.AddThemeConstantOverride("margin_right", 90);
        outer.AddThemeConstantOverride("margin_top", 64);
        outer.AddThemeConstantOverride("margin_bottom", 64);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.96f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_bottom", 26);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(titles);

        var eyebrow = new Label { Text = "DEVELOPMENT  ·  CHECKPOINTS" };
        eyebrow.AddThemeFontSizeOverride("font_size", 11);
        eyebrow.AddThemeColorOverride("font_color", new Color(EvolitPalette.EvolutionCyan, 0.82f));
        titles.AddChild(eyebrow);

        var title = new Label { Text = "Версии Evolit" };
        title.AddThemeFontSizeOverride("font_size", 34);
        titles.AddChild(title);

        var subtitle = new Label { Text = "Контрольные состояния разработки" };
        subtitle.AddThemeFontSizeOverride("font_size", 14);
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var back = new Button
        {
            Text = "Назад",
            Icon = EvolitIcons.Load("actions/back.svg"),
            CustomMinimumSize = new Vector2(120, 44)
        };
        back.Pressed += () => BackRequested?.Invoke();
        header.AddChild(back);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        var versions = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        versions.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(versions);

        foreach (var record in AppVersionCatalog.VisibleVersions)
            versions.AddChild(BuildVersionCard(record));

        var note = new Label
        {
            Text = "Откат исходного кода выполняется через Git/Codex после явного подтверждения. Evolit не изменяет рабочую Git директорию самостоятельно.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 12);
        note.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.88f));
        root.AddChild(note);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("game_pause"))
            return;

        BackRequested?.Invoke();
        GetViewport().SetInputAsHandled();
    }

    private Control BuildVersionCard(AppVersionRecord record)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 118),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            record.IsCurrent ? CurrentVersionStyle() : NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        margin.AddChild(row);

        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 3);
        row.AddChild(details);

        var top = new HBoxContainer();
        details.AddChild(top);

        var version = new Label { Text = record.Version };
        version.AddThemeFontSizeOverride("font_size", 24);
        version.AddThemeColorOverride(
            "font_color",
            record.IsCurrent ? EvolitPalette.EvolutionCyan : EvolitPalette.MistWhite);
        top.AddChild(version);

        var status = new Label
        {
            Text = record.IsCurrent ? "  ·  Текущая версия" : "  ·  Контрольная версия"
        };
        status.AddThemeFontSizeOverride("font_size", 12);
        status.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        top.AddChild(status);

        var description = new Label
        {
            Text = record.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeFontSizeOverride("font_size", 14);
        details.AddChild(description);

        var metadata = new Label
        {
            Text = $"{record.Date}  ·  commit: {ShortCommit(record.Commit)}"
        };
        metadata.AddThemeFontSizeOverride("font_size", 11);
        metadata.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.82f));
        details.AddChild(metadata);

        if (!record.IsCurrent)
        {
            var selected = string.Equals(_selectedTarget, record.Version, StringComparison.Ordinal);
            var button = new Button
            {
                Text = selected ? "Выбрано для отката" : "Подготовить откат",
                Icon = EvolitIcons.Load(selected ? "actions/apply.svg" : "simulation/history.svg"),
                CustomMinimumSize = new Vector2(190, 44),
                Disabled = selected
            };
            button.Pressed += () => RollbackRequested?.Invoke(record);
            row.AddChild(button);
        }

        return panel;
    }

    private static string ShortCommit(string commit)
    {
        if (commit.Length < 12 || commit.Contains(' '))
            return commit;

        return commit[..12];
    }

    private static StyleBoxFlat CurrentVersionStyle()
    {
        var style = NatureTechTheme.SectionStyle();
        style.BorderColor = new Color(EvolitPalette.EvolutionCyan, 0.72f);
        style.BorderWidthLeft = 2;
        return style;
    }
}
