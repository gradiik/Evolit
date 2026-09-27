using System;
using Evolit.Core;
using Evolit.Game;
using Evolit.Game.Core;
using Evolit.Settings;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class PlanetWorldView : Control
{
    public event Action<DemoEntity?>? SelectionChanged;

    private const float PlanetRadius = 3.0f;
    private const float MinDistance = 4.25f;
    private const float MaxDistance = 12.5f;
    private const float GridVisibleDistance = 6.6f;
    private const float EntityVisibleDistance = 7.0f;
    private const float RiverVisibleDistance = 8.4f;

    private DemoWorldDataProvider? _world;
    private CoreSimulationHost? _core;
    private float _cameraSpeed = 5f;
    private bool _smoothZoom = true;
    private GraphicsQualityProfile _quality = GraphicsQualityProfile.From(AppSettings.Default());

    private Node3D? _sceneRoot;
    private Camera3D? _camera;
    private MeshInstance3D? _terrain;
    private MeshInstance3D? _water;
    private MeshInstance3D? _river;
    private MeshInstance3D? _grid;
    private MeshInstance3D? _selection;
    private MeshInstance3D? _entityMarkers;
    private Label? _debugLabel;
    private PanelContainer? _inspectorPanel;
    private Label? _inspectorLabel;

    private Vector3 _orbit = new(-0.24f, 0.72f, 0f);
    private float _distance = 7.2f;
    private float _targetDistance = 7.2f;
    private bool _dragging;
    private MouseButton _dragButton;
    private bool _dragMoved;
    private Vector2 _dragStart;

    private PlanetWorldCell? _selectedCell;
    private DemoEntity? _selectedEntity;
    private GenerationDebugMode _debugMode;
    private int _terrainRebuilds;
    private int _estimatedDrawCommands;
    private int _terrainNodeCount;

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

        BuildScene();
        BuildInspector();
        BuildDebugLabel();
        RebuildTerrainMesh();
        RebuildWaterMesh();
        RebuildRiverMesh();
        RebuildGridMesh();
        RebuildEntityMarkers();
        UpdateCamera(true);
    }

    public override void _Process(double delta)
    {
        if (Godot.Input.IsActionJustPressed("worldgen_debug"))
        {
            _debugMode = (GenerationDebugMode)(((int)_debugMode + 1) % Enum.GetValues<GenerationDebugMode>().Length);
            if (_debugLabel is not null)
            {
                _debugLabel.Visible = _debugMode != GenerationDebugMode.None;
                _debugLabel.Text = $"F4 · Planet worldgen: {_debugMode}";
            }
            RebuildTerrainMesh();
            var normalView = _debugMode == GenerationDebugMode.None;
            if (_water is not null)
                _water.Visible = normalView;
            if (_river is not null)
                _river.Visible = normalView && _distance <= RiverVisibleDistance;
            if (_entityMarkers is not null)
                _entityMarkers.Visible = normalView && _distance <= EntityVisibleDistance;
            UpdateEstimatedDrawCommands();
        }

        var input = Godot.Input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
        if (input.LengthSquared() > 0.001f)
        {
            var speed = 0.32f + _cameraSpeed * 0.045f;
            _orbit.Y -= input.X * speed * (float)delta;
            _orbit.X = Mathf.Clamp(_orbit.X - input.Y * speed * (float)delta, -1.45f, 1.45f);
            UpdateCamera();
        }

        if (_smoothZoom)
        {
            var next = Mathf.Lerp(_distance, _targetDistance, 1f - MathF.Exp(-10f * (float)delta));
            if (Math.Abs(next - _distance) > 0.0005f)
            {
                _distance = next;
                UpdateCamera();
            }
            else
            {
                _distance = _targetDistance;
            }
        }
        else if (Math.Abs(_distance - _targetDistance) > 0.0001f)
        {
            _distance = _targetDistance;
            UpdateCamera();
        }

        UpdateInspector();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && _dragging)
        {
            var delta = motion.Position - _dragStart;
            if (!_dragMoved && delta.LengthSquared() > 16f)
                _dragMoved = true;

            if (_dragMoved)
            {
                var sensitivity = 0.0035f + _cameraSpeed * 0.00035f;
                _orbit.Y -= motion.Relative.X * sensitivity;
                _orbit.X = Mathf.Clamp(_orbit.X - motion.Relative.Y * sensitivity, -1.45f, 1.45f);
                UpdateCamera();
            }
            AcceptEvent();
            return;
        }

        if (@event is not InputEventMouseButton mouse)
            return;

        if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed)
        {
            ZoomIn();
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed)
        {
            ZoomOut();
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex is MouseButton.Left or MouseButton.Middle or MouseButton.Right)
        {
            if (mouse.Pressed)
            {
                _dragging = true;
                _dragButton = mouse.ButtonIndex;
                _dragMoved = false;
                _dragStart = mouse.Position;
            }
            else if (_dragging && mouse.ButtonIndex == _dragButton)
            {
                var wasClick = !_dragMoved && mouse.ButtonIndex == MouseButton.Left;
                _dragging = false;
                if (wasClick)
                    SelectAt(mouse.Position);
            }

            AcceptEvent();
        }
    }

    public void ZoomIn() => SetTargetDistance(_targetDistance * 0.86f);
    public void ZoomOut() => SetTargetDistance(_targetDistance / 0.86f);

    public void CenterCamera()
    {
        if (_selectedCell is null)
        {
            _orbit = new Vector3(-0.24f, 0.72f, 0f);
        }
        else
        {
            var d = ToGodot(_selectedCell.Direction);
            _orbit.Y = MathF.Atan2(d.X, d.Z);
            _orbit.X = MathF.Asin(Mathf.Clamp(d.Y, -1f, 1f));
        }
        UpdateCamera();
    }

    public void ResetCamera()
    {
        _orbit = new Vector3(-0.24f, 0.72f, 0f);
        _distance = 7.2f;
        _targetDistance = 7.2f;
        _selectedCell = null;
        _selectedEntity = null;
        RebuildSelectionMesh();
        SelectionChanged?.Invoke(null);
        UpdateCamera(true);
    }

    public GameViewState CaptureViewState() =>
        GameViewState.Planet(_orbit, _distance, _selectedEntity?.Id, _selectedCell?.Id);

    public void RestoreViewState(GameViewState state)
    {
        if (state.Shape != WorldShape.Planet || _world?.PlanetMap is null)
            return;

        _orbit = state.PlanetOrbit;
        _distance = Mathf.Clamp(state.PlanetDistance <= 0f ? 7.2f : state.PlanetDistance, MinDistance, MaxDistance);
        _targetDistance = _distance;

        _selectedCell = null;
        if (state.SelectedCellId.HasValue)
        {
            var id = new CellId(state.SelectedCellId.Value);
            if (_world.PlanetMap.TryGetCell(id, out var cell))
                _selectedCell = cell;
        }

        _selectedEntity = null;
        if (!string.IsNullOrWhiteSpace(state.SelectedEntityId))
        {
            foreach (var entity in _world.Entities)
            {
                if (string.Equals(entity.Id, state.SelectedEntityId, StringComparison.Ordinal))
                {
                    _selectedEntity = entity;
                    break;
                }
            }
        }

        RebuildSelectionMesh();
        SelectionChanged?.Invoke(_selectedEntity);
        UpdateCamera(true);
    }

    public WorldRenderDiagnostics GetDiagnostics()
    {
        var count = _world?.PlanetMap?.Cells.Count ?? 0;
        return new WorldRenderDiagnostics(
            count,
            1,
            count,
            1,
            _terrainRebuilds,
            0,
            _estimatedDrawCommands,
            1,
            _terrainNodeCount);
    }

    private void BuildScene()
    {
        _sceneRoot = new Node3D { Name = "PlanetScene" };
        AddChild(_sceneRoot);

        var light = new DirectionalLight3D
        {
            Name = "PlanetSun",
            LightEnergy = 1.25f,
            ShadowEnabled = false,
            RotationDegrees = new Vector3(-42f, -28f, 0f)
        };
        _sceneRoot.AddChild(light);

        _terrain = new MeshInstance3D { Name = "PlanetTerrain" };
        _sceneRoot.AddChild(_terrain);

        _water = new MeshInstance3D { Name = "PlanetWater" };
        _sceneRoot.AddChild(_water);

        _river = new MeshInstance3D { Name = "PlanetRivers" };
        _sceneRoot.AddChild(_river);

        _grid = new MeshInstance3D { Name = "PlanetGrid", Visible = false };
        _sceneRoot.AddChild(_grid);

        _selection = new MeshInstance3D { Name = "PlanetSelection" };
        _sceneRoot.AddChild(_selection);

        _entityMarkers = new MeshInstance3D { Name = "PlanetEntities" };
        _sceneRoot.AddChild(_entityMarkers);

        _camera = new Camera3D
        {
            Name = "PlanetCamera",
            Current = true,
            Fov = 42f,
            Near = 0.05f,
            Far = 40f
        };
        _sceneRoot.AddChild(_camera);
    }

    private void BuildInspector()
    {
        _inspectorPanel = new PanelContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(285, 0)
        };
        _inspectorPanel.AddThemeStyleboxOverride("panel", NatureTechTheme.CardStyle(0.98f));
        AddChild(_inspectorPanel);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 9);
        margin.AddThemeConstantOverride("margin_bottom", 9);
        _inspectorPanel.AddChild(margin);

        _inspectorLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _inspectorLabel.AddThemeFontSizeOverride("font_size", 12);
        _inspectorLabel.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        margin.AddChild(_inspectorLabel);
    }

    private void BuildDebugLabel()
    {
        _debugLabel = new Label
        {
            Visible = false,
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(16, 96)
        };
        _debugLabel.AddThemeFontSizeOverride("font_size", 13);
        _debugLabel.AddThemeColorOverride("font_color", EvolitPalette.SoftAqua);
        AddChild(_debugLabel);
    }

    private void RebuildTerrainMesh()
    {
        if (_terrain is null || _world?.PlanetMap is not { } map)
            return;

        var triangleCount = 0;
        foreach (var cell in map.Cells)
            triangleCount += map.GetPolygon(cell).Length;

        var vertices = new Vector3[triangleCount * 3];
        var normals = new Vector3[vertices.Length];
        var colors = new Color[vertices.Length];
        var cursor = 0;

        foreach (var cell in map.Cells)
        {
            var centerDirection = ToGodot(cell.Direction);
            var center = centerDirection * VisualRadiusFromMeters(cell.ElevationMeters);
            var polygon = map.GetPolygon(cell);
            var polygonElevation = map.GetPolygonTerrainElevationMeters(cell);
            var color = CellColor(cell, _debugMode);

            for (var corner = 0; corner < polygon.Length; corner++)
            {
                var nextCorner = (corner + 1) % polygon.Length;
                var a = center;
                var b = ToGodot(polygon[corner]) * VisualRadiusFromMeters(polygonElevation[corner]);
                var d = ToGodot(polygon[nextCorner]) * VisualRadiusFromMeters(polygonElevation[nextCorner]);
                var cross = (b - a).Cross(d - a);
                if (cross.Dot(centerDirection) < 0f)
                    (b, d) = (d, b);

                var faceNormal = (b - a).Cross(d - a);
                faceNormal = faceNormal.LengthSquared() > 0.0000001f
                    ? faceNormal.Normalized()
                    : centerDirection;
                var centerNormal = (centerDirection * 0.68f + faceNormal * 0.32f).Normalized();
                var bNormal = (b.Normalized() * 0.74f + faceNormal * 0.26f).Normalized();
                var dNormal = (d.Normalized() * 0.74f + faceNormal * 0.26f).Normalized();

                vertices[cursor] = a;
                normals[cursor] = centerNormal;
                colors[cursor++] = color;
                vertices[cursor] = b;
                normals[cursor] = bNormal;
                colors[cursor++] = color;
                vertices[cursor] = d;
                normals[cursor] = dNormal;
                colors[cursor++] = color;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.88f,
            Metallic = 0.02f
        };
        mesh.SurfaceSetMaterial(0, material);
        _terrain.Mesh = mesh;
        _terrainRebuilds++;
        UpdateEstimatedDrawCommands();
        _terrainNodeCount = 6;
    }

    private void RebuildWaterMesh()
    {
        if (_water is null || _world?.PlanetMap is not { } map)
            return;

        var triangleCount = 0;
        foreach (var cell in map.Cells)
        {
            if (cell.WaterKind is not (HexWaterKind.Ocean or HexWaterKind.Lake))
                continue;
            triangleCount += map.GetPolygon(cell).Length;
        }

        if (triangleCount == 0)
        {
            _water.Mesh = null;
            return;
        }

        var vertices = new Vector3[triangleCount * 3];
        var normals = new Vector3[vertices.Length];
        var colors = new Color[vertices.Length];
        var cursor = 0;

        foreach (var cell in map.Cells)
        {
            if (cell.WaterKind is not (HexWaterKind.Ocean or HexWaterKind.Lake))
                continue;

            var polygon = map.GetPolygon(cell);
            var direction = ToGodot(cell.Direction).Normalized();
            var surfaceElevation = PlanetWorldMap.VisibleSurfaceElevationMeters(cell);
            var radius = VisualRadiusFromMeters(surfaceElevation) + 0.006f;
            var center = direction * radius;
            var waterColor = cell.WaterKind == HexWaterKind.Ocean
                ? (cell.WaterDepthMeters > 700f
                    ? new Color(0.018f, 0.12f, 0.20f)
                    : new Color(0.035f, 0.25f, 0.32f))
                : new Color(0.045f, 0.34f, 0.38f);

            for (var corner = 0; corner < polygon.Length; corner++)
            {
                var next = (corner + 1) % polygon.Length;
                var a = center;
                var b = ToGodot(polygon[corner]) * radius;
                var d = ToGodot(polygon[next]) * radius;
                if ((b - a).Cross(d - a).Dot(direction) < 0f)
                    (b, d) = (d, b);

                vertices[cursor] = a;
                normals[cursor] = direction;
                colors[cursor++] = waterColor;
                vertices[cursor] = b;
                normals[cursor] = b.Normalized();
                colors[cursor++] = waterColor;
                vertices[cursor] = d;
                normals[cursor] = d.Normalized();
                colors[cursor++] = waterColor;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.36f,
            Metallic = 0.08f
        });
        _water.Mesh = mesh;
        _water.Visible = _debugMode == GenerationDebugMode.None;
        UpdateEstimatedDrawCommands();
    }

    private void RebuildRiverMesh()
    {
        if (_river is null || _world?.PlanetMap is not { } map)
            return;

        const int segments = 4;
        var riverCount = 0;
        foreach (var cell in map.Cells)
            if (cell.WaterKind == HexWaterKind.River &&
                cell.DrainageTarget >= 0 &&
                map.TryGetCell(cell.DrainageTarget, out _))
                riverCount++;

        if (riverCount == 0)
        {
            _river.Mesh = null;
            return;
        }

        var vertices = new Vector3[riverCount * (segments + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var colors = new Color[vertices.Length];
        var indices = new int[riverCount * segments * 6];
        var vertexCursor = 0;
        var indexCursor = 0;
        var riverColor = new Color(0.08f, 0.55f, 0.62f);

        foreach (var cell in map.Cells)
        {
            if (cell.WaterKind != HexWaterKind.River ||
                cell.DrainageTarget < 0 ||
                !map.TryGetCell(cell.DrainageTarget, out var target))
                continue;

            var startDirection = ToGodot(cell.Direction).Normalized();
            var endDirection = ToGodot(target.Direction).Normalized();
            var startElevation = PlanetWorldMap.VisibleSurfaceElevationMeters(cell);
            var endElevation = PlanetWorldMap.VisibleSurfaceElevationMeters(target);
            var width = 0.0045f + Mathf.Clamp(cell.RiverWidth / 5.2f, 0.08f, 1f) * 0.0105f;
            var baseVertex = vertexCursor;

            for (var sample = 0; sample <= segments; sample++)
            {
                var t = sample / (float)segments;
                var radial = (startDirection * (1f - t) + endDirection * t).Normalized();
                var tangent = endDirection - radial * endDirection.Dot(radial);
                if (tangent.LengthSquared() <= 0.000001f)
                    tangent = radial.Cross(Vector3.Up);
                tangent = tangent.Normalized();
                var side = radial.Cross(tangent).Normalized();
                var elevation = Mathf.Lerp(startElevation, endElevation, t);
                var radius = VisualRadiusFromMeters(elevation) + 0.012f;
                var center = radial * radius;

                vertices[vertexCursor] = center + side * width;
                normals[vertexCursor] = radial;
                colors[vertexCursor++] = riverColor;
                vertices[vertexCursor] = center - side * width;
                normals[vertexCursor] = radial;
                colors[vertexCursor++] = riverColor;
            }

            for (var segment = 0; segment < segments; segment++)
            {
                var a = baseVertex + segment * 2;
                var b = a + 1;
                var c0 = a + 2;
                var d = a + 3;
                indices[indexCursor++] = a;
                indices[indexCursor++] = c0;
                indices[indexCursor++] = b;
                indices[indexCursor++] = b;
                indices[indexCursor++] = c0;
                indices[indexCursor++] = d;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.42f,
            Metallic = 0.02f
        });
        _river.Mesh = mesh;
        _river.Visible =
            _debugMode == GenerationDebugMode.None &&
            _distance <= RiverVisibleDistance;
    }

    private void RebuildGridMesh()
    {
        if (_grid is null || _world?.PlanetMap is not { } map)
            return;

        var edgeVertices = 0;
        foreach (var cell in map.Cells)
            edgeVertices += map.GetPolygon(cell).Length * 2;

        var vertices = new Vector3[edgeVertices];
        var cursor = 0;
        foreach (var cell in map.Cells)
        {
            var polygon = map.GetPolygon(cell);
            var elevation = map.GetPolygonSurfaceElevationMeters(cell);
            for (var i = 0; i < polygon.Length; i++)
            {
                var next = (i + 1) % polygon.Length;
                vertices[cursor++] = ToGodot(polygon[i]) * (VisualRadiusFromMeters(elevation[i]) + 0.014f);
                vertices[cursor++] = ToGodot(polygon[next]) * (VisualRadiusFromMeters(elevation[next]) + 0.014f);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            AlbedoColor = new Color(0.16f, 0.31f, 0.32f),
            Roughness = 1f
        });
        _grid.Mesh = mesh;
    }

    private void RebuildSelectionMesh()
    {
        if (_selection is null)
            return;

        if (_selectedCell is null || _world?.PlanetMap is not { } map)
        {
            _selection.Mesh = null;
            return;
        }

        var polygon = map.GetPolygon(_selectedCell);
        var elevation = map.GetPolygonSurfaceElevationMeters(_selectedCell);
        var vertices = new Vector3[polygon.Length * 2];
        var cursor = 0;
        for (var i = 0; i < polygon.Length; i++)
        {
            var next = (i + 1) % polygon.Length;
            vertices[cursor++] = ToGodot(polygon[i]) * (VisualRadiusFromMeters(elevation[i]) + 0.026f);
            vertices[cursor++] = ToGodot(polygon[next]) * (VisualRadiusFromMeters(elevation[next]) + 0.026f);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            AlbedoColor = new Color(0.48f, 1f, 0.96f),
            Roughness = 0.72f
        });
        _selection.Mesh = mesh;
        UpdateEstimatedDrawCommands();
    }

    private void RebuildEntityMarkers()
    {
        if (_entityMarkers is null || _world?.PlanetMap is not { } map)
            return;

        var drawable = 0;
        foreach (var entity in _world.Entities)
            if (entity.SurfaceCellId.HasValue && map.TryGetCell(entity.SurfaceCellId.Value, out _))
                drawable++;

        if (drawable == 0)
        {
            _entityMarkers.Mesh = null;
            return;
        }

        const int trianglesPerMarker = 8;
        var vertices = new Vector3[drawable * trianglesPerMarker * 3];
        var normals = new Vector3[vertices.Length];
        var colors = new Color[vertices.Length];
        var cursor = 0;

        foreach (var entity in _world.Entities)
        {
            if (!entity.SurfaceCellId.HasValue ||
                !map.TryGetCell(entity.SurfaceCellId.Value, out var cell))
                continue;

            var radial = ToGodot(cell.Direction).Normalized();
            var reference = Math.Abs(radial.Y) < 0.92f ? Vector3.Up : Vector3.Right;
            var tangent = reference.Cross(radial).Normalized();
            var bitangent = radial.Cross(tangent).Normalized();
            var size = entity.Kind == DemoEntityKind.Creature ? 0.052f : 0.044f;
            var center = radial * (VisualRadius(cell) + 0.060f);
            var apex = center + radial * size * 1.35f;
            var baseA = center + tangent * size;
            var baseB = center + bitangent * size;
            var baseC = center - tangent * size;
            var baseD = center - bitangent * size;
            var inner = center - radial * size * 0.32f;
            var color = entity.Kind == DemoEntityKind.Creature
                ? new Color(0.72f, 0.95f, 0.93f)
                : new Color(0.44f, 0.83f, 0.48f);

            AddMarkerTriangle(apex, baseA, baseB, radial, color, vertices, normals, colors, ref cursor);
            AddMarkerTriangle(apex, baseB, baseC, radial, color, vertices, normals, colors, ref cursor);
            AddMarkerTriangle(apex, baseC, baseD, radial, color, vertices, normals, colors, ref cursor);
            AddMarkerTriangle(apex, baseD, baseA, radial, color, vertices, normals, colors, ref cursor);
            AddMarkerTriangle(inner, baseA, baseD, radial, color, vertices, normals, colors, ref cursor);
            AddMarkerTriangle(inner, baseD, baseC, radial, color, vertices, normals, colors, ref cursor);
            AddMarkerTriangle(inner, baseC, baseB, radial, color, vertices, normals, colors, ref cursor);
            AddMarkerTriangle(inner, baseB, baseA, radial, color, vertices, normals, colors, ref cursor);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.72f
        });
        _entityMarkers.Mesh = mesh;
        _entityMarkers.Visible =
            _debugMode == GenerationDebugMode.None &&
            _distance <= EntityVisibleDistance;
        _terrainNodeCount = 6;
        UpdateEstimatedDrawCommands();
    }

    private static void AddMarkerTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 d,
        Vector3 outward,
        Color color,
        Vector3[] vertices,
        Vector3[] normals,
        Color[] colors,
        ref int cursor)
    {
        if ((b - a).Cross(d - a).Dot(outward) < 0f)
            (b, d) = (d, b);

        vertices[cursor] = a;
        normals[cursor] = outward;
        colors[cursor++] = color;
        vertices[cursor] = b;
        normals[cursor] = outward;
        colors[cursor++] = color;
        vertices[cursor] = d;
        normals[cursor] = outward;
        colors[cursor++] = color;
    }

    private void UpdateCamera(bool force = false)
    {
        if (_camera is null)
            return;

        _orbit.X = Mathf.Clamp(_orbit.X, -1.45f, 1.45f);
        _distance = Mathf.Clamp(_distance, MinDistance, MaxDistance);
        var cp = MathF.Cos(_orbit.X);
        var position = new Vector3(
            MathF.Sin(_orbit.Y) * cp,
            MathF.Sin(_orbit.X),
            MathF.Cos(_orbit.Y) * cp) * _distance;

        _camera.Position = position;
        _camera.LookAt(Vector3.Zero, Vector3.Up);

        var visibilityChanged = force;
        if (_grid is not null)
        {
            var next = _distance <= GridVisibleDistance;
            if (_grid.Visible != next)
            {
                _grid.Visible = next;
                visibilityChanged = true;
            }
        }

        if (_river is not null)
        {
            var next = _debugMode == GenerationDebugMode.None && _distance <= RiverVisibleDistance;
            if (_river.Visible != next)
            {
                _river.Visible = next;
                visibilityChanged = true;
            }
        }

        if (_entityMarkers is not null)
        {
            var next = _debugMode == GenerationDebugMode.None && _distance <= EntityVisibleDistance;
            if (_entityMarkers.Visible != next)
            {
                _entityMarkers.Visible = next;
                visibilityChanged = true;
            }
        }

        if (visibilityChanged)
            UpdateEstimatedDrawCommands();
    }

    private void UpdateEstimatedDrawCommands()
    {
        _estimatedDrawCommands =
            1 +
            (_water?.Visible == true && _water.Mesh is not null ? 1 : 0) +
            (_river?.Visible == true && _river.Mesh is not null ? 1 : 0) +
            (_grid?.Visible == true ? 1 : 0) +
            (_selection?.Mesh is null ? 0 : 1) +
            (_entityMarkers?.Visible == true && _entityMarkers.Mesh is not null ? 1 : 0);
    }

    private void SetTargetDistance(float value)
    {
        _targetDistance = Mathf.Clamp(value, MinDistance, MaxDistance);
        if (!_smoothZoom)
        {
            _distance = _targetDistance;
            UpdateCamera();
        }
    }

    private void SelectAt(Vector2 mouse)
    {
        var cell = PickCell(mouse);
        if (cell is null)
            return;

        _selectedCell = cell;
        _selectedEntity = FindEntity(cell.Id);
        RebuildSelectionMesh();
        SelectionChanged?.Invoke(_selectedEntity);
    }

    private PlanetWorldCell? PickCell(Vector2 mouse)
    {
        if (_camera is null || _world?.PlanetMap is not { } map)
            return null;

        var origin = _camera.ProjectRayOrigin(mouse);
        var direction = _camera.ProjectRayNormal(mouse).Normalized();
        var b = 2f * origin.Dot(direction);
        var c = origin.LengthSquared() - PlanetRadius * PlanetRadius;
        var discriminant = b * b - 4f * c;
        if (discriminant < 0f)
            return null;

        var root = MathF.Sqrt(discriminant);
        var t0 = (-b - root) * 0.5f;
        var t1 = (-b + root) * 0.5f;
        var t = t0 > 0f ? t0 : t1 > 0f ? t1 : -1f;
        if (t <= 0f)
            return null;

        var hit = (origin + direction * t).Normalized();
        return map.FindNearestCell(ToCore(hit));
    }

    private DemoEntity? FindEntity(CellId cellId)
    {
        if (_world is null)
            return null;
        foreach (var entity in _world.Entities)
            if (entity.SurfaceCellId == cellId)
                return entity;
        return null;
    }

    private void UpdateInspector()
    {
        if (_inspectorPanel is null || _inspectorLabel is null)
            return;

        var active = Godot.Input.IsActionPressed("terrain_inspect");
        if (!active)
        {
            _inspectorPanel.Visible = false;
            return;
        }

        var mouse = GetLocalMousePosition();
        if (!new Rect2(Vector2.Zero, Size).HasPoint(mouse))
        {
            _inspectorPanel.Visible = false;
            return;
        }

        var cell = PickCell(mouse);
        if (cell is null)
        {
            _inspectorPanel.Visible = false;
            return;
        }

        _inspectorPanel.Visible = true;
        var panelWidth = Math.Max(285f, _inspectorPanel.Size.X);
        var panelHeight = Math.Max(260f, _inspectorPanel.Size.Y);
        _inspectorPanel.Position = new Vector2(
            Math.Max(8f, Math.Min(Size.X - panelWidth - 8f, mouse.X + 18f)),
            Math.Max(8f, Math.Min(Size.Y - panelHeight - 8f, mouse.Y + 18f)));
        _inspectorLabel.Text = FormatInspector(cell);
    }

    private string FormatInspector(PlanetWorldCell cell)
    {
        var terrain = TerrainLabel(cell.Terrain);
        if (_core is null || !_core.TryGetEnvironment(cell.Id, out var env, out var physical))
            return $"Клетка {cell.Id.PlanetIndex}\nРельеф: {terrain}\nCore environment: недоступен";

        var vertical = env.WaterDepthMeters > 0.01f
            ? $"Вода: {env.WaterDepthMeters:0.0} м"
            : $"Высота: {env.ElevationMeters:0} м";
        var wind = MathF.Sqrt(physical.WindX * physical.WindX + physical.WindY * physical.WindY);
        var water = cell.WaterKind == HexWaterKind.None ? "суша" : cell.WaterKind.ToString();
        var river = cell.WaterKind == HexWaterKind.River
            ? $"\nРека: длина {cell.RiverLength} · ширина {cell.RiverWidth:0.00} · order {cell.StreamOrder} · ветви {cell.UpstreamBranches} · dir {cell.RiverDirection} · target {cell.DrainageTarget}"
            : string.Empty;
        return
            $"Клетка {cell.Id.PlanetIndex}\nРельеф: {terrain} · вода: {water}\n{vertical}\n" +
            $"Климат: {env.TemperatureCelsius:0.0} °C · {env.Humidity * 100f:0}% · {env.PressureKPa:0.0} кПа\n" +
            $"Ветер: {wind:0.000} · Осадки: {physical.Precipitation:0.000}\n" +
            $"Материал: {env.Substrate} · Минералы: {env.MineralPotential:0.00}\n" +
            $"Питательность: {env.NutrientPotential:0.00} · Геотермия: {env.GeothermalPotential:0.00}\n" +
            $"Провинция: {cell.ProvinceId} · Бассейн: {cell.BasinId} · Берег: {cell.CoastDistance}" +
            river;
    }

    private static float VisualRadius(PlanetWorldCell cell) =>
        VisualRadiusFromMeters(PlanetWorldMap.VisibleSurfaceElevationMeters(cell));

    private static float VisualRadiusFromMeters(float elevationMeters)
    {
        var offset = elevationMeters >= 0f
            ? Math.Clamp(elevationMeters / 45_000f, 0f, 0.095f)
            : -Math.Clamp(-elevationMeters / 70_000f, 0f, 0.080f);
        return PlanetRadius + offset;
    }

    private static Color CellColor(PlanetWorldCell cell, GenerationDebugMode mode)
    {
        if (mode != GenerationDebugMode.None)
            return DebugColor(cell, mode);

        var baseColor = cell.Terrain switch
        {
            HexTerrainType.DeepWater => new Color(0.025f, 0.16f, 0.24f),
            HexTerrainType.ShallowWater => new Color(0.055f, 0.31f, 0.39f),
            HexTerrainType.Lake => new Color(0.06f, 0.37f, 0.42f),
            HexTerrainType.River => new Color(0.10f, 0.48f, 0.51f),
            HexTerrainType.Sand => new Color(0.58f, 0.51f, 0.31f),
            HexTerrainType.Desert => new Color(0.62f, 0.47f, 0.23f),
            HexTerrainType.Grassland => new Color(0.23f, 0.48f, 0.27f),
            HexTerrainType.Highland => new Color(0.36f, 0.44f, 0.28f),
            HexTerrainType.Rocky => new Color(0.34f, 0.38f, 0.34f),
            HexTerrainType.Mountain => new Color(0.49f, 0.51f, 0.48f),
            _ => new Color(0.24f, 0.45f, 0.30f)
        };
        var variation = (cell.VisualVariation - 0.5f) * 0.08f;
        return new Color(
            Mathf.Clamp(baseColor.R + variation, 0f, 1f),
            Mathf.Clamp(baseColor.G + variation, 0f, 1f),
            Mathf.Clamp(baseColor.B + variation, 0f, 1f));
    }

    private static Color DebugColor(PlanetWorldCell cell, GenerationDebugMode mode)
    {
        var value = mode switch
        {
            GenerationDebugMode.Continentalness => Mathf.Clamp(cell.Continentalness * 0.8f + 0.5f, 0f, 1f),
            GenerationDebugMode.Elevation => Mathf.Clamp((cell.ElevationMeters + 5200f) / 9100f, 0f, 1f),
            GenerationDebugMode.Slope => Mathf.Clamp(cell.Slope / 1100f, 0f, 1f),
            GenerationDebugMode.CoastDistance => Mathf.Clamp(cell.CoastDistance / 18f, 0f, 1f),
            GenerationDebugMode.WaterDepth => Mathf.Clamp(cell.WaterDepthMeters / 3200f, 0f, 1f),
            GenerationDebugMode.FlowAccumulation => Mathf.Clamp(MathF.Log10(1f + cell.FlowAccumulation) / 4f, 0f, 1f),
            GenerationDebugMode.StreamOrder => Mathf.Clamp(cell.StreamOrder / 6f, 0f, 1f),
            GenerationDebugMode.RiverDirection => cell.RiverDirection < 0 ? 0f : Mathf.Clamp((cell.RiverDirection + 1f) / 6f, 0f, 1f),
            GenerationDebugMode.TectonicUplift => Mathf.Clamp(cell.TectonicUplift, 0f, 1f),
            GenerationDebugMode.Temperature => Mathf.Clamp((cell.TemperatureCelsius + 45f) / 90f, 0f, 1f),
            GenerationDebugMode.Humidity => Mathf.Clamp(cell.Humidity, 0f, 1f),
            GenerationDebugMode.Minerals => Mathf.Clamp(cell.MineralPotential, 0f, 1f),
            GenerationDebugMode.Province => HashColorValue(cell.ProvinceId),
            GenerationDebugMode.Basin => HashColorValue(cell.BasinId),
            GenerationDebugMode.Substrate => HashColorValue((int)cell.Substrate),
            GenerationDebugMode.Region => HashColorValue((int)cell.Terrain),
            _ => 0.5f
        };

        if (mode is GenerationDebugMode.Province or GenerationDebugMode.Basin or GenerationDebugMode.Substrate or GenerationDebugMode.Region or GenerationDebugMode.RiverDirection)
            return HashColor((int)(value * 10000f));

        return new Color(
            Mathf.Clamp(0.18f + value * 0.78f, 0f, 1f),
            Mathf.Clamp(0.22f + (1f - Math.Abs(value - 0.5f) * 2f) * 0.66f, 0f, 1f),
            Mathf.Clamp(0.82f - value * 0.66f, 0f, 1f));
    }

    private static float HashColorValue(int value)
    {
        unchecked
        {
            var x = (uint)(value + 1) * 2654435761u;
            return (x & 0xffff) / 65535f;
        }
    }

    private static Color HashColor(int value)
    {
        unchecked
        {
            var x = (uint)(value + 17) * 2654435761u;
            return new Color(
                0.25f + ((x >> 0) & 255) / 255f * 0.70f,
                0.25f + ((x >> 8) & 255) / 255f * 0.70f,
                0.25f + ((x >> 16) & 255) / 255f * 0.70f);
        }
    }

    private static string TerrainLabel(HexTerrainType terrain) => terrain switch
    {
        HexTerrainType.DeepWater => "Глубокий океан",
        HexTerrainType.ShallowWater => "Мелководье",
        HexTerrainType.Lake => "Озеро",
        HexTerrainType.River => "Река",
        HexTerrainType.Sand => "Песок",
        HexTerrainType.Desert => "Пустыня",
        HexTerrainType.Grassland => "Равнина",
        HexTerrainType.Highland => "Возвышенность",
        HexTerrainType.Rocky => "Камни",
        HexTerrainType.Mountain => "Горы",
        _ => terrain.ToString()
    };

    private static Vector3 ToGodot(CoreVector3 value) => new(value.X, value.Y, value.Z);
    private static CoreVector3 ToCore(Vector3 value) => new(value.X, value.Y, value.Z);
}
