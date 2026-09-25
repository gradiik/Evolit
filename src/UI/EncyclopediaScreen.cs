using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI;

public sealed partial class EncyclopediaScreen : Control
{
    public event Action? BackRequested;

    private readonly List<DemoSpeciesRecord> _species = SpeciesDemoData.CreateSpecies();
    private readonly Dictionary<string, Button> _filters = new();
    private readonly Dictionary<string, Button> _cards = new();
    private GridContainer? _grid;
    private PanelContainer? _inspector;
    private VBoxContainer? _inspectorContent;
    private LineEdit? _search;
    private Button? _labButton;
    private Button? _rerollButton;
    private Label? _resultCount;
    private string _filter = "Все";
    private string? _selectedId;
    private bool _labMode;
    private uint _previewGeneration = 1;

    public override void _Ready()
    {
        Build();
        Resized += UpdateColumns;
        Refresh();
        UpdateColumns();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("game_pause"))
        {
            BackRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Build()
    {
        var background = new MenuBackground { Name = "MenuBackground" };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left", 52);
        outer.AddThemeConstantOverride("margin_right", 52);
        outer.AddThemeConstantOverride("margin_top", 42);
        outer.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(outer);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.97f));
        outer.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(titles);

        var title = new Label { Text = "Энциклопедия Evolit" };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(32));
        titles.AddChild(title);

        var subtitle = new Label
        {
            Text = "Каталог известных линий · портреты помогают различать родственные формы."
        };
        subtitle.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        subtitle.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        titles.AddChild(subtitle);

        var back = new Button
        {
            Text = "Назад",
            Icon = EvolitIcons.Load("actions/back.svg"),
            CustomMinimumSize = UiMetrics.Size(110, 40)
        };
        back.Pressed += () => BackRequested?.Invoke();
        header.AddChild(back);

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 6);
        root.AddChild(tools);
        AddFilter(tools, "Все");
        AddFilter(tools, "Растения");
        AddFilter(tools, "Существа");
        AddFilter(tools, "Вымершие");

        _labButton = new Button
        {
            Text = "Лаборатория форм",
            ToggleMode = true,
            TooltipText = "Тестовая выборка процедурных портретов без добавления видов в мир.",
            CustomMinimumSize = UiMetrics.Size(150, 34)
        };
        _labButton.Pressed += () =>
        {
            _labMode = _labButton.ButtonPressed;
            UpdateToolVisibility();
            Refresh();
        };
        tools.AddChild(_labButton);

        _rerollButton = new Button
        {
            Text = "Новая выборка",
            Visible = false,
            TooltipText = "Создать новую тестовую комбинацию visual seeds.",
            CustomMinimumSize = UiMetrics.Size(122, 34)
        };
        _rerollButton.Pressed += () =>
        {
            _previewGeneration++;
            Refresh();
        };
        tools.AddChild(_rerollButton);

        tools.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _resultCount = new Label { Text = "0 записей", VerticalAlignment = VerticalAlignment.Center };
        _resultCount.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        _resultCount.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        tools.AddChild(_resultCount);

        _search = new LineEdit
        {
            PlaceholderText = "Поиск по названию…",
            ClearButtonEnabled = true,
            CustomMinimumSize = UiMetrics.Size(260, 36)
        };
        _search.TextChanged += _ => Refresh();
        tools.AddChild(_search);
        root.AddChild(new HSeparator());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var catalog = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        catalog.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(catalog);

        var catalogMargin = new MarginContainer();
        catalogMargin.AddThemeConstantOverride("margin_left", 12);
        catalogMargin.AddThemeConstantOverride("margin_right", 12);
        catalogMargin.AddThemeConstantOverride("margin_top", 12);
        catalogMargin.AddThemeConstantOverride("margin_bottom", 12);
        catalog.AddChild(catalogMargin);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        catalogMargin.AddChild(scroll);

        _grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 9);
        _grid.AddThemeConstantOverride("v_separation", 9);
        scroll.AddChild(_grid);

        _inspector = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(342, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _inspector.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        body.AddChild(_inspector);

        var inspectorMargin = new MarginContainer();
        inspectorMargin.AddThemeConstantOverride("margin_left", 18);
        inspectorMargin.AddThemeConstantOverride("margin_right", 18);
        inspectorMargin.AddThemeConstantOverride("margin_top", 18);
        inspectorMargin.AddThemeConstantOverride("margin_bottom", 18);
        _inspector.AddChild(inspectorMargin);

        var inspectorScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        inspectorMargin.AddChild(inspectorScroll);

        _inspectorContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inspectorContent.AddThemeConstantOverride("separation", 9);
        inspectorScroll.AddChild(_inspectorContent);
        UpdateFilterButtons();
        ShowEmptyInspector();
    }

    private void AddFilter(Container parent, string name)
    {
        var button = new Button
        {
            Text = name,
            ToggleMode = true,
            CustomMinimumSize = UiMetrics.Size(98, 34)
        };
        button.Pressed += () =>
        {
            _filter = name;
            UpdateFilterButtons();
            Refresh();
        };
        _filters[name] = button;
        parent.AddChild(button);
    }

    private void UpdateFilterButtons()
    {
        foreach (var pair in _filters)
            pair.Value.ButtonPressed = pair.Key == _filter;
    }

    private void Refresh()
    {
        if (_grid is null)
            return;

        ClearChildren(_grid);
        _cards.Clear();

        if (_labMode)
        {
            BuildPreviewLab();
            ShowLabInspector();
            UpdateColumns();
            return;
        }

        var query = _search?.Text.Trim() ?? string.Empty;
        var rows = _species
            .Where(species => species.Kind != DemoSpeciesKind.Origin)
            .Where(MatchesFilter)
            .Where(species => query.Length == 0 || species.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(species => species.Kind)
            .ThenBy(species => species.Name)
            .ToList();

        if (_resultCount is not null)
            _resultCount.Text = $"{rows.Count} записей";

        if (rows.Count == 0)
        {
            _grid.AddChild(EmptyState("Ничего не найдено", "Измени категорию или очисти поисковый запрос."));
            ClearSelection();
            UpdateColumns();
            return;
        }

        foreach (var species in rows)
            _grid.AddChild(BuildCard(species));

        var selected = _selectedId is null
            ? null
            : rows.FirstOrDefault(species => species.Id == _selectedId);

        if (selected is null)
        {
            _selectedId = rows[0].Id;
            selected = rows[0];
        }

        foreach (var pair in _cards)
            pair.Value.ButtonPressed = pair.Key == _selectedId;

        ShowInspector(selected!);
        UpdateColumns();
    }

    private bool MatchesFilter(DemoSpeciesRecord species)
    {
        return _filter switch
        {
            "Растения" => species.Kind == DemoSpeciesKind.Plant,
            "Существа" => species.Kind == DemoSpeciesKind.Creature,
            "Вымершие" => species.Status == DemoSpeciesStatus.Extinct,
            _ => true
        };
    }

    private Control BuildCard(DemoSpeciesRecord species)
    {
        var button = new Button
        {
            Text = string.Empty,
            ToggleMode = true,
            ButtonPressed = species.Id == _selectedId,
            CustomMinimumSize = UiMetrics.Size(250, 116),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = $"Открыть запись: {species.Name}"
        };
        button.Pressed += () => SelectSpecies(species);
        UiMotion.BindButton(button);
        _cards[species.Id] = button;

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        button.AddChild(margin);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 11);
        margin.AddChild(row);

        var portrait = new SpeciesPortrait
        {
            CustomMinimumSize = UiMetrics.Size(70, 70),
            MouseFilter = MouseFilterEnum.Ignore
        };
        portrait.Configure(species, _species);
        row.AddChild(portrait);

        var text = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        text.AddThemeConstantOverride("separation", 3);
        row.AddChild(text);

        var name = new Label { Text = species.Name, MouseFilter = MouseFilterEnum.Ignore };
        name.AddThemeFontSizeOverride("font_size", UiMetrics.Font(17));
        text.AddChild(name);

        var type = new Label
        {
            Text = species.Kind == DemoSpeciesKind.Plant ? "Растительная линия" : "Подвижная линия",
            MouseFilter = MouseFilterEnum.Ignore
        };
        type.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        type.AddThemeColorOverride("font_color", species.Kind == DemoSpeciesKind.Plant ? EvolitPalette.YoungLeaf : EvolitPalette.SoftAqua);
        text.AddChild(type);

        var status = new Label
        {
            Text = species.Status == DemoSpeciesStatus.Extinct ? "ВЫМЕРШИЙ" : "АКТИВНЫЙ",
            MouseFilter = MouseFilterEnum.Ignore
        };
        status.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        status.AddThemeColorOverride("font_color", species.Status == DemoSpeciesStatus.Extinct ? EvolitPalette.WarmAlert : EvolitPalette.FogBlue);
        text.AddChild(status);

        var population = new Label { Text = $"Популяция  {species.Population}", MouseFilter = MouseFilterEnum.Ignore };
        population.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        population.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        text.AddChild(population);

        var adaptability = new Label { Text = $"Адаптивность  {species.Adaptability * 100:0}%", MouseFilter = MouseFilterEnum.Ignore };
        adaptability.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        adaptability.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        text.AddChild(adaptability);
        return button;
    }

    private void SelectSpecies(DemoSpeciesRecord species)
    {
        _selectedId = species.Id;
        foreach (var pair in _cards)
            pair.Value.ButtonPressed = pair.Key == _selectedId;
        ShowInspector(species);
    }

    private void ClearSelection()
    {
        _selectedId = null;
        foreach (var button in _cards.Values)
            button.ButtonPressed = false;
        ShowEmptyInspector();
    }

    private void ShowInspector(DemoSpeciesRecord species)
    {
        if (_inspector is null || _inspectorContent is null)
            return;

        foreach (var child in _inspectorContent.GetChildren())
        {
            _inspectorContent.RemoveChild(child);
            child.QueueFree();
        }

        _inspector.Visible = true;
        var portrait = new SpeciesPortrait
        {
            CustomMinimumSize = UiMetrics.Size(0, 126),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        portrait.Configure(species, _species);
        _inspectorContent.AddChild(portrait);

        var title = new Label { Text = species.Name };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(23));
        _inspectorContent.AddChild(title);

        var accent = species.Kind == DemoSpeciesKind.Plant ? EvolitPalette.YoungLeaf : EvolitPalette.SoftAqua;
        var type = new Label { Text = species.Kind == DemoSpeciesKind.Plant ? "РАСТИТЕЛЬНАЯ ЛИНИЯ" : "ПОДВИЖНАЯ ЛИНИЯ" };
        type.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        type.AddThemeColorOverride("font_color", accent);
        _inspectorContent.AddChild(type);
        _inspectorContent.AddChild(new HSeparator());
        _inspectorContent.AddChild(Detail("Статус", species.Status == DemoSpeciesStatus.Extinct ? "Вымерший" : "Активный"));
        _inspectorContent.AddChild(Detail("Появление", $"День {species.DayAppeared}"));
        _inspectorContent.AddChild(Detail("Популяция", species.Population.ToString()));
        _inspectorContent.AddChild(Detail("Адаптивность", $"{species.Adaptability * 100:0}%"));

        var parent = _species.FirstOrDefault(item => item.Id == species.ParentId);
        _inspectorContent.AddChild(Detail("Родитель", parent?.Name ?? "—"));

        var children = _species.Where(item => item.ParentId == species.Id).Select(item => item.Name).ToArray();
        if (children.Length > 0)
            _inspectorContent.AddChild(Detail("Потомки", string.Join(", ", children), true));

        _inspectorContent.AddChild(new HSeparator());
        var description = new Label
        {
            Text = species.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        _inspectorContent.AddChild(description);
    }

    private void UpdateToolVisibility()
    {
        foreach (var button in _filters.Values)
            button.Visible = !_labMode;

        if (_search is not null)
            _search.Visible = !_labMode;
        if (_rerollButton is not null)
            _rerollButton.Visible = _labMode;
        if (_resultCount is not null)
            _resultCount.Visible = !_labMode;
    }

    private void ShowEmptyInspector()
    {
        if (_inspectorContent is null)
            return;

        ClearChildren(_inspectorContent);

        var title = new Label { Text = "Карточка линии" };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(20));
        title.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        _inspectorContent.AddChild(title);

        var text = new Label
        {
            Text = "Выбери запись в каталоге. Здесь появятся реальные доступные сведения о линии и её родственных связях.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        text.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        _inspectorContent.AddChild(text);
    }

    private void BuildPreviewLab()
    {
        if (_grid is null)
            return;

        const int count = 12;
        for (var i = 0; i < count; i++)
        {
            var kind = i < count / 2 ? DemoSpeciesKind.Creature : DemoSpeciesKind.Plant;
            var familyIndex = i % 3;
            var familySeed = SpeciesVisualFactory.StableSeed($"preview-family:{_previewGeneration}:{kind}:{familyIndex}");
            var variantSeed = SpeciesVisualFactory.StableSeed($"preview-variant:{_previewGeneration}:{kind}:{i}");
            _grid.AddChild(BuildPreviewCard(
                kind,
                familyIndex + 1,
                i + 1,
                SpeciesVisualFactory.Preview(kind, familySeed, variantSeed)));
        }
    }

    private Control BuildPreviewCard(
        DemoSpeciesKind kind,
        int familyIndex,
        int index,
        SpeciesVisualDescriptor descriptor)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(220, 132),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.70f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var portrait = new SpeciesPortrait { CustomMinimumSize = UiMetrics.Size(82, 82) };
        portrait.Configure(descriptor);
        root.AddChild(portrait);

        var meta = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(meta);

        var title = new Label
        {
            Text = kind == DemoSpeciesKind.Creature
                ? $"Тест-существо {index}"
                : $"Тест-растение {index}"
        };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(15));
        meta.AddChild(title);

        var family = new Label { Text = $"Семейство {familyIndex}" };
        family.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        family.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        meta.AddChild(family);

        var note = new Label
        {
            Text = "Только visual seed. Запись не добавлена в мир.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", UiMetrics.Font(10));
        note.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.72f));
        meta.AddChild(note);

        return panel;
    }

    private void ShowLabInspector()
    {
        if (_inspectorContent is null)
            return;

        ClearChildren(_inspectorContent);

        var title = new Label { Text = "Лаборатория форм" };
        title.AddThemeFontSizeOverride("font_size", UiMetrics.Font(21));
        title.AddThemeColorOverride("font_color", EvolitPalette.EvolutionCyan);
        _inspectorContent.AddChild(title);

        var text = new Label
        {
            Text = "Этот режим только для визуального аудита генератора. Он показывает разные толщины, длины, отростки, кроны и семейные силуэты. Тестовые формы не являются видами и не записываются в симуляцию.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        text.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        _inspectorContent.AddChild(text);

        _inspectorContent.AddChild(new HSeparator());

        var hint = new Label
        {
            Text = "Нажми «Новая выборка», чтобы получить другой набор seeds. Формы одного семейства должны сохранять общий визуальный характер.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        hint.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        _inspectorContent.AddChild(hint);
    }

    private static Control EmptyState(string title, string text)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = UiMetrics.Size(320, 110),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.55f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", UiMetrics.Font(17));
        root.AddChild(heading);

        var description = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        root.AddChild(description);
        return panel;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static Control Detail(string caption, string value, bool wrap = false)
    {
        var row = new HBoxContainer();
        var name = new Label { Text = caption, CustomMinimumSize = UiMetrics.Size(104, 0) };
        name.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        name.AddThemeColorOverride("font_color", EvolitPalette.FogBlue);
        row.AddChild(name);

        var data = new Label
        {
            Text = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off
        };
        data.AddThemeFontSizeOverride("font_size", UiMetrics.Font(11));
        data.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        row.AddChild(data);
        return row;
    }

    private void UpdateColumns()
    {
        if (_grid is null)
            return;

        _grid.Columns = _labMode
            ? (Size.X >= 1500 ? 3 : 2)
            : (Size.X >= 1480 ? 3 : Size.X >= 1080 ? 2 : 1);
    }
}
