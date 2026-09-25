using System;
using System.Collections.Generic;
using Godot;

namespace Evolit.UI.Game;

public sealed partial class PopulationGraph : Control
{
    private IReadOnlyList<float> _values = Array.Empty<float>();

    public PopulationGraph()
    {
        CustomMinimumSize = UiMetrics.Size(0, 120);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetValues(IReadOnlyList<float>? values)
    {
        _values = values ?? Array.Empty<float>();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var width = Size.X;
        var height = Size.Y;
        if (width < 8 || height < 8)
            return;

        var area = new Rect2(1, 1, width - 2, height - 2);
        DrawRect(area, new Color(0.02f, 0.09f, 0.11f, 0.72f), true);
        DrawRect(area, new Color(EvolitPalette.EvolutionCyan, 0.16f), false, 1f);

        for (var i = 1; i < 4; i++)
        {
            var y = height * i / 4f;
            DrawLine(new Vector2(8, y), new Vector2(width - 8, y), new Color(EvolitPalette.FogBlue, 0.08f), 1f);
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

        var innerWidth = width - 20f;
        var innerHeight = height - 18f;
        Vector2? previous = null;

        for (var i = 0; i < _values.Count; i++)
        {
            var x = 10f + innerWidth * i / (_values.Count - 1f);
            var normalized = (_values[i] - min) / (max - min);
            var y = height - 9f - normalized * innerHeight;
            var point = new Vector2(x, y);

            if (previous.HasValue)
                DrawLine(previous.Value, point, EvolitPalette.EvolutionCyan, 2.2f, true);

            DrawCircle(point, 2.6f, EvolitPalette.SoftAqua);
            previous = point;
        }
    }
}
