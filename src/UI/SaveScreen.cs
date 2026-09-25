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

    public void Configure(
        SaveManager saveManager,
        GameSession? session,
        SaveScreenMode mode)
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

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", UiMetrics.Space(56));
        outer.AddThemeConstantOverride("margin_right", UiMetrics.Space(56));
        outer.AddThemeConstantOverride("margin_top", UiMetrics.Space(40));
        outer.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(40));
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.97f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(24));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(24));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(20));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(20));
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", UiMetrics.Space(9));
        margin.AddChild(root);

        var heading = new HBoxContainer();
        root.AddChild(heading);

        var titles = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        heading.AddChild(titles);

        var eyebrow = new Label { Text = "SESSION  ·  STORAGE" };
        eyebrow.AddThemeFontSizeOverride("font_size", UiMetrics.Font(9));
        eyebrow.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.EvolutionCyan, 0.72f));
        titles.AddChild(eyebrow);

        var title = new Label
        {
            Text = _mode == SaveScreenMode.Load
                ? "Загрузить сохранение"
                : "Сохранения"
        };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(30));
        titles.AddChild(title);

        var subtitle = new Label
        {
            Text = "Manual и autosave · backup используется для безопасного восстановления."
        };
        subtitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        subtitle.AddThemeColorOverride(
            "font_color",
            EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var back = new Button
        {
            Text = "Назад",
            Icon = EvolitIcons.Load("actions/back.svg"),
            CustomMinimumSize = UiMetrics.Size(108, 40)
        };
        back.Pressed += () => BackRequested?.Invoke();
        UiMotion.BindButton(back);
        heading.AddChild(back);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        _list = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _list.AddThemeConstantOverride("separation", UiMetrics.Space(8));
        scroll.AddChild(_list);

        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("cancel"))
        {
            BackRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Refresh()
    {
        if (_list is null || _saveManager is null)
            return;

        ClearChildren(_list);

        var slots = _saveManager.ListSaves();
        if (slots.Count == 0)
        {
            _list.AddChild(BuildEmptyState());
            return;
        }

        foreach (var slot in slots)
            _list.AddChild(BuildSlot(slot));

        UiMotion.FadeIn(_list, new Vector2(0, UiMetrics.Px(6)));
    }

    private Control BuildEmptyState()
    {
        var empty = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(0, 138)
        };
        empty.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.SubtleSectionStyle());

        var center = new CenterContainer();
        empty.AddChild(center);

        var copy = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        center.AddChild(copy);

        var title = new Label
        {
            Text = "Сохранений пока нет",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(18));
        copy.AddChild(title);

        var text = new Label
        {
            Text = "Создайте новый мир — первая точка сохранения появится автоматически.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        text.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        text.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        copy.AddChild(text);

        return empty;
    }

    private Control BuildSlot(SaveSlot slot)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(0, 92)
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.SubtleSectionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(14));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(12));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(10));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(10));
        panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiMetrics.Space(12));
        margin.AddChild(row);

        var info = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        info.AddThemeConstantOverride("separation", UiMetrics.Space(2));
        row.AddChild(info);

        var document = slot.Document;
        var name = document?.WorldName
            ?? $"Повреждённый файл · {Path.GetFileName(slot.Path)}";

        var title = new Label
        {
            Text = name,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(17));
        info.AddChild(title);

        var status = slot.Status switch
        {
            SaveSlotStatus.Invalid => "Повреждено",
            SaveSlotStatus.Recoverable => "Доступен backup",
            _ when document?.SaveType == SaveManager.AutosaveType =>
                $"Автосохранение #{document.AutosaveIndex}",
            _ => "Ручное сохранение"
        };

        var statusLabel = new Label { Text = status };
        statusLabel.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        statusLabel.AddThemeColorOverride(
            "font_color",
            slot.Status switch
            {
                SaveSlotStatus.Invalid => EvolitPalette.WarmAlert,
                SaveSlotStatus.Recoverable => EvolitPalette.WarmSand,
                _ => EvolitPalette.SoftAqua
            });
        info.AddChild(statusLabel);

        var metaText = document is not null
            ? $"{document.SavedAt.ToLocalTime():dd.MM.yyyy HH:mm} · {FormatPlaytime(document.PlaytimeSeconds)} · seed {document.Seed} · {document.WorldSize}"
            : slot.Error ?? "Файл не удалось прочитать.";

        var meta = new Label
        {
            Text = metaText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        meta.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        meta.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        info.AddChild(meta);

        var actions = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        actions.AddThemeConstantOverride("separation", UiMetrics.Space(6));
        row.AddChild(actions);

        if (slot.CanLoad)
        {
            var load = CompactButton(
                "Загрузить",
                "menu/continue.svg");
            load.Pressed += () => LoadRequested?.Invoke(slot);
            NatureTechTheme.MarkPrimary(load);
            actions.AddChild(load);
        }

        if (document?.SaveType == SaveManager.ManualType
            && _session is not null
            && document.SaveId == _session.SaveId)
        {
            var overwrite = CompactButton(
                "Перезаписать",
                "actions/apply.svg");
            overwrite.Pressed += () => OverwriteRequested?.Invoke(slot);
            actions.AddChild(overwrite);
        }

        var delete = CompactButton(
            "Удалить",
            "actions/close.svg");
        delete.Pressed += () => DeleteRequested?.Invoke(slot);
        NatureTechTheme.MarkDanger(delete);
        actions.AddChild(delete);

        return panel;
    }

    private static Button CompactButton(string text, string icon)
    {
        var button = new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(icon),
            CustomMinimumSize = UiMetrics.Size(102, 36)
        };
        UiMotion.BindButton(button);
        return button;
    }

    private static string FormatPlaytime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
