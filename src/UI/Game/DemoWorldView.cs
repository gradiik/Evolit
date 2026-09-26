using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Evolit.Game.Core;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

public readonly record struct GameViewState(
    Vector2 CameraPosition,
    float Zoom,
    string? SelectedEntityId);

public enum GenerationDebugMode
{
    None,
    Elevation,
    WaterDepth,
    FlowAccumulation,
    Temperature,
    Humidity,
    Substrate,
    Minerals,
    Region
}

public readonly record struct WorldRenderDiagnostics(
    int TotalHexes,
    int TotalChunks,
    int VisibleHexes,
    int VisibleChunks,
    int TerrainRebuilds,
    double OverlayRedrawsPerSecond,
    int EstimatedDrawCommands);

public sealed partial class DemoWorldView : Control
{
    public event Action<DemoEntity?>? SelectionChanged;

    private const float MinZoom = 0.035f;
    private const float MaxZoom = 3.0f;
    private const int ChunkHexSpan = 12;
    private const float DetailShowZoom = 0.22f;
    private const float DetailHideZoom = 0.18f;
    private const float FineShowZoom = 0.46f;
    private const float FineHideZoom = 0.38f;

    private static readonly Vector2[] UnitHexPoints =
    [
        new Vector2(0.8660254f, 0.5f),
        new Vector2(0f, 1f),
        new Vector2(-0.8660254f, 0.5f),
        new Vector2(-0.8660254f, -0.5f),
        new Vector2(0f, -1f),
        new Vector2(0.8660254f, -0.5f)
    ];

    private DemoWorldDataProvider? _world;
    private CoreSimulationHost? _core;
    private DemoEntity? _selected;
    private Vector2 _cameraPosition;
    private float _zoom = 0.6f;
    private float _targetZoom = 0.6f;

    private float _cameraSpeed = 5f;
    private bool _smoothZoom = true;
    private GraphicsQualityProfile _quality = GraphicsQualityProfile.From(AppSettings.Default());
    private bool _panning;
    private MouseButton _panButton;
    private Vector2 _zoomAnchorScreen;
    private Vector2 _zoomAnchorWorld;
    private bool _hasZoomAnchor;

    private Control? _worldRoot;
    private EntityOverlayView? _entityOverlay;
    private readonly List<TerrainChunkSet> _chunks = new();
    private bool _showDetails;
    private bool _showFineDetails;
    private int _terrainRebuilds;
    private int _visibleHexes;
    private int _visibleChunks;
    private int _estimatedDrawCommands;
    private int _overlayRedrawsThisSample;
    private double _diagnosticSeconds;
    private double _overlayRedrawsPerSecond;

    private PanelContainer? _inspectorPanel;
    private Label? _inspectorLabel;
    private WorldHexCell? _inspectedCell;
    private bool _inspectorActive;
    private readonly Vector2[] _inspectorPoints = new Vector2[6];
    private readonly Vector2[] _inspectorOutline = new Vector2[7];
    private GenerationDebugMode _generationDebugMode;
    private Label? _generationDebugLabel;

