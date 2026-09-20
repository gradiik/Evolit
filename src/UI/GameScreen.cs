using Evolit.Session;
using Godot;

namespace Evolit.UI;

public sealed partial class GameScreen : Control
{
    private GameSession? _session;

    public void Configure(GameSession session)
    {
        _session = session;
    }

    public override void _Ready()
    {
        if (_session is null)
            return;

        var background = new MenuBackground();
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var top = new PanelContainer();
        top.AnchorLeft = 0.03f;
        top.AnchorRight = 0.45f;
        top.AnchorTop = 0.04f;
        top.AnchorBottom = 0.17f;
        top.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        AddChild(top);

        var topMargin = new MarginContainer();
        topMargin.AddThemeConstantOverride("margin_left", 18);
        topMargin.AddThemeConstantOverride("margin_right", 18);
        topMargin.AddThemeConstantOverride("margin_top", 12);
        topMargin.AddThemeConstantOverride("margin_bottom", 12);
        top.AddChild(topMargin);

        var info = new VBoxContainer();
        topMargin.AddChild(info);

        var title = new Label { Text = _session.WorldName };
        title.AddThemeFontSizeOverride("font_size", 22);
        info.AddChild(title);

        var meta = new Label { Text = $"seed {_session.Seed}  ·  {_session.WorldSize}" };
        meta.AddThemeFontSizeOverride("font_size", 13);
        meta.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        info.AddChild(meta);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var message = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        center.AddChild(message);

        var ready = new Label
        {
            Text = "Мир готов к следующему этапу разработки",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        ready.AddThemeFontSizeOverride("font_size", 28);
        ready.AddThemeColorOverride("font_color", new Color(EvolitPalette.MistWhite, 0.78f));
        message.AddChild(ready);

        var sub = new Label
        {
            Text = "Генерация мира и симуляция намеренно ещё не созданы.\nEsc — пауза · F3 — debug",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        sub.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        message.AddChild(sub);
    }
}
