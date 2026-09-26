using System;
using Godot;

namespace Evolit.UI;

public sealed partial class EvolitConfirmDialog : Control
{
    private Label? _title;
    private Label? _message;
    private Button? _confirm;
    private Action? _onConfirm;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect
        {
            Color = new Color(0.005f, 0.025f, 0.032f, 0.72f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(470, 0)
        };
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.98f));
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        card.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(_title);

        _message = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _message.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(_message);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        root.AddChild(buttons);

        var cancel = new Button { Text = "Отмена", CustomMinimumSize = new Vector2(120, 44) };
        cancel.Icon = EvolitIcons.Load("actions/cancel.svg");
        cancel.Pressed += HideDialog;
        buttons.AddChild(cancel);

        _confirm = new Button { CustomMinimumSize = new Vector2(150, 44) };
        _confirm.Icon = EvolitIcons.Load("actions/apply.svg");
        _confirm.Pressed += Confirm;
        buttons.AddChild(_confirm);
    }

    public bool TryCancel()
    {
        if (!Visible)
            return false;

        HideDialog();
        return true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed("game_pause"))
            return;

        HideDialog();
        GetViewport().SetInputAsHandled();
    }

    public void ShowDialog(string title, string message, string confirmText, Action onConfirm)
    {
        if (_title is null || _message is null || _confirm is null)
            return;

        _title.Text = title;
        _message.Text = message;
        _confirm.Text = confirmText;
        _onConfirm = onConfirm;
        Visible = true;
    }

    private void Confirm()
    {
        var callback = _onConfirm;
        HideDialog();
        callback?.Invoke();
    }

    private void HideDialog()
    {
        Visible = false;
        _onConfirm = null;
    }
}