    public void Configure(
        DemoWorldDataProvider world,
        CoreSimulationHost core,
        double cameraSpeed,
        bool smoothZoom,
        GraphicsQualityProfile quality)
    {
        _world = world;
        _core = core;
        _cameraSpeed = (float)Math.Clamp(cameraSpeed, 1, 10);
        _smoothZoom = smoothZoom;
        _quality = quality;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        ClipContents = true;
        Resized += HandleResized;

        BuildWorldLayers();
        BuildInspectorPanel();
        BuildGenerationDebugLabel();

        if (_world is not null)
            _world.DataChanged += HandleWorldDataChanged;

        ApplyFitView();
        UpdateViewTransform(true);
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_world is not null)
            _world.DataChanged -= HandleWorldDataChanged;
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("worldgen_debug"))
        {
            _generationDebugMode = (GenerationDebugMode)(((int)_generationDebugMode + 1) % Enum.GetValues<GenerationDebugMode>().Length);
            if (_generationDebugLabel is not null)
            {
                _generationDebugLabel.Visible = _generationDebugMode != GenerationDebugMode.None;
                _generationDebugLabel.Text = $"F4 · Worldgen: {_generationDebugMode}";
            }
            foreach (var chunk in _chunks)
                chunk.Base.SetDebugMode(_generationDebugMode);
            UpdateChunkVisibility(true);
        }

        _diagnosticSeconds += delta;
        if (_diagnosticSeconds >= 1.0)
        {
            _overlayRedrawsPerSecond = _overlayRedrawsThisSample / _diagnosticSeconds;
            _overlayRedrawsThisSample = 0;
            _diagnosticSeconds = 0;
        }

        var direction = Input.GetVector(
            "camera_left",
            "camera_right",
            "camera_up",
            "camera_down");

        var viewChanged = false;
        if (direction.LengthSquared() > 0.001f)
        {
            _hasZoomAnchor = false;
            var worldUnitsPerSecond = 100f + _cameraSpeed * 48f;
            _cameraPosition += direction
                * worldUnitsPerSecond
                * (float)delta
                / Math.Max(_zoom, 0.1f);
            ClampCamera();
            viewChanged = true;
        }

        if (_smoothZoom)
        {
            var next = Mathf.Lerp(
                _zoom,
                _targetZoom,
                1f - MathF.Exp(-10f * (float)delta));

            if (Math.Abs(next - _zoom) > 0.0005f)
            {
                _zoom = next;
                ApplyZoomAnchor();
                viewChanged = true;
            }
            else if (Math.Abs(_zoom - _targetZoom) <= 0.001f)
            {
                if (Math.Abs(_zoom - _targetZoom) > 0.00001f)
                    viewChanged = true;
                _zoom = _targetZoom;
                ApplyZoomAnchor();
                _hasZoomAnchor = false;
            }
        }
        else if (Math.Abs(_zoom - _targetZoom) > 0.0001f)
        {
            _zoom = _targetZoom;
            ApplyZoomAnchor();
            _hasZoomAnchor = false;
            viewChanged = true;
        }

        if (viewChanged)
            UpdateViewTransform();

        UpdateInspector();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && _panning)
        {
            _cameraPosition -= motion.Relative / Math.Max(_zoom, 0.01f);
            ClampCamera();
            UpdateViewTransform();
            AcceptEvent();
            return;
        }

        if (@event is not InputEventMouseButton mouse)
            return;

        if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed)
        {
            ZoomAt(mouse.Position, 1.14f);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed)
        {
            ZoomAt(mouse.Position, 1f / 1.14f);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex is MouseButton.Middle or MouseButton.Right)
        {
            if (mouse.Pressed)
            {
                _hasZoomAnchor = false;
                _panning = true;
                _panButton = mouse.ButtonIndex;
            }
            else if (_panning && mouse.ButtonIndex == _panButton)
            {
                _panning = false;
            }

            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed)
        {
            var worldPoint = ScreenToWorld(mouse.Position);
            SetSelected(HitTest(worldPoint));
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        DrawRect(
            new Rect2(Vector2.Zero, Size),
            new Color(0.010f, 0.040f, 0.047f),
            true);

        DrawInspectorHighlight();
    }

    public GameViewState CaptureViewState()
    {
        return new GameViewState(
            _cameraPosition,
            _zoom,
            _selected?.Id);
    }

    public void RestoreViewState(GameViewState state)
    {
        _cameraPosition = state.CameraPosition;
        _zoom = Mathf.Clamp(state.Zoom, MinZoom, MaxZoom);
        _targetZoom = _zoom;
        _hasZoomAnchor = false;

        _selected = null;
        if (_world is not null && !string.IsNullOrWhiteSpace(state.SelectedEntityId))
            _selected = _world.Entities.FirstOrDefault(entity => entity.Id == state.SelectedEntityId);

        ClampCamera();
        UpdateViewTransform(true);
        SelectionChanged?.Invoke(_selected);
    }

    public WorldRenderDiagnostics GetDiagnostics()
    {
        return new WorldRenderDiagnostics(
            _world?.Map.Cells.Count ?? 0,
            _chunks.Count,
            _visibleHexes,
            _visibleChunks,
            _terrainRebuilds,
            _overlayRedrawsPerSecond,
            _estimatedDrawCommands);
    }

    private void BuildWorldLayers()
    {
        if (_world is null)
            return;

        _worldRoot = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None
        };
        AddChild(_worldRoot);

        var grouped = new Dictionary<ChunkKey, List<WorldHexCell>>();
        foreach (var cell in _world.Map.Cells)
        {
            var key = new ChunkKey(
                FloorDiv(cell.Coord.Q, ChunkHexSpan),
                FloorDiv(cell.Coord.R, ChunkHexSpan));
            if (!grouped.TryGetValue(key, out var cells))
            {
                cells = new List<WorldHexCell>(ChunkHexSpan * ChunkHexSpan);
                grouped[key] = cells;
            }
            cells.Add(cell);
        }

        foreach (var pair in grouped)
        {
            var bounds = ComputeChunkBounds(pair.Value, _world.Map.HexSize);
            var cells = pair.Value.ToArray();
            var baseLayer = new TerrainChunkLayer();
            var detailLayer = new TerrainChunkLayer();
            var fineLayer = new TerrainChunkLayer();
            baseLayer.Configure(cells, bounds, _world.Map.HexSize, _quality, TerrainLayerKind.Base);
            detailLayer.Configure(cells, bounds, _world.Map.HexSize, _quality, TerrainLayerKind.Detail);
            fineLayer.Configure(cells, bounds, _world.Map.HexSize, _quality, TerrainLayerKind.Fine);

            _worldRoot.AddChild(baseLayer);
            _worldRoot.AddChild(detailLayer);
            _worldRoot.AddChild(fineLayer);

            _chunks.Add(new TerrainChunkSet(
                bounds,
                cells.Length,
                baseLayer,
                detailLayer,
                fineLayer));
        }

        _terrainRebuilds++;

        _entityOverlay = new EntityOverlayView
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _entityOverlay.Configure(_world, _quality);
        _entityOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_entityOverlay);
    }

    private void UpdateViewTransform(bool forceVisibility = false)
    {
        if (_worldRoot is null)
            return;

        _worldRoot.Position = Size * 0.5f - _cameraPosition * _zoom;
        _worldRoot.Scale = Vector2.One * _zoom;

        var oldDetails = _showDetails;
        var oldFine = _showFineDetails;
        if (_showDetails)
            _showDetails = _zoom > DetailHideZoom;
        else
            _showDetails = _zoom >= DetailShowZoom;

        if (_showFineDetails)
            _showFineDetails = _zoom > FineHideZoom;
        else
            _showFineDetails = _zoom >= FineShowZoom;

        UpdateChunkVisibility(forceVisibility || oldDetails != _showDetails || oldFine != _showFineDetails);
        _entityOverlay?.SetView(_cameraPosition, _zoom, Size, _selected);
        _overlayRedrawsThisSample++;

        if (_inspectorActive || _generationDebugMode != GenerationDebugMode.None)
            QueueRedraw();
    }

    private void UpdateChunkVisibility(bool force = false)
    {
        if (_world is null || _chunks.Count == 0)
            return;

        var padding = _world.Map.HexSize * 2.5f;
        var topLeft = ScreenToWorld(new Vector2(-padding, -padding));
        var bottomRight = ScreenToWorld(Size + new Vector2(padding, padding));
        var viewport = new Rect2(
            new Vector2(Math.Min(topLeft.X, bottomRight.X), Math.Min(topLeft.Y, bottomRight.Y)),
            new Vector2(Math.Abs(bottomRight.X - topLeft.X), Math.Abs(bottomRight.Y - topLeft.Y)));

        var visibleChunks = 0;
        var visibleHexes = 0;
        var estimatedCommands = 0;

        foreach (var chunk in _chunks)
        {
            var visible = chunk.Bounds.Intersects(viewport, true);
            if (force || chunk.Base.Visible != visible)
                chunk.Base.Visible = visible;

            var debugActive = _generationDebugMode != GenerationDebugMode.None;
            var detailVisible = visible && !debugActive && _showDetails && _quality.DetailLevel > 0;
            var fineVisible = visible && !debugActive && _showFineDetails && _quality.DetailLevel > 0;
            if (force || chunk.Detail.Visible != detailVisible)
                chunk.Detail.Visible = detailVisible;
            if (force || chunk.Fine.Visible != fineVisible)
                chunk.Fine.Visible = fineVisible;

            if (!visible)
                continue;

            visibleChunks++;
            visibleHexes += chunk.CellCount;
            estimatedCommands += chunk.Base.EstimatedCommands;
            if (detailVisible)
                estimatedCommands += chunk.Detail.EstimatedCommands;
            if (fineVisible)
                estimatedCommands += chunk.Fine.EstimatedCommands;
        }

        _visibleChunks = visibleChunks;
        _visibleHexes = visibleHexes;
        _estimatedDrawCommands = estimatedCommands;
    }

    private void HandleWorldDataChanged()
    {
        _entityOverlay?.Refresh(_selected);
        _overlayRedrawsThisSample++;
    }


    private void BuildGenerationDebugLabel()
    {
        _generationDebugLabel = new Label
        {
            Visible = false,
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 14
        };
        _generationDebugLabel.Position = new Vector2(16, 16);
        _generationDebugLabel.AddThemeFontSizeOverride("font_size", UiMetrics.Font(13));
        _generationDebugLabel.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        AddChild(_generationDebugLabel);
    }

    private void BuildInspectorPanel()
    {
        _inspectorPanel = new PanelContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 15,
            CustomMinimumSize = UiMetrics.Size(268, 0)
        };
        _inspectorPanel.AddThemeStyleboxOverride(
            "panel",
            NatureTechTheme.CardStyle(0.98f));
        AddChild(_inspectorPanel);

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 9);
        margin.AddThemeConstantOverride("margin_bottom", 9);
        _inspectorPanel.AddChild(margin);

        _inspectorLabel = new Label
        {
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _inspectorLabel.AddThemeFontSizeOverride("font_size", UiMetrics.Font(12));
        _inspectorLabel.AddThemeColorOverride(
            "font_color",
            EvolitPalette.MistWhite);
        margin.AddChild(_inspectorLabel);
    }

    private void UpdateInspector()
    {
        if (_world is null || _inspectorPanel is null)
            return;

        var active = Input.IsActionPressed("terrain_inspect");
        var mouse = GetLocalMousePosition();
        var inside = new Rect2(Vector2.Zero, Size).HasPoint(mouse);
        var nextCell = active && inside
            ? _world.Map.GetCellAtWorld(ScreenToWorld(mouse))
            : null;

        if (!active || nextCell is null)
        {
            if (_inspectorActive || _inspectedCell is not null)
            {
                _inspectorActive = false;
                _inspectedCell = null;
                _inspectorPanel.Visible = false;
                QueueRedraw();
            }
            return;
        }

        _inspectorActive = true;
        _inspectorPanel.Visible = true;
        var panelWidth = Math.Max(284f, _inspectorPanel.Size.X);
        var panelHeight = Math.Max(260f, _inspectorPanel.Size.Y);
        _inspectorPanel.Position = new Vector2(
            Math.Max(8f, Math.Min(Size.X - panelWidth - 8f, mouse.X + 18f)),
            Math.Max(8f, Math.Min(Size.Y - panelHeight - 8f, mouse.Y + 18f)));

        var changedCell = !ReferenceEquals(_inspectedCell, nextCell);
        _inspectedCell = nextCell;
        if (_inspectorLabel is not null)
            _inspectorLabel.Text = FormatInspectorText(nextCell);
        if (changedCell)
            QueueRedraw();
    }

    private void DrawInspectorHighlight()
    {
        if (!_inspectorActive || _inspectedCell is null || _world is null)
            return;

        var center = WorldToScreen(_inspectedCell.WorldCenter);
        var radius = _world.Map.HexSize * _zoom;
        if (radius <= 0f)
            return;

        FillHexPoints(center, radius * 0.96f, _inspectorPoints);
        DrawColoredPolygon(
            _inspectorPoints,
            new Color(EvolitPalette.EvolutionCyan, 0.08f));

        for (var index = 0; index < 6; index++)
            _inspectorOutline[index] = _inspectorPoints[index];
        _inspectorOutline[6] = _inspectorPoints[0];

        DrawPolyline(
            _inspectorOutline,
            EvolitPalette.EvolutionCyan,
            Math.Max(1.5f, 2.2f * _zoom),
            true);
    }

    private string FormatInspectorText(WorldHexCell cell)
    {
        var location = $"Гекс {cell.Coord.Q}:{cell.Coord.R}";
        var terrain = $"Рельеф: {TerrainLabel(cell.Terrain)}";
        if (_core is null || !_core.TryGetEnvironment(cell.Coord.Q, cell.Coord.R, out var env, out var physical))
            return $"{location}\n{terrain}\nCore environment: недоступен";

        var vertical = env.WaterDepthMeters > 0.01f
            ? $"Вода: {env.WaterDepthMeters:0.0} м"
            : $"Высота: {env.ElevationMeters:0} м";
        var wind = MathF.Sqrt(physical.WindX * physical.WindX + physical.WindY * physical.WindY);

        return
            $"{location}\n{terrain}\n{vertical}\n" +
            $"Климат: {env.TemperatureCelsius:0.0} °C · {env.Humidity * 100f:0}% · {env.PressureKPa:0.0} кПа\n" +
            $"Ветер: {wind:0.000} ({physical.WindX:0.000}, {physical.WindY:0.000})\n" +
            $"Свет: {env.LightAvailability * 100f:0}% · вода {physical.WaterAvailability * 100f:0}%\n" +
            $"Минералы: {env.MineralPotential * 100f:0}% · nutrients {env.NutrientPotential * 100f:0}%\n" +
            $"Органика: {env.OrganicMatter:0.000} · substrate dev {env.SubstrateDevelopment * 100f:0}%\n" +
            $"Субстрат: {env.Substrate} · регион: {physical.Region}\n" +
            $"Сток: {cell.FlowAccumulation:0.0} · уклон {cell.Slope:0.0} м\n" +
            $"Geo: {cell.GeothermalPotential * 100f:0}% · mineral seed {cell.MineralPotential * 100f:0}%";
    }

    private static float WaterDepthMeters(WorldHexCell cell)
    {
        return cell.Terrain switch
        {
            HexTerrainType.River => 2.5f + cell.WaterDepth * 18f,
            HexTerrainType.Lake => 6f + cell.WaterDepth * 120f,
            _ => cell.WaterDepth * 850f
        };
    }

    private static string TerrainLabel(HexTerrainType terrain)
    {
        return terrain switch
        {
            HexTerrainType.DeepWater => "Глубокий океан",
            HexTerrainType.ShallowWater => "Мелководье",
            HexTerrainType.Lake => "Озеро",
            HexTerrainType.River => "Река",
            HexTerrainType.Sand => "Песчаный берег",
            HexTerrainType.Desert => "Пустыня",
            HexTerrainType.Grassland => "Равнина",
            HexTerrainType.Rocky => "Каменистая возвышенность",
            HexTerrainType.Mountain => "Горы",
            _ => terrain.ToString()
        };
    }

    private static void FillHexPoints(Vector2 center, float radius, Vector2[] target)
    {
        for (var index = 0; index < 6; index++)
            target[index] = center + UnitHexPoints[index] * radius;
    }

    private void ZoomAt(Vector2 screenPoint, float factor)
    {
        _zoomAnchorScreen = screenPoint;
        _zoomAnchorWorld = ScreenToWorld(screenPoint);
        _hasZoomAnchor = true;
        _targetZoom = Mathf.Clamp(
            _targetZoom * factor,
            MinZoom,
            MaxZoom);

        if (!_smoothZoom)
        {
            _zoom = _targetZoom;
            ApplyZoomAnchor();
            _hasZoomAnchor = false;
            UpdateViewTransform();
        }
    }

    private void ApplyZoomAnchor()
    {
        if (!_hasZoomAnchor)
            return;

        _cameraPosition = _zoomAnchorWorld
            - (_zoomAnchorScreen - Size * 0.5f)
            / Math.Max(_zoom, 0.01f);

        ClampCamera();
    }

    public void ZoomIn()
    {
        ZoomAt(Size * 0.5f, 1.14f);
    }

    public void ZoomOut()
    {
        ZoomAt(Size * 0.5f, 1f / 1.14f);
    }

    public void CenterCamera()
    {
        if (_world is null)
            return;

        _cameraPosition = _world.Map.Bounds.GetCenter();
        _hasZoomAnchor = false;
        UpdateViewTransform();
    }

    public void ResetCamera()
    {
        ApplyFitView();
        UpdateViewTransform(true);
    }

    private void ApplyFitView()
    {
        if (_world is null || Size.X <= 1 || Size.Y <= 1)
            return;

        var bounds = _world.Map.Bounds;
        var availableWidth = Math.Max(1f, Size.X * 0.92f);
        var availableHeight = Math.Max(1f, Size.Y * 0.72f);
        var fit = Math.Min(
            availableWidth / Math.Max(1f, bounds.Size.X),
            availableHeight / Math.Max(1f, bounds.Size.Y));

        _cameraPosition = bounds.GetCenter();
        _zoom = Mathf.Clamp(fit, MinZoom, 1.05f);
        _targetZoom = _zoom;
        _hasZoomAnchor = false;
        ClampCamera();
    }

    private DemoEntity? HitTest(Vector2 worldPoint)
    {
        if (_world is null)
            return null;

        DemoEntity? best = null;
        var bestDistance = float.MaxValue;
        var radius = 48f / Math.Max(_zoom, 0.4f);

        foreach (var entity in _world.Entities)
        {
            var distance = worldPoint.DistanceTo(entity.WorldPosition);
            if (distance <= radius && distance < bestDistance)
            {
                best = entity;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void SetSelected(DemoEntity? entity)
    {
        if (ReferenceEquals(_selected, entity))
            return;

        _selected = entity;
        _entityOverlay?.Refresh(_selected);
        _overlayRedrawsThisSample++;
        SelectionChanged?.Invoke(entity);
    }

    private Vector2 ScreenToWorld(Vector2 screen)
    {
        return (screen - Size * 0.5f)
            / Math.Max(_zoom, 0.01f)
            + _cameraPosition;
    }

    private Vector2 WorldToScreen(Vector2 world)
    {
        return (world - _cameraPosition) * _zoom + Size * 0.5f;
    }

    private void ClampCamera()
    {
        if (_world is null)
            return;

        var bounds = _world.Map.Bounds;
        var padding = _world.Map.HexSize * 5f;

        _cameraPosition.X = Mathf.Clamp(
            _cameraPosition.X,
            bounds.Position.X - padding,
            bounds.End.X + padding);
        _cameraPosition.Y = Mathf.Clamp(
            _cameraPosition.Y,
            bounds.Position.Y - padding,
            bounds.End.Y + padding);
    }

    private void HandleResized()
    {
        ClampCamera();
        UpdateViewTransform(true);
        QueueRedraw();
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static Rect2 ComputeChunkBounds(IReadOnlyList<WorldHexCell> cells, float hexSize)
    {
        var horizontal = 0.8660254f * hexSize;
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        foreach (var cell in cells)
        {
            minX = Math.Min(minX, cell.WorldCenter.X - horizontal);
            maxX = Math.Max(maxX, cell.WorldCenter.X + horizontal);
            minY = Math.Min(minY, cell.WorldCenter.Y - hexSize);
            maxY = Math.Max(maxY, cell.WorldCenter.Y + hexSize);
        }

        return new Rect2(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
    }

    private readonly record struct ChunkKey(int Q, int R);
}
