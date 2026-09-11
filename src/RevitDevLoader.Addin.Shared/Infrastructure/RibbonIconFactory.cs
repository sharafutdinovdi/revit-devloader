using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace RevitDevLoader.Infrastructure;

public static class RibbonIconFactory
{
    private static readonly Brush BrandBrush = CreateBrandBrush();
    private static readonly Brush OnBrandBrush = new SolidColorBrush(DSTokenColors.DSTextOnBrand);
    private static readonly Typeface IconTypeface = new("Segoe UI Semibold");

    public static ImageSource CreateIcon()
    {
        return CreateDevIcon();
    }

    public static ImageSource CreateDevIcon()
    {
        return CreateTextIcon("DEV", 7.0);
    }

    public static ImageSource CreatePluginIcon(string text)
    {
        return CreateTextIcon(text, text.Length <= 2 ? 9.2 : 7.0);
    }

    public static ImageSource CreateCatalogIcon(string pluginId)
    {
        return CreateTile(context => CatalogIconDrawings.DrawPlugin(context, pluginId));
    }

    public static ImageSource CreateSettingsIcon()
    {
        return CreateServiceIcon(CatalogIconDrawings.DrawSettings);
    }

    public static ImageSource CreateJournalIcon()
    {
        return CreateServiceIcon(CatalogIconDrawings.DrawJournal);
    }

    private static ImageSource CreateTextIcon(string text, double emSize)
    {
        return CreateTile(context => DrawText(context, text, emSize));
    }

    private static ImageSource CreateTile(Action<DrawingContext> drawSign)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
            context.DrawRoundedRectangle(BrandBrush, null, new Rect(0, 0, 32, 32), 7, 7);
            drawSign(context);
        }

        group.Freeze();
        return new DrawingImage(group);
    }

    private static ImageSource CreateServiceIcon(Action<DrawingContext> drawSign)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
            drawSign(context);
        }

        group.Freeze();
        return new DrawingImage(group);
    }

    private static void DrawText(DrawingContext context, string text, double emSize)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            IconTypeface,
            emSize,
            OnBrandBrush,
            1.0)
        {
            TextAlignment = TextAlignment.Center
        };
        context.DrawText(formatted, new Point(16, 16 - formatted.Height / 2));
    }

    private static Brush CreateBrandBrush()
    {
        var brush = new LinearGradientBrush(
            DSTokenColors.DSBrandGradientStart,
            DSTokenColors.DSBrandGradientEnd,
            new Point(0, 1),
            new Point(1, 0));
        brush.Freeze();
        return brush;
    }
}
