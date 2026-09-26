using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public enum LineageFilter { All, Plants, Creatures, Extinct, Active }

public sealed partial class LineageCanvas : Control
{
    private const float NodeWidth = 190f;
    private const float NodeHeight = 92f;
    public event Action<DemoSpeciesRecord>? SpeciesSelected;

    private DemoWorldDataProvider? _world;
    private Control? _content;
    private readonly Dictionary<string, LineageNodeView> _views = new();
    private readonly Dictionary<string, Vector2> _positions = new();
    private Vector2 _pan;
    private float _zoom = 1f;
    private bool _dragging;
    private bool _showSubspecies = true;
    private LineageFilter _filter = LineageFilter.All;
    private string? _selectedId;

    public void Configure(DemoWorldDataProvider world) => _world = world;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        _content = new Control();
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

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton b)
        {
            if (b.ButtonIndex == MouseButton.WheelUp && b.Pressed)
            {
                ZoomAt(b.Position, 1.15f);
                AcceptEvent();
                return;
            }
            if (b.ButtonIndex == MouseButton.WheelDown && b.Pressed)
            {
                ZoomAt(b.Position, 1f / 1.15f);
                AcceptEvent();
                return;
            }
            if (b.ButtonIndex is MouseButton.Middle or MouseButton.Right)
            {
                _dragging = b.Pressed;
                AcceptEvent();
                return;
            }
        }

        if (e is InputEventMouseMotion m && _dragging)
        {
            _pan += m.Relative;
            UpdateTransform();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        DrawGrid();
        DrawConnections();
    }

    public void ZoomIn() => ZoomAt(Size * 0.5f, 1.15f);
    public void ZoomOut() => ZoomAt(Size * 0.5f, 1f / 1.15f);
    public void ResetView() { _zoom = 1; _pan = Vector2.Zero; UpdateTransform(); }
    public void CenterView() { _pan = Vector2.Zero; UpdateTransform(); }
    public void SetFilter(LineageFilter filter) { _filter = filter; UpdateFilter(); }
    public void SetShowSubspecies(bool show) { _showSubspecies = show; UpdateFilter(); }

    public bool SelectByName(string query)
    {
        if (_world is null || string.IsNullOrWhiteSpace(query))
            return false;

        var species = _world.Species.FirstOrDefault(s => s.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
        if (species is null)
            return false;

        SelectSpecies(species);
        FocusSpecies(species.Id);
        return true;
    }

    public void FocusSpecies(string id)
    {
        if (!_positions.TryGetValue(id, out var pos))
            return;

        _pan = -(pos + new Vector2(NodeWidth / 2, NodeHeight / 2)) * _zoom;
        UpdateTransform();
    }

    private void ZoomAt(Vector2 screenPoint, float factor)
    {
        var local = (screenPoint - Size * 0.5f - _pan) / Math.Max(_zoom, 0.01f);
        _zoom = Mathf.Clamp(_zoom * factor, .55f, 2.1f);
        _pan = screenPoint - Size * 0.5f - local * _zoom;
        UpdateTransform();
    }

    private void BuildNodes()
    {
        if (_world is null || _content is null)
            return;

        AssignPositions();
        foreach (var species in _world.Species)
            AddNode(species);
    }

    private void AssignPositions()
    {
        if (_world is null)
            return;

        _positions.Clear();
        _positions["origin"] = new(-520, -46);
        _positions["viridia"] = new(-185, -230);
        _positions["motilis"] = new(-185, 140);
        _positions["viridia_minor"] = new(185, -325);
        _positions["viridia_aqua"] = new(185, -175);
        _positions["motilis_minor"] = new(185, 65);
        _positions["motilis_longa"] = new(185, 205);
        _positions["motilis_brevis"] = new(185, 345);

        var rowsByDepth = new Dictionary<int, int>();
        foreach (var species in _world.Species)
        {
            if (_positions.ContainsKey(species.Id))
                continue;

            var depth = FindDepth(species);
            rowsByDepth.TryGetValue(depth, out var row);
            var candidate = new Vector2(-520 + depth * 370, -320 + row * 125);

            while (_positions.Values.Any(existing => existing.DistanceTo(candidate) < NodeHeight * 1.15f))
            {
                row++;
                candidate.Y = -320 + row * 125;
            }

            rowsByDepth[depth] = row + 1;
            _positions[species.Id] = candidate;
        }
    }

    private int FindDepth(DemoSpeciesRecord species)
    {
        if (_world is null || string.IsNullOrWhiteSpace(species.ParentId))
            return 0;

        var depth = 0;
        var cursor = species;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrWhiteSpace(cursor.ParentId) && depth < 12 && visited.Add(cursor.Id))
        {
            depth++;
            var parent = _world.Species.FirstOrDefault(item => item.Id == cursor.ParentId);
            if (parent is null)
                break;
            cursor = parent;
        }
        return depth;
    }

    private void AddNode(DemoSpeciesRecord species)
    {
        if (_content is null)
            return;

        var view = BuildNode(species);
        view.Panel.Position = _positions.TryGetValue(species.Id, out var position) ? position : Vector2.Zero;
        view.Panel.Size = view.Panel.CustomMinimumSize = new Vector2(NodeWidth, NodeHeight);
        _content.AddChild(view.Panel);
        _views[species.Id] = view;
    }

    private LineageNodeView BuildNode(DemoSpeciesRecord species)
    {
        var accent = NodeColor(species);
        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = $"Открыть {species.Name}"
        };
        panel.AddThemeStyleboxOverride("panel", NodeStyle(accent, species.Status == DemoSpeciesStatus.Extinct, species.Id == _selectedId));

        var speciesId = species.Id;
        panel.GuiInput += e =>
        {
            if (e is not InputEventMouseButton b || b.ButtonIndex != MouseButton.Left || !b.Pressed || _world is null)
                return;

            var current = _world.Species.FirstOrDefault(item => item.Id == speciesId);
            if (current is null)
                return;

            SelectSpecies(current);
            AcceptEvent();
        };

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

        var portrait = new SpeciesPortrait { CustomMinimumSize = new Vector2(26, 26), MouseFilter = MouseFilterEnum.Ignore };
        if (_world is not null)
            portrait.Configure(species, _world.Species);
        header.AddChild(portrait);

        var title = new Label
        {
            Text = species.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", species.ParentId == "origin" ? 15 : 13);
        header.AddChild(title);

        var badge = new Label { MouseFilter = MouseFilterEnum.Ignore };
        badge.AddThemeFontSizeOverride("font_size", 9);
        header.AddChild(badge);

        var meta = new Label { MouseFilter = MouseFilterEnum.Ignore };
        meta.AddThemeFontSizeOverride("font_size", 10);
        meta.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(meta);

        var adapt = new Label { MouseFilter = MouseFilterEnum.Ignore };
        adapt.AddThemeFontSizeOverride("font_size", 10);
        root.AddChild(adapt);

        var view = new LineageNodeView(panel, title, badge, meta, adapt);
        UpdateNodeView(view, species);
        return view;
    }

    private void UpdateNodeView(LineageNodeView view, DemoSpeciesRecord species)
    {
        var accent = NodeColor(species);
        view.Title.Text = species.Name;
        view.Title.AddThemeColorOverride(
            "font_color",
            species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.Disabled : EvolitPalette.MistWhite);
        view.Badge.Text = species.Status == DemoSpeciesStatus.Extinct
            ? "ВЫМЕР"
            : species.Kind == DemoSpeciesKind.Origin ? "КОРЕНЬ" : "АКТИВЕН";
        view.Badge.AddThemeColorOverride(
            "font_color",
            species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.WarmAlert : accent);
        view.Meta.Text = $"День {species.DayAppeared} · популяция {species.Population}";
        view.Adapt.Text = $"Адаптивность {species.Adaptability * 100:0}%";
        view.Adapt.AddThemeColorOverride("font_color", accent);
        view.Panel.TooltipText = $"Открыть {species.Name}";
        view.Panel.AddThemeStyleboxOverride(
            "panel",
            NodeStyle(accent, species.Status == DemoSpeciesStatus.Extinct, species.Id == _selectedId));
    }

    private void SelectSpecies(DemoSpeciesRecord species)
    {
        _selectedId = species.Id;
        RefreshStyles();
        SpeciesSelected?.Invoke(species);
    }

    private void RefreshStyles()
    {
        if (_world is null)
            return;

        foreach (var species in _world.Species)
            if (_views.TryGetValue(species.Id, out var view))
                UpdateNodeView(view, species);
    }

    private void RefreshData()
    {
        if (_world is null || _content is null)
            return;

        var topologyChanged = _views.Count != _world.Species.Count
            || _world.Species.Any(species => !_views.ContainsKey(species.Id));

        if (topologyChanged)
        {
            foreach (var view in _views.Values)
            {
                _content.RemoveChild(view.Panel);
                view.Panel.QueueFree();
            }
            _views.Clear();
            BuildNodes();
        }
        else
        {
            foreach (var species in _world.Species)
                if (_views.TryGetValue(species.Id, out var view))
                    UpdateNodeView(view, species);
        }

        UpdateFilter();
        UpdateTransform();
    }

    private void UpdateFilter()
    {
        if (_world is null)
            return;

        foreach (var species in _world.Species)
        {
            if (!_views.TryGetValue(species.Id, out var view))
                continue;

            var isSubspecies = species.ParentId != "origin" && !string.IsNullOrWhiteSpace(species.ParentId);
            var category = _filter switch
            {
                LineageFilter.Plants => species.Kind is DemoSpeciesKind.Plant or DemoSpeciesKind.Origin,
                LineageFilter.Creatures => species.Kind is DemoSpeciesKind.Creature or DemoSpeciesKind.Origin,
                LineageFilter.Extinct => species.Status == DemoSpeciesStatus.Extinct || species.Kind == DemoSpeciesKind.Origin,
                LineageFilter.Active => species.Status == DemoSpeciesStatus.Active,
                _ => true
            };
            view.Panel.Visible = category && (_showSubspecies || !isSubspecies || species.Kind == DemoSpeciesKind.Origin);
        }
        QueueRedraw();
    }

    private void UpdateTransform()
    {
        if (_content is null)
            return;

        _content.Scale = Vector2.One * _zoom;
        _content.Position = Size * .5f + _pan;
        QueueRedraw();
    }

    private void DrawGrid()
    {
        var spacing = Math.Max(28f, 58f * _zoom);
        var origin = Size * .5f + _pan;
        var ox = origin.X % spacing;
        var oy = origin.Y % spacing;
        var color = new Color(EvolitPalette.SoftAqua, .035f);

        for (var x = ox; x < Size.X; x += spacing)
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), color);
        for (var y = oy; y < Size.Y; y += spacing)
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), color);
    }

    private void DrawConnections()
    {
        if (_world is null)
            return;

        foreach (var child in _world.Species)
        {
            if (string.IsNullOrWhiteSpace(child.ParentId)
                || !_positions.TryGetValue(child.Id, out var childPosition)
                || !_positions.TryGetValue(child.ParentId, out var parentPosition)
                || !_views.TryGetValue(child.Id, out var childView)
                || !_views.TryGetValue(child.ParentId, out var parentView)
                || !childView.Panel.Visible
                || !parentView.Panel.Visible)
            {
                continue;
            }

            var start = LocalToScreen(parentPosition + new Vector2(NodeWidth, NodeHeight * .5f));
            var end = LocalToScreen(childPosition + new Vector2(0, NodeHeight * .5f));
            var middleX = (start.X + end.X) * .5f;
            var color = new Color(NodeColor(child), child.Status == DemoSpeciesStatus.Extinct ? .28f : .55f);
            DrawLine(start, new Vector2(middleX, start.Y), color, 2, true);
            DrawLine(new Vector2(middleX, start.Y), new Vector2(middleX, end.Y), color, 2, true);
            DrawLine(new Vector2(middleX, end.Y), end, color, 2, true);
            DrawCircle(end, 3, color);
        }
    }

    private Vector2 LocalToScreen(Vector2 local) => Size * .5f + _pan + local * _zoom;

    private static Color NodeColor(DemoSpeciesRecord species) => species.Status == DemoSpeciesStatus.Extinct
        ? EvolitPalette.WarmAlert
        : species.Kind switch
        {
            DemoSpeciesKind.Plant => EvolitPalette.YoungLeaf,
            DemoSpeciesKind.Creature => EvolitPalette.SoftAqua,
            _ => EvolitPalette.EvolutionCyan
        };

    private static StyleBoxFlat NodeStyle(Color accent, bool extinct, bool selected = false) => new()
    {
        BgColor = extinct ? new Color(.075f, .065f, .055f, .93f) : new Color(.025f, .105f, .125f, .95f),
        BorderColor = new Color(accent, selected ? 1f : extinct ? .52f : .72f),
        BorderWidthLeft = selected ? 3 : 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 12,
        CornerRadiusTopRight = 12,
        CornerRadiusBottomLeft = 12,
        CornerRadiusBottomRight = 12
    };

    private sealed record LineageNodeView(
        PanelContainer Panel,
        Label Title,
        Label Badge,
        Label Meta,
        Label Adapt);
}
