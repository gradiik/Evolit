using System;
using Godot;

namespace Evolit.UI;

public sealed partial class EvolitConfirmDialog : Control
{
    private Label? _title;
    private Label? _message;
    private Button? _confirm;
    private PanelContainer? _card;
    private Action? _onConfirm;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 100;

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

        _card = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(470, 0)
        };
        _card.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.98f));
        center.AddChild(_card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiMetrics.Space(26));
        margin.AddThemeConstantOverride("margin_right", UiMetrics.Space(26));
        margin.AddThemeConstantOverride("margin_top", UiMetrics.Space(22));
        margin.AddThemeConstantOverride("margin_bottom", UiMetrics.Space(22));
        _card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", UiMetrics.Space(10));
        margin.AddChild(root);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(23));
        root.AddChild(_title);

        _message = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _message.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        _message.AddThemeColorOverride(
            "font_color",
            EvolitPalette.FogBlue);
        root.AddChild(_message);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        buttons.AddThemeConstantOverride("separation", UiMetrics.Space(7));
        root.AddChild(buttons);

        var cancel = new Button
        {
            Text = "Отмена",
            Icon = EvolitIcons.Load("actions/cancel.svg"),
            CustomMinimumSize = UiMetrics.Size(110, 40)
        };
        cancel.Pressed += HideDialog;
        UiMotion.BindButton(cancel);
        buttons.AddChild(cancel);

        _confirm = new Button
        {
            CustomMinimumSize = UiMetrics.Size(150, 40)
        };
        _confirm.Icon = EvolitIcons.Load("actions/apply.svg");
        _confirm.Pressed += Confirm;
        UiMotion.BindButton(_confirm);
        buttons.AddChild(_confirm);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("cancel"))
        {
            HideDialog();
            GetViewport().SetInputAsHandled();
        }
    }

    public void ShowDialog(
        string title,
        string message,
        string confirmText,
        Action onConfirm)
    {
        if (_title is null
            || _message is null
            || _confirm is null
            || _card is null)
        {
            return;
        }

        _title.Text = title;
        _message.Text = message;
        _confirm.Text = confirmText;
        _onConfirm = onConfirm;

        _confirm.RemoveThemeStyleboxOverride("normal");
        _confirm.RemoveThemeStyleboxOverride("hover");
        _confirm.RemoveThemeColorOverride("font_color");
        _confirm.RemoveThemeColorOverride("font_hover_color");

        if (confirmText.Contains("Удал", StringComparison.OrdinalIgnoreCase)
            || confirmText.Contains("Выйти", StringComparison.OrdinalIgnoreCase))
        {
            NatureTechTheme.MarkDanger(_confirm);
        }
        else
        {
            NatureTechTheme.MarkPrimary(_confirm);
        }

        Visible = true;
        UiMotion.FadeIn(
            _card,
            new Vector2(0, UiMetrics.Px(8)));
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
