using System;
using System.Collections.Generic;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public enum LineageFilter
{
    All,
    Plants,
    Creatures,
    Extinct,
    Active
}

public sealed partial class LineageCanvas : Control
{
    private const float NodeWidth = 190f;
    private const float NodeHeight = 92f;

    private DemoWorldDataProvider? _world;
    private Control? _content;
    private readonly Dictionary<string, PanelContainer> _views = new();
    private readonly Dictionary<string, Vector2> _positions = new();

    private Vector2 _pan;
    private float _zoom = 1f;
    private bool _dragging;
    private LineageFilter _filter = LineageFilter.All;

    public void Configure(DemoWorldDataProvider world)
    {
        _world = world;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;

        _content = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_content);

        BuildNodes();

        if (_world is not null)
            _world.DataChanged += RefreshData;

        Resized += UpdateTransform;
        UpdateFilter();
        UpdateTransform();
    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.DataChanged -= RefreshData;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
            {
                ZoomIn();
                AcceptEvent();
                return;
            }

            if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
            {
                ZoomOut();
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

    public override void _Draw()
    {
        DrawGrid();
        DrawConnections();
    }

    public void ZoomIn()
    {
        _zoom = Mathf.Clamp(_zoom * 1.15f, 0.55f, 2.1f);
        UpdateTransform();
    }

    public void ZoomOut()
    {
        _zoom = Mathf.Clamp(_zoom / 1.15f, 0.55f, 2.1f);
        UpdateTransform();
    }

    public void ResetView()
    {
        _zoom = 1f;
        _pan = Vector2.Zero;
        UpdateTransform();
    }

    public void CenterView()
    {
        _pan = Vector2.Zero;
        UpdateTransform();
    }

    public void SetFilter(LineageFilter filter)
    {
        _filter = filter;
        UpdateFilter();
    }

    private void BuildNodes()
    {
        if (_world is null || _content is null)
            return;

        _positions.Clear();
        _positions["origin"] = new Vector2(-520, -46);
        _positions["viridia"] = new Vector2(-185, -230);
        _positions["motilis"] = new Vector2(-185, 140);
        _positions["viridia_minor"] = new Vector2(185, -325);
        _positions["viridia_aqua"] = new Vector2(185, -175);
        _positions["motilis_minor"] = new Vector2(185, 65);
        _positions["motilis_longa"] = new Vector2(185, 205);
        _positions["motilis_brevis"] = new Vector2(185, 345);

        foreach (var species in _world.Species)
        {
            var panel = BuildNode(species);
            panel.Position = _positions.TryGetValue(species.Id, out var position) ? position : Vector2.Zero;
            panel.Size = new Vector2(NodeWidth, NodeHeight);
            panel.CustomMinimumSize = new Vector2(NodeWidth, NodeHeight);
            panel.MouseFilter = MouseFilterEnum.Ignore;
            _content.AddChild(panel);
            _views[species.Id] = panel;
        }
    }

    private PanelContainer BuildNode(DemoSpeciesRecord species)
    {
        var accent = NodeColor(species);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", NodeStyle(accent, species.Status == DemoSpeciesStatus.Extinct));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 3);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var icon = new TextureRect
        {
            Texture = EvolitIcons.Load(species.Kind switch
            {
                DemoSpeciesKind.Plant => "biology/plant.svg",
                DemoSpeciesKind.Creature => "biology/creature.svg",
                _ => "biology/life.svg"
            }),
            CustomMinimumSize = new Vector2(22, 22),
            Modulate = accent,
            MouseFilter = MouseFilterEnum.Ignore
        };
        header.AddChild(icon);

        var title = new Label
        {
            Text = species.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.Disabled : EvolitPalette.MistWhite);
        header.AddChild(title);

        var badge = new Label { Text = species.Status == DemoSpeciesStatus.Extinct ? "ВЫМЕР" : "АКТИВЕН" };
        badge.AddThemeFontSizeOverride("font_size", 9);
        badge.AddThemeColorOverride("font_color", species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.WarmAlert : accent);
        header.AddChild(badge);

        var meta = new Label
        {
            Text = $"День {species.DayAppeared} · популяция {species.Population}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        meta.AddThemeFontSizeOverride("font_size", 10);
        meta.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(meta);

        var adapt = new Label { Text = $"Адаптивность {species.Adaptability * 100:0}%" };
        adapt.AddThemeFontSizeOverride("font_size", 10);
        adapt.AddThemeColorOverride("font_color", accent);
        root.AddChild(adapt);

        return panel;
    }

    private void RefreshData()
    {
        if (_world is null)
            return;

        foreach (var species in _world.Species)
        {
            if (!_views.TryGetValue(species.Id, out var old))
                continue;

            var position = old.Position;
            var parent = old.GetParent();
            parent?.RemoveChild(old);
            old.QueueFree();

            var replacement = BuildNode(species);
            replacement.Position = position;
            replacement.Size = new Vector2(NodeWidth, NodeHeight);
            replacement.CustomMinimumSize = new Vector2(NodeWidth, NodeHeight);
            replacement.MouseFilter = MouseFilterEnum.Ignore;
            _content?.AddChild(replacement);
            _views[species.Id] = replacement;
        }

        UpdateFilter();
        QueueRedraw();
    }

    private void UpdateFilter()
    {
        if (_world is null)
            return;

        foreach (var species in _world.Species)
        {
            if (!_views.TryGetValue(species.Id, out var view))
                continue;

            view.Visible = _filter switch
            {
                LineageFilter.Plants => species.Kind is DemoSpeciesKind.Plant or DemoSpeciesKind.Origin,
                LineageFilter.Creatures => species.Kind is DemoSpeciesKind.Creature or DemoSpeciesKind.Origin,
                LineageFilter.Extinct => species.Status == DemoSpeciesStatus.Extinct || species.Kind == DemoSpeciesKind.Origin,
                LineageFilter.Active => species.Status == DemoSpeciesStatus.Active,
                _ => true
            };
        }

        QueueRedraw();
    }

    private void UpdateTransform()
    {
        if (_content is null)
            return;

        _content.Scale = Vector2.One * _zoom;
        _content.Position = Size * 0.5f + _pan;
        QueueRedraw();
    }

    private void DrawGrid()
    {
        var spacing = Math.Max(28f, 58f * _zoom);
        var origin = Size * 0.5f + _pan;
        var offsetX = origin.X % spacing;
        var offsetY = origin.Y % spacing;
        var color = new Color(EvolitPalette.SoftAqua, 0.035f);

        for (var x = offsetX; x < Size.X; x += spacing)
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), color, 1f);

        for (var y = offsetY; y < Size.Y; y += spacing)
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), color, 1f);

        var dotColor = new Color(EvolitPalette.EvolutionCyan, 0.055f);
        for (var x = offsetX; x < Size.X; x += spacing)
        {
            for (var y = offsetY; y < Size.Y; y += spacing)
                DrawCircle(new Vector2(x, y), 1.2f, dotColor);
        }
    }

    private void DrawConnections()
    {
        if (_world is null)
            return;

        foreach (var child in _world.Species)
        {
            if (string.IsNullOrWhiteSpace(child.ParentId))
                continue;

            if (!_positions.TryGetValue(child.Id, out var childPos) ||
                !_positions.TryGetValue(child.ParentId, out var parentPos) ||
                !_views.TryGetValue(child.Id, out var childView) ||
                !_views.TryGetValue(child.ParentId, out var parentView) ||
                !childView.Visible ||
                !parentView.Visible)
            {
                continue;
            }

            var startLocal = parentPos + new Vector2(NodeWidth, NodeHeight * 0.5f);
            var endLocal = childPos + new Vector2(0, NodeHeight * 0.5f);
            var start = LocalToScreen(startLocal);
            var end = LocalToScreen(endLocal);
            var middleX = (start.X + end.X) * 0.5f;
            var color = new Color(NodeColor(child), child.Status == DemoSpeciesStatus.Extinct ? 0.28f : 0.55f);

            DrawLine(start, new Vector2(middleX, start.Y), color, 2f, true);
            DrawLine(new Vector2(middleX, start.Y), new Vector2(middleX, end.Y), color, 2f, true);
            DrawLine(new Vector2(middleX, end.Y), end, color, 2f, true);
            DrawCircle(end, 3f, color);
        }
    }

    private Vector2 LocalToScreen(Vector2 local)
    {
        return Size * 0.5f + _pan + local * _zoom;
    }

    private static Color NodeColor(DemoSpeciesRecord species)
    {
        if (species.Status == DemoSpeciesStatus.Extinct)
            return EvolitPalette.WarmAlert;

        return species.Kind switch
        {
            DemoSpeciesKind.Plant => EvolitPalette.YoungLeaf,
            DemoSpeciesKind.Creature => EvolitPalette.SoftAqua,
            _ => EvolitPalette.EvolutionCyan
        };
    }

    private static StyleBoxFlat NodeStyle(Color accent, bool extinct)
    {
        return new StyleBoxFlat
        {
            BgColor = extinct
                ? new Color(0.075f, 0.065f, 0.055f, 0.93f)
                : new Color(0.025f, 0.105f, 0.125f, 0.95f),
            BorderColor = new Color(accent, extinct ? 0.52f : 0.72f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        };
    }
}
