// Settings and journal geometry is derived from Lucide. See THIRD-PARTY-NOTICES.md.
using System.Windows;
using System.Windows.Media;

namespace RevitDevLoader.Infrastructure;

internal static class CatalogIconDrawings
{
    private static readonly Brush WhiteBrush = FrozenBrush(DSTokenColors.DSTextOnBrand);
    private static readonly Brush AccentBrush = FrozenBrush(DSTokenColors.DSActionPrimaryBg);
    private static readonly Pen LucidePen = CreatePen(WhiteBrush, 2);
    private static readonly Pen ServiceLucidePen = CreatePen(AccentBrush, 2);

    internal static void DrawPlugin(DrawingContext context, string? pluginId)
    {
        DrawFallback(context);
    }

    internal static void DrawSettings(DrawingContext context)
    {
        DrawServiceLucide(context,
            "M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915 M12 9 a3 3 0 1 0 0 6 a3 3 0 0 0 0-6 Z");
    }

    internal static void DrawJournal(DrawingContext context)
    {
        DrawServiceLucide(context,
            "M3 5h1 M3 12h1 M3 19h1 M8 5h1 M8 12h1 M8 19h1 M13 5h8 M13 12h8 M13 19h8");
    }

    private static void DrawFallback(DrawingContext context)
    {
        context.DrawRoundedRectangle(null, LucidePen, new Rect(7, 7, 18, 18), 3, 3);
        context.DrawGeometry(null, LucidePen, Geometry.Parse("M7,13 L25,13 M11,18 L21,18 M11,22 L18,22"));
    }

    private static void DrawServiceLucide(DrawingContext context, string path)
    {
        context.PushTransform(new TranslateTransform(4, 4));
        context.DrawGeometry(null, ServiceLucidePen, Geometry.Parse(path));
        context.Pop();
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }
}
