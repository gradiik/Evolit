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
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect
        {
            Color = new Color(0.005f, 0.025f, 0.032f, 0.68f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(390, 0)
        };
        card.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.97f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(28));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(28));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(24));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(24));
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", UiMetrics.Space(6));
        margin.AddChild(root);

        var eyebrow = new Label { Text = "SESSION  ·  PAUSED" };
        eyebrow.AddThemeFontSizeOverride("font_size", UiMetrics.Font(9));
        eyebrow.AddThemeColorOverride(
            "font_color",
            new Color(EvolitPalette.EvolutionCyan, 0.72f));
        root.AddChild(eyebrow);

        var title = new Label { Text = "Пауза" };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(30));
        root.AddChild(title);

        root.AddChild(new Control
        {
            CustomMinimumSize = UiMetrics.Size(0, 6)
        });

        var resume = MenuButton(
            "Продолжить",
            "menu/continue.svg",
            ResumeRequested);
        NatureTechTheme.MarkPrimary(resume);
        root.AddChild(resume);

        root.AddChild(MenuButton(
            "Сохранить",
            "actions/apply.svg",
            SaveRequested));
        root.AddChild(MenuButton(
            "Настройки",
            "menu/settings.svg",
            SettingsRequested));

        root.AddChild(new HSeparator());

        root.AddChild(MenuButton(
            "Загрузить сохранение",
            "menu/saves.svg",
            LoadRequested));
        root.AddChild(MenuButton(
            "В главное меню",
            "actions/back.svg",
            MainMenuRequested));

        var exit = MenuButton(
            "Выйти из Evolit",
            "menu/exit.svg",
            ExitRequested);
        NatureTechTheme.MarkDanger(exit);
        root.AddChild(exit);
    }

    private static Button MenuButton(
        string text,
        string icon,
        Action? callback)
    {
        var button = new Button
        {
            Text = text,
            Icon = EvolitIcons.Load(icon),
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = UiMetrics.Size(0, 43)
        };
        button.Pressed += () => callback?.Invoke();
        UiMotion.BindButton(button);
        return button;
    }
}
