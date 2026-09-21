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
    bool Extinct);

public static class SpeciesVisualFactory
{
    public static SpeciesVisualDescriptor FromSpecies(
        DemoSpeciesRecord species,
        IReadOnlyList<DemoSpeciesRecord> speciesSet)
    {
        var family = FindFamily(species, speciesSet);
        return new SpeciesVisualDescriptor(
            species.Kind,
            StableSeed(family.Id),
            StableSeed(species.Id),
            species.Status == DemoSpeciesStatus.Extinct);
    }

    public static SpeciesVisualDescriptor Preview(
        DemoSpeciesKind kind,
        uint familySeed,
        uint variantSeed,
        bool extinct = false)
    {
        return new SpeciesVisualDescriptor(kind, familySeed, variantSeed, extinct);
    }

    public static uint StableSeed(string value)
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

    private static DemoSpeciesRecord FindFamily(
        DemoSpeciesRecord species,
        IReadOnlyList<DemoSpeciesRecord> speciesSet)
    {
        var current = species;
        var guard = 0;
        while (!string.IsNullOrWhiteSpace(current.ParentId)
               && current.ParentId != "origin"
               && guard++ < 32)
        {
            var parent = speciesSet.FirstOrDefault(item => item.Id == current.ParentId);
            if (parent is null)
                break;
            current = parent;
        }

        return current;
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
        Resized += QueueRedraw;
    }

    public void Configure(
        DemoSpeciesRecord species,
        IReadOnlyList<DemoSpeciesRecord> speciesSet)
    {
        Configure(SpeciesVisualFactory.FromSpecies(species, speciesSet));
    }

