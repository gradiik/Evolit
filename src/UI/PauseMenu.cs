using System;
using Godot;

namespace Evolit.UI;

public sealed partial class PauseMenu : Control
{
    public event Action? ResumeRequested;
    public event Action? SaveRequested;
    public event Action? LoadRequested;
    public event Action? SettingsRequested;
    public event Action? MainMenuRequested;
    public event Action? ExitRequested;

    public override void _Ready()
    {
        var dim = new ColorRect { Color = new Color(0.005f, 0.025f, 0.032f, 0.80f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(430, 0) };
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.98f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        card.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var title = new Label { Text = "Пауза" };
        title.AddThemeFontSizeOverride("font_size", 32);
        root.AddChild(title);

        root.AddChild(Button("Продолжить", "menu/continue.svg", ResumeRequested));
        root.AddChild(Button("Сохранить", "actions/apply.svg", SaveRequested));
        root.AddChild(Button("Загрузить", "menu/saves.svg", LoadRequested));
        root.AddChild(Button("Настройки", "menu/settings.svg", SettingsRequested));
        root.AddChild(Button("В главное меню", "actions/back.svg", MainMenuRequested));
        root.AddChild(Button("Выход", "menu/exit.svg", ExitRequested));
    }

    private static Button Button(string text, string icon, Action? callback)
    {
        var button = new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(icon),
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 48)
        };
        button.Pressed += () => callback?.Invoke();
        return button;
    }
}
