using System;
using System.Collections.Generic;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class WorldStatsGraph : Control
{
    private IReadOnlyList<float> _values = Array.Empty<float>();
    private Color _lineColor = EvolitPalette.EvolutionCyan;

    public WorldStatsGraph()
    {
        CustomMinimumSize = new Vector2(0, 128);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetValues(IReadOnlyList<float>? values, Color? lineColor = null)
    {
        _values = values ?? Array.Empty<float>();
        if (lineColor.HasValue)
            _lineColor = lineColor.Value;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X < 12 || Size.Y < 12)
            return;

        var bounds = new Rect2(1, 1, Size.X - 2, Size.Y - 2);
        DrawRect(bounds, new Color(0.012f, 0.060f, 0.072f, 0.74f), true);
        DrawRect(bounds, new Color(_lineColor, 0.18f), false, 1f);

        for (var i = 1; i < 4; i++)
        {
            var y = Size.Y * i / 4f;
            DrawLine(new Vector2(8, y), new Vector2(Size.X - 8, y), new Color(EvolitPalette.FogBlue, 0.07f), 1f);
        }

        if (_values.Count < 2)
            return;

        var min = float.MaxValue;
        var max = float.MinValue;
        foreach (var value in _values)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (Math.Abs(max - min) < 0.001f)
            max = min + 1f;

        var width = Size.X - 20f;
        var height = Size.Y - 18f;
        Vector2? previous = null;

        for (var i = 0; i < _values.Count; i++)
        {
            var x = 10f + width * i / (_values.Count - 1f);
            var normalized = (_values[i] - min) / (max - min);
            var y = Size.Y - 9f - normalized * height;
            var point = new Vector2(x, y);

            if (previous.HasValue)
                DrawLine(previous.Value, point, _lineColor, 2.2f, true);

            if (i == _values.Count - 1)
                DrawCircle(point, 3.4f, EvolitPalette.MistWhite);

            previous = point;
        }
    }
}
