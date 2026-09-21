using System;
using System.Collections.Generic;
using System.Linq;
using Evolit.Game;
using Godot;

namespace Evolit.UI;

public readonly record struct SpeciesVisualDescriptor(
    DemoSpeciesKind Kind,
    uint FamilySeed,
    uint VariantSeed,
    bool Extinct)
{
    public static SpeciesVisualDescriptor FromSpecies(DemoSpeciesRecord species, IReadOnlyList<DemoSpeciesRecord> speciesSet)
    {
        var family = FindFamily(species, speciesSet);
        return new SpeciesVisualDescriptor(
            species.Kind,
            StableHash(family.Id),
            StableHash(species.Id),
            species.Status == DemoSpeciesStatus.Extinct);
    }

    private static DemoSpeciesRecord FindFamily(DemoSpeciesRecord species, IReadOnlyList<DemoSpeciesRecord> speciesSet)
    {
        var current = species;
        var guard = 0;
        while (!string.IsNullOrWhiteSpace(current.ParentId) && current.ParentId != "origin" && guard++ < 32)
        {
            var parent = speciesSet.FirstOrDefault(item => item.Id == current.ParentId);
            if (parent is null)
                break;
            current = parent;
        }
        return current;
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= prime;
        }
        return hash;
    }
}

public sealed partial class SpeciesPortrait : Control
{
    private SpeciesVisualDescriptor _descriptor;
    private bool _configured;

    public SpeciesPortrait()
    {
        CustomMinimumSize = new Vector2(64, 64);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Configure(DemoSpeciesRecord species, IReadOnlyList<DemoSpeciesRecord> speciesSet)
    {
        _descriptor = SpeciesVisualDescriptor.FromSpecies(species, speciesSet);
        _configured = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_configured || Size.X < 8 || Size.Y < 8)
            return;

        var center = Size * 0.5f;
        var radius = Math.Min(Size.X, Size.Y) * 0.42f;
        var accent = _descriptor.Kind switch
        {
            DemoSpeciesKind.Plant => EvolitPalette.YoungLeaf,
            DemoSpeciesKind.Creature => EvolitPalette.SoftAqua,
            _ => EvolitPalette.EvolutionCyan
        };

        if (_descriptor.Extinct)
            accent = new Color(EvolitPalette.WarmAlert, 0.72f);

        DrawCircle(center, radius, new Color(accent, 0.08f));
        DrawCircle(center, radius, new Color(accent, 0.34f), false, Math.Max(1f, radius * 0.035f));

        switch (_descriptor.Kind)
        {
            case DemoSpeciesKind.Plant:
                DrawPlant(center, radius, accent);
                break;
            case DemoSpeciesKind.Creature:
                DrawCreature(center, radius, accent);
                break;
            default:
                DrawOrigin(center, radius, accent);
                break;
        }

        if (_descriptor.Extinct)
        {
            var d = radius * 0.55f;
            DrawLine(center + new Vector2(-d, -d), center + new Vector2(d, d), new Color(EvolitPalette.WarmAlert, 0.86f), Math.Max(1.5f, radius * 0.055f), true);
        }
    }

