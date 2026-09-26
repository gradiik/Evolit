using System;
using System.IO;
using Evolit.Save;
using Evolit.Session;
using Godot;

namespace Evolit.UI;

public enum SaveScreenMode
{
    Manage,
    Load
}

public sealed partial class SaveScreen : Control
{
    public event Action? BackRequested;
    public event Action<SaveSlot>? LoadRequested;
    public event Action<SaveSlot>? DeleteRequested;
    public event Action<SaveSlot>? OverwriteRequested;

    private SaveManager? _saveManager;
    private GameSession? _session;
    private SaveScreenMode _mode;
    private VBoxContainer? _list;

    public void Configure(SaveManager saveManager, GameSession? session, SaveScreenMode mode)
    {
        _saveManager = saveManager;
        _session = session;
        _mode = mode;
    }

    public override void _Ready()
    {
        var background = new MenuBackground { MotionScale = 0.28f };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(1120, 620) };
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.97f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        card.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var heading = new HBoxContainer();
        root.AddChild(heading);

        var title = new Label
        {
            Text = _mode == SaveScreenMode.Load ? "Загрузить сохранение" : "Сохранения",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        heading.AddChild(title);

        var back = new Button { Text = "Назад", Icon = EvolitIcons.Load("actions/back.svg"), CustomMinimumSize = new Vector2(118, 42) };
        back.Pressed += () => BackRequested?.Invoke();
        heading.AddChild(back);

        var subtitle = new Label { Text = "Manual и autosave · повреждённые файлы остаются безопасно изолированы" };
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(subtitle);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(_list);

        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("game_pause"))
            return;

        BackRequested?.Invoke();
        GetViewport().SetInputAsHandled();
    }

    public void Refresh()
    {
        if (_list is null || _saveManager is null)
            return;

        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        var slots = _saveManager.ListSaves();
        if (slots.Count == 0)
        {
            var empty = new PanelContainer { CustomMinimumSize = new Vector2(0, 150) };
            empty.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
            var center = new CenterContainer();
            empty.AddChild(center);

            var label = new Label
            {
                Text = "Сохранений пока нет\nСоздайте новый мир — первое сохранение появится автоматически.",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
            center.AddChild(label);
            _list.AddChild(empty);
            return;
        }

        foreach (var slot in slots)
            _list.AddChild(BuildSlot(slot));
    }

    private Control BuildSlot(SaveSlot slot)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 92) };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 11);
        margin.AddThemeConstantOverride("margin_bottom", 11);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 3);
        row.AddChild(info);

        var document = slot.Document;
        var name = document?.WorldName ?? $"Повреждённый файл · {Path.GetFileName(slot.Path)}";
        var title = new Label { Text = name };
        title.AddThemeFontSizeOverride("font_size", 18);
        info.AddChild(title);

        var statusRow = new HBoxContainer();
        info.AddChild(statusRow);

        var status = slot.Status switch
        {
            SaveSlotStatus.Invalid => "Повреждено",
            SaveSlotStatus.Recoverable => "Доступен backup",
            _ when document?.SaveType == SaveManager.AutosaveType => $"Автосохранение #{document.AutosaveIndex}",
            _ => "Ручное сохранение"
        };

        var statusLabel = new Label { Text = status };
        statusLabel.AddThemeFontSizeOverride("font_size", 13);
        statusLabel.AddThemeColorOverride("font_color", slot.Status switch
        {
            SaveSlotStatus.Invalid => EvolitPalette.WarmAlert,
            SaveSlotStatus.Recoverable => EvolitPalette.WarmSand,
            _ => EvolitPalette.SoftAqua
        });
        statusRow.AddChild(statusLabel);

        if (document is not null)
        {
            var meta = new Label
            {
                Text = $"   ·   {document.SavedAt.ToLocalTime():dd.MM.yyyy HH:mm}   ·   {FormatPlaytime(document.PlaytimeSeconds)}   ·   seed {document.Seed}   ·   {document.WorldSize}"
            };
            meta.AddThemeFontSizeOverride("font_size", 12);
            meta.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
            statusRow.AddChild(meta);
        }
        else if (!string.IsNullOrWhiteSpace(slot.Error))
        {
            var error = new Label
            {
                Text = $"   ·   {slot.Error}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            error.AddThemeFontSizeOverride("font_size", 12);
            error.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
            statusRow.AddChild(error);
        }

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 6);
        row.AddChild(actions);

        if (slot.CanLoad)
        {
            var load = CompactButton("Загрузить", "menu/continue.svg");
            load.Pressed += () => LoadRequested?.Invoke(slot);
            actions.AddChild(load);
        }

        if (document?.SaveType == SaveManager.ManualType &&
            _session is not null &&
            document.SaveId == _session.SaveId)
        {
            var overwrite = CompactButton("Перезаписать", "actions/apply.svg");
            overwrite.Pressed += () => OverwriteRequested?.Invoke(slot);
            actions.AddChild(overwrite);
        }

        var delete = CompactButton("Удалить", "actions/close.svg");
        delete.Pressed += () => DeleteRequested?.Invoke(slot);
        actions.AddChild(delete);

        return panel;
    }

    private static Button CompactButton(string text, string icon)
    {
        return new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(icon),
            CustomMinimumSize = new Vector2(108, 36)
        };
    }

    private static string FormatPlaytime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }
}
