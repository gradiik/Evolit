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
    private readonly Dictionary<string, PanelContainer> _views = new();
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
        if (_world is not null) _world.DataChanged += RefreshData;
        Resized += UpdateTransform;
        UpdateFilter();
        UpdateTransform();
    }

    public override void _ExitTree()
    {
        if (_world is not null) _world.DataChanged -= RefreshData;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton b)
        {
            if (b.ButtonIndex == MouseButton.WheelUp && b.Pressed) { ZoomIn(); AcceptEvent(); return; }
            if (b.ButtonIndex == MouseButton.WheelDown && b.Pressed) { ZoomOut(); AcceptEvent(); return; }
            if (b.ButtonIndex is MouseButton.Middle or MouseButton.Right) { _dragging = b.Pressed; AcceptEvent(); return; }
        }
        if (e is InputEventMouseMotion m && _dragging) { _pan += m.Relative; UpdateTransform(); AcceptEvent(); }
    }

    public override void _Draw() { DrawGrid(); DrawConnections(); }
    public void ZoomIn() { _zoom = Mathf.Clamp(_zoom * 1.15f, .55f, 2.1f); UpdateTransform(); }
    public void ZoomOut() { _zoom = Mathf.Clamp(_zoom / 1.15f, .55f, 2.1f); UpdateTransform(); }
    public void ResetView() { _zoom = 1; _pan = Vector2.Zero; UpdateTransform(); }
    public void CenterView() { _pan = Vector2.Zero; UpdateTransform(); }
    public void SetFilter(LineageFilter filter) { _filter = filter; UpdateFilter(); }
    public void SetShowSubspecies(bool show) { _showSubspecies = show; UpdateFilter(); }

    public bool SelectByName(string query)
    {
        if (_world is null || string.IsNullOrWhiteSpace(query)) return false;
        var species = _world.Species.FirstOrDefault(s => s.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
        if (species is null) return false;
        SelectSpecies(species);
        FocusSpecies(species.Id);
        return true;
    }

    public void FocusSpecies(string id)
    {
        if (!_positions.TryGetValue(id, out var pos)) return;
        _pan = -(pos + new Vector2(NodeWidth / 2, NodeHeight / 2)) * _zoom;
        UpdateTransform();
    }

    private void BuildNodes()
    {
        if (_world is null || _content is null) return;
        _positions.Clear();
        _positions["origin"] = new(-520, -46); _positions["viridia"] = new(-185, -230); _positions["motilis"] = new(-185, 140);
        _positions["viridia_minor"] = new(185, -325); _positions["viridia_aqua"] = new(185, -175);
        _positions["motilis_minor"] = new(185, 65); _positions["motilis_longa"] = new(185, 205); _positions["motilis_brevis"] = new(185, 345);
        foreach (var s in _world.Species) AddNode(s);
    }

    private void AddNode(DemoSpeciesRecord species)
    {
        if (_content is null) return;
        var panel = BuildNode(species);
        panel.Position = _positions.TryGetValue(species.Id, out var p) ? p : Vector2.Zero;
        panel.Size = panel.CustomMinimumSize = new Vector2(NodeWidth, NodeHeight);
        _content.AddChild(panel);
        _views[species.Id] = panel;
    }

    private PanelContainer BuildNode(DemoSpeciesRecord species)
    {
        var accent = NodeColor(species);
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = $"Открыть {species.Name}" };
        panel.AddThemeStyleboxOverride("panel", NodeStyle(accent, species.Status == DemoSpeciesStatus.Extinct, species.Id == _selectedId));
        panel.GuiInput += e => { if (e is InputEventMouseButton b && b.ButtonIndex == MouseButton.Left && b.Pressed) { SelectSpecies(species); AcceptEvent(); } };

        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 10); margin.AddThemeConstantOverride("margin_right", 10); margin.AddThemeConstantOverride("margin_top", 8); margin.AddThemeConstantOverride("margin_bottom", 8); panel.AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 3); margin.AddChild(root);
        var header = new HBoxContainer(); root.AddChild(header);
        var portrait = new SpeciesPortrait { CustomMinimumSize = new Vector2(26, 26), MouseFilter = MouseFilterEnum.Ignore };
        if (_world is not null) portrait.Configure(species, _world.Species);
        header.AddChild(portrait);
        var title = new Label { Text = species.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore }; title.AddThemeFontSizeOverride("font_size", species.ParentId == "origin" ? 15 : 13); title.AddThemeColorOverride("font_color", species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.Disabled : EvolitPalette.MistWhite); header.AddChild(title);
        var badge = new Label { Text = species.Status == DemoSpeciesStatus.Extinct ? "ВЫМЕР" : species.Kind == DemoSpeciesKind.Origin ? "КОРЕНЬ" : "АКТИВЕН", MouseFilter = MouseFilterEnum.Ignore }; badge.AddThemeFontSizeOverride("font_size", 9); badge.AddThemeColorOverride("font_color", species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.WarmAlert : accent); header.AddChild(badge);
        var meta = new Label { Text = $"День {species.DayAppeared} · популяция {species.Population}", MouseFilter = MouseFilterEnum.Ignore }; meta.AddThemeFontSizeOverride("font_size", 10); meta.AddThemeColorOverride("font_color", EvolitPalette.FogBlue); root.AddChild(meta);
        var adapt = new Label { Text = $"Адаптивность {species.Adaptability*100:0}%", MouseFilter = MouseFilterEnum.Ignore }; adapt.AddThemeFontSizeOverride("font_size", 10); adapt.AddThemeColorOverride("font_color", accent); root.AddChild(adapt);
        return panel;
    }

    private void SelectSpecies(DemoSpeciesRecord species)
    {
        _selectedId = species.Id;
        RefreshStyles();
        SpeciesSelected?.Invoke(species);
    }

    private void RefreshStyles()
    {
        if (_world is null) return;
        foreach (var s in _world.Species)
            if (_views.TryGetValue(s.Id, out var p))
                p.AddThemeStyleboxOverride("panel", NodeStyle(NodeColor(s), s.Status == DemoSpeciesStatus.Extinct, s.Id == _selectedId));
    }

    private void RefreshData()
    {
        if (_world is null || _content is null) return;
        foreach (var p in _views.Values) { _content.RemoveChild(p); p.QueueFree(); }
        _views.Clear(); BuildNodes(); UpdateFilter(); UpdateTransform();
    }

    private void UpdateFilter()
    {
        if (_world is null) return;
        foreach (var s in _world.Species)
        {
            if (!_views.TryGetValue(s.Id, out var v)) continue;
            var isSubspecies = s.ParentId != "origin" && !string.IsNullOrWhiteSpace(s.ParentId);
            var category = _filter switch {
                LineageFilter.Plants => s.Kind is DemoSpeciesKind.Plant or DemoSpeciesKind.Origin,
                LineageFilter.Creatures => s.Kind is DemoSpeciesKind.Creature or DemoSpeciesKind.Origin,
                LineageFilter.Extinct => s.Status == DemoSpeciesStatus.Extinct || s.Kind == DemoSpeciesKind.Origin,
                LineageFilter.Active => s.Status == DemoSpeciesStatus.Active,
                _ => true };
            v.Visible = category && (_showSubspecies || !isSubspecies || s.Kind == DemoSpeciesKind.Origin);
        }
        QueueRedraw();
    }

    private void UpdateTransform()
    {
        if (_content is null) return;
        _content.Scale = Vector2.One * _zoom; _content.Position = Size * .5f + _pan; QueueRedraw();
    }

    private void DrawGrid()
    {
        var spacing=Math.Max(28f,58f*_zoom); var origin=Size*.5f+_pan; var ox=origin.X%spacing; var oy=origin.Y%spacing; var c=new Color(EvolitPalette.SoftAqua,.035f);
        for(var x=ox;x<Size.X;x+=spacing) DrawLine(new(x,0),new(x,Size.Y),c);
        for(var y=oy;y<Size.Y;y+=spacing) DrawLine(new(0,y),new(Size.X,y),c);
    }

    private void DrawConnections()
    {
        if (_world is null) return;
        foreach(var child in _world.Species)
        {
            if(string.IsNullOrWhiteSpace(child.ParentId) || !_positions.TryGetValue(child.Id,out var cp) || !_positions.TryGetValue(child.ParentId,out var pp) || !_views.TryGetValue(child.Id,out var cv) || !_views.TryGetValue(child.ParentId,out var pv) || !cv.Visible || !pv.Visible) continue;
            var start=LocalToScreen(pp+new Vector2(NodeWidth,NodeHeight*.5f)); var end=LocalToScreen(cp+new Vector2(0,NodeHeight*.5f)); var mx=(start.X+end.X)*.5f; var color=new Color(NodeColor(child),child.Status==DemoSpeciesStatus.Extinct?.28f:.55f);
            DrawLine(start,new(mx,start.Y),color,2,true); DrawLine(new(mx,start.Y),new(mx,end.Y),color,2,true); DrawLine(new(mx,end.Y),end,color,2,true); DrawCircle(end,3,color);
        }
    }

    private Vector2 LocalToScreen(Vector2 local)=>Size*.5f+_pan+local*_zoom;
    private static Color NodeColor(DemoSpeciesRecord s)=>s.Status==DemoSpeciesStatus.Extinct?EvolitPalette.WarmAlert:s.Kind switch{DemoSpeciesKind.Plant=>EvolitPalette.YoungLeaf,DemoSpeciesKind.Creature=>EvolitPalette.SoftAqua,_=>EvolitPalette.EvolutionCyan};
    private static StyleBoxFlat NodeStyle(Color accent,bool extinct,bool selected=false)=>new(){BgColor=extinct?new(.075f,.065f,.055f,.93f):new(.025f,.105f,.125f,.95f),BorderColor=new Color(accent,selected?1f:extinct?.52f:.72f),BorderWidthLeft=selected?3:1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12};
}
