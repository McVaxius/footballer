using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;

namespace footballer.Windows;

internal enum UiFontRole { Body, BodyStrong, Title, PluginName, Counter, Action, CompactTitle }

internal static class FootballerPresentation
{
    // Footballer-main-v3: native chrome retained; measured content at 100%.
    internal const uint ReferenceAccent = 0xE96D9E;
    internal static bool Compact => MaterialTheme.Current.Density == MaterialDensity.Compact;
    internal static float HeaderHeight => Compact ? 77 : 96;
    internal static float ToolbarHeight => Compact ? 48 : 66;
    internal static float StatusHeight => Compact ? 42 : 52;
    internal static float CardHeight => Compact ? 404 : 592;
    internal static float CardWidth => Compact ? 330 : 357;
    internal static float CardGap => Compact ? 12 : 14;
    internal static float RegionGap => Compact ? 10 : 14;
    internal static float FaceSize => Compact ? 104 : 158;
    internal static float TitleScale => Compact ? 1.08f : 1.11f;
    internal static float SubtitleScale => Compact ? 1.33f : 1.15f;
    internal static float CardNameScale => Compact ? 1 : 1.17f;
    internal static readonly float[] FontSizes = [16, 16, 38, 20, 22, 18, 32];
    internal static readonly string[] FontFiles = ["segoeui.ttf", "seguisb.ttf", "segoeuib.ttf", "seguisb.ttf", "seguisb.ttf", "seguisb.ttf", "segoeuib.ttf"];
    internal static float AtlasHeight(UiFontRole role) => FontSizes[(int)role] * 4 / 3;
    internal static Vector4 Rgb(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
    internal static readonly Vector4 Ready = Rgb(0x7DCF6E), Pending = Rgb(0xE8B86C), Error = Rgb(0xEE7788);
    internal static MaterialControlMetrics Controls(float height, float icon = 20)
    {
        var s = MaterialTheme.Metrics.Scale;
        return new() { Height = height * s, Padding = new(12 * s, Math.Max(0, (height * s - ImGui.GetTextLineHeight()) * .5f)),
            Gap = 8 * s, IconSize = icon * s, Rounding = 4 * s, ItemSpacing = new(10 * s, 4 * s), CellPadding = new(16 * s, 8 * s) };
    }
    internal static MaterialTheme Theme(uint accent)
    {
        accent &= 0xFFFFFF;
        var reference = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new Vector3(Rgb(ReferenceAccent).X, Rgb(ReferenceAccent).Y, Rgb(ReferenceAccent).Z)));
        var selected = Rgb(accent);
        var seed = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(selected.X, selected.Y, selected.Z)));
        var hueShift = seed.Y < .001f ? 0 : seed.Z - reference.Z;
        var chromaScale = seed.Y < .001f ? 0 : seed.Y / reference.Y;
        Vector4 Relative(uint rgb)
        {
            var color = Rgb(rgb);
            if (accent == ReferenceAccent) return color;
            var lch = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(color.X, color.Y, color.Z)));
            return new(MaterialColor.GamutMap(lch.X, lch.Y * chromaScale, lch.Z + hueShift), 1);
        }
        var palette = new OklchPaletteGenerator().Generate(new(selected.X, selected.Y, selected.Z));
        var background = Relative(0x231A22);
        var foreground = Relative(0xF3EAF2);
        var primary = Relative(ReferenceAccent);
        var colors = new MaterialColorScheme(palette)
        {
            Background = background, OnBackground = foreground, Surface = Relative(0x281F27), OnSurface = foreground,
            SurfaceContainerLowest = Relative(0x211A24), SurfaceContainerLow = Relative(0x281F27),
            SurfaceContainer = Relative(0x2D232C), SurfaceContainerHigh = Relative(0x352630), SurfaceContainerHighest = Relative(0x3C2C37),
            SurfaceVariant = Relative(0x493A49), OnSurfaceVariant = Relative(0xC8B7C7),
            Outline = Relative(0x594654), OutlineVariant = Relative(0x493A49),
            Primary = primary, OnPrimary = MaterialColor.Contrast(primary, background) >= MaterialColor.Contrast(primary, foreground) ? background : foreground,
            PrimaryContainer = Relative(0x583045), OnPrimaryContainer = foreground,
            Secondary = Relative(0xD7A4C5), OnSecondary = background, SecondaryContainer = Relative(0x352630), OnSecondaryContainer = foreground,
            Tertiary = Relative(0xC9AEDB), OnTertiary = background, TertiaryContainer = Relative(0x3B2F49), OnTertiaryContainer = foreground,
            InverseSurface = foreground, InverseOnSurface = background, InversePrimary = Relative(0x843C5D),
        };
        return new(colors, MaterialDensity.Standard) { SurfaceOpacity = 1 };
    }
    internal static void Surface(Vector2 min, Vector2 max, bool raised = false)
    {
        var c = MaterialTheme.Current.Colors;
        MaterialCanvas.Surface(min, max, raised ? c.SurfaceContainerHigh : c.Surface, c.Background, 4 * MaterialTheme.Metrics.Scale);
        Dalamud.Bindings.ImGui.ImGui.GetWindowDrawList().AddRect(min, max, MaterialCanvas.Color(c.OutlineVariant), 4 * MaterialTheme.Metrics.Scale);
    }
    internal static void Footprint(Vector2 origin, float size, Vector4 color)
        => Footprint(origin, new Vector2(size), color);

    internal static void Footprint(Vector2 origin, Vector2 size, Vector4 color)
    {
        var dl = Dalamud.Bindings.ImGui.ImGui.GetWindowDrawList();
        var ink = MaterialCanvas.Color(color);
        Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;
        void Ellipse(float x, float y, float rx, float ry, float angle)
        {
            for (var i = 0; i < 32; i++)
            {
                var t = i * MathF.Tau / 32;
                var px = MathF.Cos(t) * rx;
                var py = MathF.Sin(t) * ry;
                dl.PathLineTo(P(x + px * MathF.Cos(angle) - py * MathF.Sin(angle), y + px * MathF.Sin(angle) + py * MathF.Cos(angle)));
            }
            dl.PathFillConvex(ink);
        }
        // Convex native meshes overlap to form the curved sole and graduated toes.
        Ellipse(.48f, .43f, .26f, .22f, -.28f);
        Ellipse(.36f, .67f, .15f, .28f, .38f);
        Ellipse(.26f, .85f, .16f, .105f, -.28f);
        Ellipse(.68f, .13f, .09f, .105f, -.22f);
        Ellipse(.48f, .14f, .067f, .078f, -.22f);
        Ellipse(.32f, .19f, .050f, .060f, -.22f);
        Ellipse(.21f, .28f, .038f, .046f, -.22f);
    }
    internal static void Shoe(Vector2 origin, float size, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var ink = MaterialCanvas.Color(color);
        Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;
        dl.AddQuadFilled(P(.08f, .64f), P(.94f, .5f), P(.94f, .7f), P(.12f, .78f), ink);
        dl.AddQuadFilled(P(.36f, .58f), P(.58f, .28f), P(.72f, .42f), P(.62f, .57f), ink);
        dl.AddQuadFilled(P(.62f, .42f), P(.92f, .38f), P(.94f, .56f), P(.62f, .58f), ink);
        var cut = MaterialCanvas.Color(MaterialTheme.Current.Colors.SurfaceContainerHigh);
        dl.AddLine(P(.32f, .57f), P(.43f, .65f), cut, Math.Max(1, size * .05f));
        dl.AddLine(P(.43f, .49f), P(.54f, .57f), cut, Math.Max(1, size * .05f));
        dl.AddLine(P(.12f, .72f), P(.54f, .72f), cut, Math.Max(1, size * .05f));
        dl.AddLine(P(.54f, .72f), P(.92f, .64f), cut, Math.Max(1, size * .05f));
    }
    internal static void Discord(Vector2 origin, float size, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var ink = MaterialCanvas.Color(color);
        Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;
        dl.AddRectFilled(P(.08f, .2f), P(.92f, .8f), ink, size * .18f);
        dl.AddQuadFilled(P(.14f, .26f), P(.3f, .13f), P(.4f, .18f), P(.22f, .47f), ink);
        dl.AddQuadFilled(P(.6f, .18f), P(.7f, .13f), P(.86f, .26f), P(.78f, .47f), ink);
        dl.AddQuadFilled(P(.08f, .57f), P(.37f, .7f), P(.31f, .9f), P(.04f, .78f), ink);
        dl.AddQuadFilled(P(.63f, .7f), P(.92f, .57f), P(.96f, .78f), P(.69f, .9f), ink);
        var cut = MaterialCanvas.Color(MaterialTheme.Current.Colors.Background);
        dl.AddCircleFilled(P(.34f, .5f), size * .075f, cut, 20);
        dl.AddCircleFilled(P(.66f, .5f), size * .075f, cut, 20);
    }
}
