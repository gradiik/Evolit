using System;
using System.Collections.Generic;
using Evolit.Game;
using Godot;

namespace Evolit.UI.Game;

public enum WorldHistoryMetric
{
    CreaturePopulation,
    PlantPopulation,
    SpeciesCount,
    SubspeciesCount
}

public readonly record struct WorldGraphSeries(string Label, WorldHistoryMetric Metric, Color Color);

public sealed partial class WorldStatsGraph : Control
{
    private const int YLabelCount = 4;
    private const int XLabelCount = 5;

    private IReadOnlyList<WorldHistorySample> _samples = Array.Empty<WorldHistorySample>();
    private IReadOnlyList<WorldGraphSeries> _series = Array.Empty<WorldGraphSeries>();
    private readonly List<int> _renderIndices = new();
    private readonly List<Vector2[]> _renderPaths = new();
    private readonly Label[] _yLabels = new Label[YLabelCount];
    private readonly Label[] _xLabels = new Label[XLabelCount];

    private int _visibleStart;
    private int _visibleEnd = -1;
    private int _hoverIndex = -1;
    private float _maxY = 1f;
    private Rect2 _plotRect;
    private PanelContainer? _tooltip;
    private Label? _tooltipLabel;

    public WorldStatsGraph()
    {
        CustomMinimumSize = new Vector2(0, 190);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
    }

    public override void _Ready()
    {
        CreateAxisLabels();
        CreateTooltip();
        Resized += RebuildGeometry;
        MouseExited += ClearHover;
        RebuildGeometry();
    }

    public void SetData(
        IReadOnlyList<WorldHistorySample>? samples,
        IReadOnlyList<WorldGraphSeries>? series,
        int visibleStartIndex = 0)
    {
        _samples = samples ?? Array.Empty<WorldHistorySample>();
        _series = series ?? Array.Empty<WorldGraphSeries>();
        _visibleStart = Math.Clamp(visibleStartIndex, 0, Math.Max(0, _samples.Count - 1));
        _visibleEnd = _samples.Count - 1;
        _hoverIndex = -1;
        if (_tooltip is not null)
            _tooltip.Visible = false;
        RebuildGeometry();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
            UpdateHover(motion.Position);
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, new Color(0.012f, 0.060f, 0.072f, 0.74f), true);
        DrawRect(bounds.Grow(-0.5f), new Color(EvolitPalette.SoftAqua, 0.16f), false, 1f);

        if (_plotRect.Size.X <= 1 || _plotRect.Size.Y <= 1)
            return;

        for (var i = 0; i < YLabelCount; i++)
        {
            var ratio = i / (float)(YLabelCount - 1);
            var y = _plotRect.End.Y - _plotRect.Size.Y * ratio;
            DrawLine(new Vector2(_plotRect.Position.X, y), new Vector2(_plotRect.End.X, y), new Color(EvolitPalette.FogBlue, 0.09f), 1f);
        }

        if (_visibleEnd < _visibleStart || _series.Count == 0)
            return;

        for (var i = 0; i < _renderPaths.Count && i < _series.Count; i++)
        {
            var points = _renderPaths[i];
            if (points.Length > 1)
                DrawPolyline(points, _series[i].Color, 2.2f, true);
        }

        if (_hoverIndex < _visibleStart || _hoverIndex > _visibleEnd)
            return;

        var hoverX = XForIndex(_hoverIndex);
        DrawLine(
            new Vector2(hoverX, _plotRect.Position.Y),
            new Vector2(hoverX, _plotRect.End.Y),
            new Color(EvolitPalette.MistWhite, 0.30f),
            1f);

