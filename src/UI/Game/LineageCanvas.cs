using Godot;

namespace Evolit.UI.Game;

public sealed partial class LineageCanvas : Control
{
    private Control? _nodeCard;
    private Vector2 _pan;
    private float _zoom = 1f;
    private bool _dragging;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;

        _nodeCard = BuildRootNode();
        AddChild(_nodeCard);
        Resized += UpdateTransform;
        UpdateTransform();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
            {
                _zoom = Mathf.Clamp(_zoom * 1.12f, 0.55f, 2.2f);
                UpdateTransform();
                AcceptEvent();
                return;
            }

            if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
            {
                _zoom = Mathf.Clamp(_zoom / 1.12f, 0.55f, 2.2f);
                UpdateTransform();
                AcceptEvent();
                return;
            }

            if (button.ButtonIndex is MouseButton.Middle or MouseButton.Right)
            {
                _dragging = button.Pressed;
                AcceptEvent();
                return;
            }
        }

        if (@event is InputEventMouseMotion motion && _dragging)
        {
            _pan += motion.Relative;
            UpdateTransform();
            AcceptEvent();
        }
    }

    private Control BuildRootNode()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(220, 82),
            Size = new Vector2(220, 82)
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());

        var center = new CenterContainer();
        panel.AddChild(center);

        var label = new Label
        {
            Text = "Первичная форма\nroot · demo",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        center.AddChild(label);
        return panel;
    }

    private void UpdateTransform()
    {
        if (_nodeCard is null)
            return;

        _nodeCard.Scale = Vector2.One * _zoom;
        _nodeCard.Position = Size * 0.5f + _pan - (_nodeCard.Size * _zoom) * 0.5f;
    }
}
