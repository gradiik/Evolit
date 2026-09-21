using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI;

public sealed partial class EncyclopediaScreen : Control
{
    public event Action? BackRequested;
    private readonly List<DemoSpeciesRecord> _species=SpeciesDemoData.CreateSpecies();
    private VBoxContainer? _list;
    private Label? _details;
    private LineEdit? _search;
    private string _filter="Все";

    public override void _Ready()
    {
        var background=new MenuBackground();background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(background);
        var outer=new MarginContainer();outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);outer.AddThemeConstantOverride("margin_left",72);outer.AddThemeConstantOverride("margin_right",72);outer.AddThemeConstantOverride("margin_top",54);outer.AddThemeConstantOverride("margin_bottom",54);AddChild(outer);
        var card=new PanelContainer();card.AddThemeStyleboxOverride("panel",NatureTechTheme.CardStyle(.97f));outer.AddChild(card);
        var m=new MarginContainer();m.AddThemeConstantOverride("margin_left",26);m.AddThemeConstantOverride("margin_right",26);m.AddThemeConstantOverride("margin_top",22);m.AddThemeConstantOverride("margin_bottom",22);card.AddChild(m);
        var root=new VBoxContainer();root.AddThemeConstantOverride("separation",12);m.AddChild(root);
        var header=new HBoxContainer();root.AddChild(header);
        var titles=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};header.AddChild(titles);
        var title=new Label{Text="Энциклопедия Evolit"};title.AddThemeFontSizeOverride("font_size",32);titles.AddChild(title);
        var sub=new Label{Text="Каталог известных демо-линий · сведения основаны только на доступных данных мира"};sub.AddThemeColorOverride("font_color",EvolitPalette.FogBlue);titles.AddChild(sub);
        var back=new Button{Text="Назад",Icon=EvolitIcons.Load("actions/back.svg"),CustomMinimumSize=new(110,40)};back.Pressed+=()=>BackRequested?.Invoke();header.AddChild(back);

        var tools=new HBoxContainer();tools.AddThemeConstantOverride("separation",6);root.AddChild(tools);
        foreach(var f in new[]{"Все","Растения","Существа","Вымершие"}){var b=new Button{Text=f,ToggleMode=true,ButtonPressed=f=="Все"};var captured=f;b.Pressed+=()=>{_filter=captured;Refresh();};tools.AddChild(b);}
        tools.AddChild(new Control{SizeFlagsHorizontal=SizeFlags.ExpandFill});
        _search=new LineEdit{PlaceholderText="Поиск по названию…",ClearButtonEnabled=true,CustomMinimumSize=new(250,36)};_search.TextChanged+=_=>Refresh();tools.AddChild(_search);
        root.AddChild(new HSeparator());

        var body=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",12);root.AddChild(body);
        var scroll=new ScrollContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};body.AddChild(scroll);
        _list=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};_list.AddThemeConstantOverride("separation",8);scroll.AddChild(_list);
        var inspector=new PanelContainer{CustomMinimumSize=new(330,0),SizeFlagsVertical=SizeFlags.ExpandFill};inspector.AddThemeStyleboxOverride("panel",NatureTechTheme.SectionStyle());body.AddChild(inspector);
        var im=new MarginContainer();im.AddThemeConstantOverride("margin_left",18);im.AddThemeConstantOverride("margin_right",18);im.AddThemeConstantOverride("margin_top",18);im.AddThemeConstantOverride("margin_bottom",18);inspector.AddChild(im);
        _details=new Label{Text="Выберите запись слева.",AutowrapMode=TextServer.AutowrapMode.WordSmart};_details.AddThemeColorOverride("font_color",EvolitPalette.FogBlue);im.AddChild(_details);
        Refresh();
    }

    public override void _UnhandledInput(InputEvent e){if(e.IsActionPressed("game_pause")){BackRequested?.Invoke();GetViewport().SetInputAsHandled();}}

    private void Refresh()
    {
        if(_list is null)return;foreach(var c in _list.GetChildren()){_list.RemoveChild(c);c.QueueFree();}
        var q=_search?.Text?.Trim()??"";
        var rows=_species.Where(s=>s.Kind!=DemoSpeciesKind.Origin).Where(s=>_filter switch{"Растения"=>s.Kind==DemoSpeciesKind.Plant,"Существа"=>s.Kind==DemoSpeciesKind.Creature,"Вымершие"=>s.Status==DemoSpeciesStatus.Extinct,_=>true}).Where(s=>q.Length==0||s.Name.Contains(q,StringComparison.OrdinalIgnoreCase));
        foreach(var s in rows)_list.AddChild(Card(s));
    }

    private Control Card(DemoSpeciesRecord s)
    {
        var b=new Button{Text=$"{s.Name}\n{s.Description}",Alignment=HorizontalAlignment.Left,CustomMinimumSize=new(0,72),TooltipText="Открыть запись"};
        b.Pressed+=()=>Show(s);return b;
    }

    private void Show(DemoSpeciesRecord s)
    {
        if(_details is null)return;
        var parent=_species.FirstOrDefault(x=>x.Id==s.ParentId);
        _details.Text=$"{s.Name}\n\nТип: {(s.Kind==DemoSpeciesKind.Plant?"Растительная линия":"Подвижная линия")}\nСтатус: {(s.Status==DemoSpeciesStatus.Extinct?"Вымерший":"Активный")}\nПоявление: день {s.DayAppeared}\nПопуляция: {s.Population}\nАдаптивность: {s.Adaptability*100:0}%\nРодитель: {parent?.Name??"—"}\n\n{s.Description}";
        _details.AddThemeColorOverride("font_color",EvolitPalette.MistWhite);
    }
}