    private void DrawCreature(Vector2 center, float radius, Color accent)
    {
        var family = Unit(_descriptor.FamilySeed, 3);
        var variant = Unit(_descriptor.VariantSeed, 9) - 0.5f;
        var width = radius * (1.08f + family * 0.24f + variant * 0.08f);
        var height = radius * (0.58f + Unit(_descriptor.FamilySeed, 7) * 0.22f - variant * 0.04f);
        var points = EllipsePoints(center, width, height, 14);
        DrawColoredPolygon(points, new Color(accent, _descriptor.Extinct ? 0.42f : 0.78f));
        DrawPolyline(Close(points), new Color(accent, 0.95f), Math.Max(1.2f, radius * 0.045f), true);

        var appendageCount = Unit(_descriptor.FamilySeed, 13) > 0.48f ? 4 : 2;
        for (var i = 0; i < appendageCount; i++)
        {
            var upper = i < appendageCount / 2;
            var slot = i % Math.Max(1, appendageCount / 2);
            var x = center.X - width * 0.25f + slot * width * 0.45f;
            var y = center.Y + (upper ? -height * 0.38f : height * 0.38f);
            var outward = upper ? -1f : 1f;
            var length = radius * (0.30f + Unit(_descriptor.VariantSeed, 17 + i) * 0.16f);
            DrawLine(new Vector2(x, y), new Vector2(x - radius * 0.08f, y + outward * length), new Color(accent, 0.80f), Math.Max(1.2f, radius * 0.038f), true);
        }

        var headX = center.X + width * 0.34f;
        DrawCircle(new Vector2(headX, center.Y - height * 0.10f), Math.Max(1.5f, radius * 0.065f), EvolitPalette.DeepNavyTeal);

        if (Unit(_descriptor.FamilySeed, 23) > 0.40f)
        {
            var tailStart = new Vector2(center.X - width * 0.50f, center.Y);
            var bend = (Unit(_descriptor.VariantSeed, 29) - 0.5f) * radius * 0.35f;
            DrawLine(tailStart, tailStart + new Vector2(-radius * 0.38f, bend), new Color(accent, 0.76f), Math.Max(1.2f, radius * 0.035f), true);
        }
    }

    private void DrawPlant(Vector2 center, float radius, Color accent)
    {
        var family = Unit(_descriptor.FamilySeed, 5);
        var variant = Unit(_descriptor.VariantSeed, 11) - 0.5f;
        var stemHeight = radius * (1.15f + family * 0.22f + variant * 0.10f);
        var stemBottom = center + new Vector2(0, stemHeight * 0.43f);
        var stemTop = center - new Vector2(0, stemHeight * 0.48f);
        DrawLine(stemBottom, stemTop, new Color(accent, 0.92f), Math.Max(1.8f, radius * 0.065f), true);

        var leafCount = 3 + (int)Math.Floor(Unit(_descriptor.FamilySeed, 19) * 3f);
        for (var i = 0; i < leafCount; i++)
        {
            var t = (i + 1f) / (leafCount + 1f);
            var y = Mathf.Lerp(stemBottom.Y, stemTop.Y, t);
            var side = i % 2 == 0 ? -1f : 1f;
            var spread = radius * (0.30f + Unit(_descriptor.VariantSeed, 31 + i) * 0.18f);
            var leafCenter = new Vector2(center.X + side * spread, y - radius * 0.04f);
            var leafRadius = radius * (0.16f + family * 0.05f);
            DrawCircle(leafCenter, leafRadius, new Color(accent, _descriptor.Extinct ? 0.34f : 0.72f));
            DrawLine(new Vector2(center.X, y), leafCenter, new Color(accent, 0.76f), Math.Max(1f, radius * 0.03f), true);
        }

        DrawCircle(stemTop, radius * (0.18f + Unit(_descriptor.FamilySeed, 37) * 0.07f), new Color(accent, _descriptor.Extinct ? 0.38f : 0.82f));
    }

    private static void DrawOrigin(Vector2 center, float radius, Color accent)
    {
        DrawCircle(center, radius * 0.32f, new Color(accent, 0.72f));
    }

    private static Vector2[] EllipsePoints(Vector2 center, float width, float height, int count)
    {
        var points = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.Tau * i / count;
            points[i] = center + new Vector2(Mathf.Cos(angle) * width * 0.5f, Mathf.Sin(angle) * height * 0.5f);
        }
        return points;
    }

    private static Vector2[] Close(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        Array.Copy(points, closed, points.Length);
        closed[^1] = points[0];
        return closed;
    }

    private static float Unit(uint seed, int salt)
    {
        var value = seed ^ ((uint)salt * 2654435761u);
        value ^= value >> 16;
        value *= 2246822519u;
        value ^= value >> 13;
        return (value & 0x00FFFFFF) / 16777215f;
    }
}