        var sample = _samples[_hoverIndex];
        foreach (var item in _series)
        {
            var y = YForValue(GetMetric(sample, item.Metric));
            DrawCircle(new Vector2(hoverX, y), 4.2f, item.Color);
            DrawCircle(new Vector2(hoverX, y), 2.0f, EvolitPalette.MistWhite);
        }
    }

    private void RebuildGeometry()
    {
        _plotRect = new Rect2(50, 12, Math.Max(1, Size.X - 64), Math.Max(1, Size.Y - 44));
        _renderIndices.Clear();
        _renderPaths.Clear();

        if (_samples.Count == 0 || _series.Count == 0 || _visibleEnd < _visibleStart)
        {
            UpdateAxisLabels();
            QueueRedraw();
            return;
        }

        _maxY = 1f;
        for (var i = _visibleStart; i <= _visibleEnd; i++)
        {
            foreach (var item in _series)
                _maxY = Math.Max(_maxY, GetMetric(_samples[i], item.Metric));
        }
        _maxY = Math.Max(1f, (float)Math.Ceiling(_maxY * 1.08f));

        BuildRenderIndices();
        foreach (var item in _series)
        {
            var points = new Vector2[_renderIndices.Count];
            for (var i = 0; i < _renderIndices.Count; i++)
            {
                var sampleIndex = _renderIndices[i];
                points[i] = new Vector2(
                    XForIndex(sampleIndex),
                    YForValue(GetMetric(_samples[sampleIndex], item.Metric)));
            }
            _renderPaths.Add(points);
        }

        UpdateAxisLabels();
        QueueRedraw();
    }

    private void BuildRenderIndices()
    {
        var count = _visibleEnd - _visibleStart + 1;
        var maxRenderPoints = Math.Max(64, (int)Math.Ceiling(_plotRect.Size.X * 1.5f));
        if (count <= maxRenderPoints)
        {
            for (var i = _visibleStart; i <= _visibleEnd; i++)
                _renderIndices.Add(i);
            return;
        }

        var pointsPerBucket = Math.Max(4, _series.Count * 2 + 2);
        var bucketCount = Math.Max(1, maxRenderPoints / pointsPerBucket);
        var bucketSize = count / (double)bucketCount;
        var candidates = new HashSet<int> { _visibleStart, _visibleEnd };

        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var start = _visibleStart + (int)Math.Floor(bucket * bucketSize);
            var end = _visibleStart + (int)Math.Floor((bucket + 1) * bucketSize) - 1;
            start = Math.Clamp(start, _visibleStart, _visibleEnd);
            end = Math.Clamp(end, start, _visibleEnd);

            candidates.Add(start);
            candidates.Add(end);

            foreach (var item in _series)
            {
                var minIndex = start;
                var maxIndex = start;
                var minValue = float.MaxValue;
                var maxValue = float.MinValue;

                for (var i = start; i <= end; i++)
                {
                    var value = GetMetric(_samples[i], item.Metric);
                    if (value < minValue)
                    {
                        minValue = value;
                        minIndex = i;
                    }
                    if (value > maxValue)
                    {
                        maxValue = value;
                        maxIndex = i;
                    }
                }

                candidates.Add(minIndex);
                candidates.Add(maxIndex);
            }
        }

        _renderIndices.AddRange(candidates);
        _renderIndices.Sort();
    }

    private void UpdateHover(Vector2 position)
    {
        if (!_plotRect.HasPoint(position) || _visibleEnd < _visibleStart)
        {
            ClearHover();
            return;
        }

        var count = _visibleEnd - _visibleStart + 1;
        var ratio = Mathf.Clamp((position.X - _plotRect.Position.X) / Math.Max(1f, _plotRect.Size.X), 0f, 1f);
        var nextIndex = _visibleStart + (int)Math.Round(ratio * Math.Max(0, count - 1));
        nextIndex = Math.Clamp(nextIndex, _visibleStart, _visibleEnd);

        if (nextIndex != _hoverIndex)
        {
            _hoverIndex = nextIndex;
            UpdateTooltipText();
        }

        PositionTooltip(position);
        QueueRedraw();
    }

    private void ClearHover()
    {
        if (_hoverIndex < 0 && (_tooltip is null || !_tooltip.Visible))
            return;

        _hoverIndex = -1;
        if (_tooltip is not null)
            _tooltip.Visible = false;
        QueueRedraw();
    }

    private void UpdateTooltipText()
    {
        if (_tooltip is null || _tooltipLabel is null || _hoverIndex < 0 || _hoverIndex >= _samples.Count)
            return;

        var sample = _samples[_hoverIndex];
        var lines = new List<string>(_series.Count + 1) { sample.TooltipLabel };
        foreach (var item in _series)
            lines.Add($"{item.Label}: {(int)Math.Round(GetMetric(sample, item.Metric))}");

        _tooltipLabel.Text = string.Join("\n", lines);
        _tooltip.Size = new Vector2(190, 32 + _series.Count * 20);
        _tooltip.Visible = true;
    }

    private void PositionTooltip(Vector2 cursor)
    {
        if (_tooltip is null || !_tooltip.Visible)
            return;

        const float gap = 14f;
        var x = cursor.X + gap;
        if (x + _tooltip.Size.X > Size.X - 8)
            x = cursor.X - gap - _tooltip.Size.X;

        var y = Mathf.Clamp(cursor.Y - _tooltip.Size.Y * 0.5f, 8f, Math.Max(8f, Size.Y - _tooltip.Size.Y - 8f));
        _tooltip.Position = new Vector2(Math.Max(8f, x), y);
    }

    private void CreateAxisLabels()
    {
        for (var i = 0; i < YLabelCount; i++)
        {
            var label = AxisLabel(HorizontalAlignment.Right);
            _yLabels[i] = label;
            AddChild(label);
        }

        for (var i = 0; i < XLabelCount; i++)
        {
            var label = AxisLabel(HorizontalAlignment.Center);
            _xLabels[i] = label;
            AddChild(label);
        }
    }

    private void UpdateAxisLabels()
    {
        if (_yLabels[0] is null || _xLabels[0] is null)
            return;

        for (var i = 0; i < YLabelCount; i++)
        {
            var ratio = i / (float)(YLabelCount - 1);
            var value = _maxY * ratio;
            var y = _plotRect.End.Y - _plotRect.Size.Y * ratio - 9f;
            _yLabels[i].Text = $"{value:0}";
            _yLabels[i].Position = new Vector2(2, y);
            _yLabels[i].Size = new Vector2(42, 18);
        }

        var visibleCount = _visibleEnd - _visibleStart + 1;
        for (var i = 0; i < XLabelCount; i++)
        {
            if (visibleCount <= 0 || _samples.Count == 0)
            {
                _xLabels[i].Visible = false;
                continue;
            }

            _xLabels[i].Visible = true;
            var ratio = i / (float)(XLabelCount - 1);
            var index = _visibleStart + (int)Math.Round(ratio * Math.Max(0, visibleCount - 1));
            index = Math.Clamp(index, _visibleStart, _visibleEnd);
            var x = _plotRect.Position.X + _plotRect.Size.X * ratio;
            _xLabels[i].Text = _samples[index].AxisLabel;
            _xLabels[i].Position = new Vector2(x - 55, _plotRect.End.Y + 5);
            _xLabels[i].Size = new Vector2(110, 18);
        }
    }

    private void CreateTooltip()
    {
        _tooltip = new PanelContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 20
        };
        _tooltip.AddThemeStyleboxOverride("panel", NatureTechTheme.SectionStyle());
        AddChild(_tooltip);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _tooltip.AddChild(margin);

        _tooltipLabel = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _tooltipLabel.AddThemeFontSizeOverride("font_size", 11);
        _tooltipLabel.AddThemeColorOverride("font_color", EvolitPalette.MistWhite);
        margin.AddChild(_tooltipLabel);
    }

    private static Label AxisLabel(HorizontalAlignment alignment)
    {
        var label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 9);
        label.AddThemeColorOverride("font_color", new Color(EvolitPalette.FogBlue, 0.82f));
        return label;
    }

    private float XForIndex(int index)
    {
        var count = _visibleEnd - _visibleStart;
        if (count <= 0)
            return _plotRect.Position.X + _plotRect.Size.X * 0.5f;

        var ratio = (index - _visibleStart) / (float)count;
        return _plotRect.Position.X + _plotRect.Size.X * ratio;
    }

    private float YForValue(float value)
    {
        var normalized = Mathf.Clamp(value / Math.Max(1f, _maxY), 0f, 1f);
        return _plotRect.End.Y - _plotRect.Size.Y * normalized;
    }

    private static float GetMetric(WorldHistorySample sample, WorldHistoryMetric metric)
    {
        return metric switch
        {
            WorldHistoryMetric.CreaturePopulation => sample.CreaturePopulation,
            WorldHistoryMetric.PlantPopulation => sample.PlantPopulation,
            WorldHistoryMetric.SpeciesCount => sample.SpeciesCount,
            WorldHistoryMetric.SubspeciesCount => sample.SubspeciesCount,
            _ => 0
        };
    }
}
