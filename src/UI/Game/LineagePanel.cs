using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class LineagePanel : Control
{
    public event Action? CloseRequested;
    private DemoWorldDataProvider? _world;
    private LineageCanvas? _canvas;
    private VBoxContainer? _inspector;
    private LineEdit? _search;
    private readonly Dictionary<LineageFilter,Button> _filters=new();

    public void Configure(DemoWorldDataProvider world)=>_world=world;

    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Stop;
        var dim=new ColorRect{Color=new(.004f,.024f,.030f,.58f),MouseFilter=MouseFilterEnum.Stop}; dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(dim);
        var outer=new MarginContainer(); outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); outer.AddThemeConstantOverride("margin_left",54);outer.AddThemeConstantOverride("margin_right",54);outer.AddThemeConstantOverride("margin_top",42);outer.AddThemeConstantOverride("margin_bottom",54);AddChild(outer);
        var card=new PanelContainer();card.AddThemeStyleboxOverride("panel",NatureTechTheme.CardStyle(.99f));outer.AddChild(card);
        var margin=new MarginContainer();margin.AddThemeConstantOverride("margin_left",20);margin.AddThemeConstantOverride("margin_right",20);margin.AddThemeConstantOverride("margin_top",18);margin.AddThemeConstantOverride("margin_bottom",18);card.AddChild(margin);
        var root=new VBoxContainer();root.AddThemeConstantOverride("separation",8);margin.AddChild(root);
        var header=new HBoxContainer();root.AddChild(header);
        var titles=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};header.AddChild(titles);
        var title=new Label{Text="Генетическое древо"};title.AddThemeFontSizeOverride("font_size",30);titles.AddChild(title);
        var subtitle=new Label{Text="Выберите узел для подробностей · панорамирование — средней/правой кнопкой · масштаб — колесом"};subtitle.AddThemeFontSizeOverride("font_size",12);subtitle.AddThemeColorOverride("font_color",EvolitPalette.FogBlue);titles.AddChild(subtitle);
        var controls=new HBoxContainer();controls.AddThemeConstantOverride("separation",6);header.AddChild(controls);
        controls.AddChild(ActionButton("−","Уменьшить",()=>_canvas?.ZoomOut()));controls.AddChild(ActionButton("+","Увеличить",()=>_canvas?.ZoomIn()));controls.AddChild(ActionButton("Сброс","Сбросить масштаб и позицию",()=>_canvas?.ResetView()));controls.AddChild(ActionButton("По центру","Вернуть дерево в центр",()=>_canvas?.CenterView()));
        var close=new Button{Text="Закрыть",Icon=EvolitIcons.Load("actions/close.svg"),CustomMinimumSize=new(110,38),TooltipText="Закрыть панель"};close.Pressed+=()=>CloseRequested?.Invoke();controls.AddChild(close);

        var filters=new HBoxContainer();filters.AddThemeConstantOverride("separation",6);root.AddChild(filters);
        AddFilter(filters,LineageFilter.All,"Все");AddFilter(filters,LineageFilter.Active,"Активные");AddFilter(filters,LineageFilter.Extinct,"Вымершие");AddFilter(filters,LineageFilter.Plants,"Растения");AddFilter(filters,LineageFilter.Creatures,"Существа");
        var subs=new CheckButton{Text="Подвиды",ButtonPressed=true,TooltipText="Показывать дочерние формы"};subs.Toggled+=show=>_canvas?.SetShowSubspecies(show);filters.AddChild(subs);
        filters.AddChild(new Control{SizeFlagsHorizontal=SizeFlags.ExpandFill});
        _search=new LineEdit{PlaceholderText="Найти таксон…",CustomMinimumSize=new(220,34),ClearButtonEnabled=true};_search.TextSubmitted+=Search;filters.AddChild(_search);
        var find=new Button{Text="Найти",CustomMinimumSize=new(72,34)};find.Pressed+=()=>Search(_search?.Text??"");filters.AddChild(find);
        root.AddChild(new HSeparator());

        var body=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",10);root.AddChild(body);
        _canvas=new LineageCanvas{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill}; if(_world is not null)_canvas.Configure(_world); _canvas.SpeciesSelected+=ShowSpecies;body.AddChild(_canvas);
        var inspectPanel=new PanelContainer{CustomMinimumSize=new(260,0),SizeFlagsVertical=SizeFlags.ExpandFill};inspectPanel.AddThemeStyleboxOverride("panel",NatureTechTheme.SectionStyle());body.AddChild(inspectPanel);
        var im=new MarginContainer();im.AddThemeConstantOverride("margin_left",14);im.AddThemeConstantOverride("margin_right",14);im.AddThemeConstantOverride("margin_top",14);im.AddThemeConstantOverride("margin_bottom",14);inspectPanel.AddChild(im);
        _inspector=new VBoxContainer();_inspector.AddThemeConstantOverride("separation",8);im.AddChild(_inspector);ShowEmptyInspector();
        UpdateFilterButtons(LineageFilter.All);
    }

    private void Search(string text){if(_canvas is null||string.IsNullOrWhiteSpace(text))return;if(!_canvas.SelectByName(text)){_search!.PlaceholderText="Ничего не найдено";_search.Text="";}}

    private void ShowSpecies(DemoSpeciesRecord s)
    {
        if(_inspector is null||_world is null)return;Clear(_inspector);
        var h=new Label{Text=s.Name};h.AddThemeFontSizeOverride("font_size",21);h.AddThemeColorOverride("font_color",s.Kind==DemoSpeciesKind.Plant?EvolitPalette.YoungLeaf:s.Kind==DemoSpeciesKind.Creature?EvolitPalette.SoftAqua:EvolitPalette.EvolutionCyan);_inspector.AddChild(h);
        _inspector.AddChild(Line("Статус",s.Status==DemoSpeciesStatus.Extinct?"Вымерший":"Активный"));
        _inspector.AddChild(Line("Тип",s.Kind switch{DemoSpeciesKind.Plant=>"Растительная линия",DemoSpeciesKind.Creature=>"Подвижная линия",_=>"Исходная форма"}));
        _inspector.AddChild(Line("Появление",$"День {s.DayAppeared}"));_inspector.AddChild(Line("Популяция",s.Population.ToString()));_inspector.AddChild(Line("Адаптивность",$"{s.Adaptability*100:0}%"));
        var parent=_world.Species.FirstOrDefault(x=>x.Id==s.ParentId);if(parent is not null)_inspector.AddChild(Line("Родитель",parent.Name));
        var children=_world.Species.Where(x=>x.ParentId==s.Id).ToList();if(children.Count>0)_inspector.AddChild(Line("Потомки",string.Join(", ",children.Select(x=>x.Name))));
        _inspector.AddChild(new HSeparator());
        var d=new Label{Text=s.Description,AutowrapMode=TextServer.AutowrapMode.WordSmart};d.AddThemeColorOverride("font_color",EvolitPalette.FogBlue);_inspector.AddChild(d);
        if(parent is not null){var b=new Button{Text="Показать родителя"};b.Pressed+=()=>_canvas?.FocusSpecies(parent.Id);_inspector.AddChild(b);}
        if(children.Count>0){var b=new Button{Text="Показать потомков"};b.Pressed+=()=>_canvas?.FocusSpecies(children[0].Id);_inspector.AddChild(b);}
    }

    private void ShowEmptyInspector(){if(_inspector is null)return;var h=new Label{Text="Инспектор таксона"};h.AddThemeFontSizeOverride("font_size",18);h.AddThemeColorOverride("font_color",EvolitPalette.SoftAqua);_inspector.AddChild(h);var t=new Label{Text="Нажмите на узел дерева, чтобы увидеть доступные сведения и родственные связи.",AutowrapMode=TextServer.AutowrapMode.WordSmart};t.AddThemeColorOverride("font_color",EvolitPalette.FogBlue);_inspector.AddChild(t);}
    private static Control Line(string a,string b){var r=new HBoxContainer();var l=new Label{Text=a,SizeFlagsHorizontal=SizeFlags.ExpandFill};l.AddThemeColorOverride("font_color",EvolitPalette.FogBlue);r.AddChild(l);r.AddChild(new Label{Text=b});return r;}
    private static Button ActionButton(string text,string tip,Action action){var b=new Button{Text=text,TooltipText=tip,CustomMinimumSize=new(62,38)};b.Pressed+=action;return b;}
    private void AddFilter(Container p,LineageFilter f,string text){var b=new Button{Text=text,ToggleMode=true,CustomMinimumSize=new(88,34)};b.Pressed+=()=>{_canvas?.SetFilter(f);UpdateFilterButtons(f);};_filters[f]=b;p.AddChild(b);}
    private void UpdateFilterButtons(LineageFilter active){foreach(var p in _filters)p.Value.ButtonPressed=p.Key==active;}
    private static void Clear(Node n){foreach(var c in n.GetChildren()){n.RemoveChild(c);c.QueueFree();}}
}