    public void Configure(SpeciesVisualDescriptor descriptor)
    {
        _descriptor = descriptor;
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

        DrawCircle(center, radius, new Color(accent, 0.07f));
        DrawCircle(
            center,
            radius,
            new Color(accent, 0.34f),
            false,
            Math.Max(1f, radius * 0.035f));

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
            DrawLine(
                center + new Vector2(-d, -d),
                center + new Vector2(d, d),
                new Color(EvolitPalette.WarmAlert, 0.86f),
                Math.Max(1.5f, radius * 0.055f),
                true);
        }
    }

    private void DrawCreature(Vector2 center, float radius, Color accent)
    {
        var familyWide = Unit(_descriptor.FamilySeed, 3);
        var familyThick = Unit(_descriptor.FamilySeed, 7);
        var variant = Unit(_descriptor.VariantSeed, 11) - 0.5f;

        // The family controls the recognisable silhouette; the concrete species
        // only nudges it. This keeps related forms visually related.
        var width = radius * (0.86f + familyWide * 1.02f + variant * 0.16f);
        var height = radius * (0.48f + familyThick * 0.72f - variant * 0.10f);
        var shapeMode = Math.Min(2, (int)(Unit(_descriptor.FamilySeed, 13) * 3f));
        var points = CreatureBodyPoints(center, width, height, 18, shapeMode);

        DrawColoredPolygon(
            points,
            new Color(accent, _descriptor.Extinct ? 0.40f : 0.78f));
        DrawPolyline(
            Close(points),
            new Color(accent, 0.94f),
            Math.Max(1.2f, radius * 0.045f),
            true);

        var appendageCount = 2 + 2 * Math.Min(2, (int)(Unit(_descriptor.FamilySeed, 17) * 3f));
        var half = Math.Max(1, appendageCount / 2);
        for (var i = 0; i < appendageCount; i++)
        {
            var upper = i < half;
            var slot = i % half;
            var t = (slot + 1f) / (half + 1f);
            var x = Mathf.Lerp(center.X - width * 0.34f, center.X + width * 0.20f, t);
            var y = center.Y + (upper ? -height * 0.34f : height * 0.34f);
            var outward = upper ? -1f : 1f;
            var length = radius * (0.22f + Unit(_descriptor.VariantSeed, 23 + i) * 0.28f);
            var sweep = (Unit(_descriptor.FamilySeed, 31 + i) - 0.5f) * radius * 0.20f;

            DrawLine(
                new Vector2(x, y),
                new Vector2(x + sweep, y + outward * length),
                new Color(accent, 0.78f),
                Math.Max(1.1f, radius * 0.036f),
                true);
        }

        var headX = center.X + width * 0.34f;
        var eyeRadius = Math.Max(1.5f, radius * (0.052f + Unit(_descriptor.VariantSeed, 41) * 0.025f));
        DrawCircle(
            new Vector2(headX, center.Y - height * 0.10f),
            eyeRadius,
            EvolitPalette.DeepNavyTeal);

        var tailChance = Unit(_descriptor.FamilySeed, 43);
        if (tailChance > 0.26f)
        {
            var tailStart = new Vector2(center.X - width * 0.48f, center.Y);
            var bend = (Unit(_descriptor.VariantSeed, 47) - 0.5f) * radius * 0.50f;
            var tailLength = radius * (0.24f + tailChance * 0.42f);
            DrawLine(
                tailStart,
                tailStart + new Vector2(-tailLength, bend),
                new Color(accent, 0.74f),
                Math.Max(1.1f, radius * 0.034f),
                true);
        }

        var ridge = Math.Min(4, (int)(Unit(_descriptor.FamilySeed, 53) * 5f));
        for (var i = 0; i < ridge; i++)
        {
            var t = (i + 1f) / (ridge + 1f);
            var x = Mathf.Lerp(center.X - width * 0.27f, center.X + width * 0.20f, t);
            var baseY = center.Y - height * 0.43f;
            var spike = radius * (0.08f + Unit(_descriptor.VariantSeed, 59 + i) * 0.10f);
            DrawLine(
                new Vector2(x, baseY),
                new Vector2(x, baseY - spike),
                new Color(accent, 0.58f),
                Math.Max(1f, radius * 0.025f),
                true);
        }
    }

    private void DrawPlant(Vector2 center, float radius, Color accent)
    {
        var familyHeight = Unit(_descriptor.FamilySeed, 5);
        var familySpread = Unit(_descriptor.FamilySeed, 9);
        var variant = Unit(_descriptor.VariantSeed, 15) - 0.5f;

        var stemHeight = radius * (0.82f + familyHeight * 0.88f + variant * 0.12f);
        var stemBottom = center + new Vector2(0, stemHeight * 0.42f);
        var stemTop = center - new Vector2(0, stemHeight * 0.48f);
        var stemWidth = Math.Max(1.6f, radius * (0.045f + familySpread * 0.035f));

        DrawLine(
            stemBottom,
            stemTop,
            new Color(accent, 0.92f),
            stemWidth,
            true);

        var leafCount = 2 + Math.Min(5, (int)(Unit(_descriptor.FamilySeed, 19) * 6f));
        for (var i = 0; i < leafCount; i++)
        {
            var t = (i + 1f) / (leafCount + 1f);
            var y = Mathf.Lerp(stemBottom.Y, stemTop.Y, t);
            var side = i % 2 == 0 ? -1f : 1f;
            var spread = radius * (0.18f + familySpread * 0.30f + Unit(_descriptor.VariantSeed, 31 + i) * 0.12f);
            var vertical = (Unit(_descriptor.FamilySeed, 41 + i) - 0.5f) * radius * 0.14f;
            var leafCenter = new Vector2(center.X + side * spread, y + vertical);
            var leafRadius = radius * (0.11f + Unit(_descriptor.FamilySeed, 47 + i) * 0.12f);

            DrawLine(
                new Vector2(center.X, y),
                leafCenter,
                new Color(accent, 0.72f),
                Math.Max(1f, radius * 0.028f),
                true);
            DrawCircle(
                leafCenter,
                leafRadius,
                new Color(accent, _descriptor.Extinct ? 0.32f : 0.70f));
        }

        var crownMode = Math.Min(2, (int)(Unit(_descriptor.FamilySeed, 61) * 3f));
        switch (crownMode)
        {
            case 0:
                DrawCircle(
                    stemTop,
                    radius * (0.13f + Unit(_descriptor.VariantSeed, 67) * 0.10f),
                    new Color(accent, _descriptor.Extinct ? 0.36f : 0.82f));
                break;
            case 1:
                for (var i = -1; i <= 1; i++)
                {
                    DrawCircle(
                        stemTop + new Vector2(i * radius * 0.16f, Math.Abs(i) * radius * 0.05f),
                        radius * 0.11f,
                        new Color(accent, _descriptor.Extinct ? 0.34f : 0.76f));
                }
                break;
            default:
                var crownHeight = radius * 0.34f;
                DrawLine(
                    stemTop,
                    stemTop - new Vector2(0, crownHeight),
                    new Color(accent, 0.68f),
                    Math.Max(1f, radius * 0.03f),
                    true);
                DrawCircle(
                    stemTop - new Vector2(0, crownHeight),
                    radius * 0.10f,
                    new Color(accent, _descriptor.Extinct ? 0.34f : 0.80f));
                break;
        }
    }

    private static void DrawOrigin(Vector2 center, float radius, Color accent)
    {
        DrawCircle(center, radius * 0.32f, new Color(accent, 0.72f));
    }

    private static Vector2[] CreatureBodyPoints(
        Vector2 center,
        float width,
        float height,
        int count,
        int shapeMode)
    {
        var points = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.Tau * i / count;
            var x = Mathf.Cos(angle);
            var y = Mathf.Sin(angle);
            var modifier = shapeMode switch
            {
                1 => 0.83f + 0.17f * (x + 1f) * 0.5f,
                2 => 0.90f + 0.10f * Mathf.Sin(angle * 3f),
                _ => 1f
            };
            points[i] = center + new Vector2(
                x * width * 0.5f * modifier,
                y * height * 0.5f * modifier);
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
