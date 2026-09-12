using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RevitDevLoader.Infrastructure;

public static class RibbonIconFactory
{
    private static readonly Brush BrandBrush = CreateBrandBrush();
    private static readonly Brush OnBrandBrush = new SolidColorBrush(DSTokenColors.DSTextOnBrand);
    private static readonly Typeface IconTypeface = new("Segoe UI Semibold");

    public static ImageSource? LoadPackageIcon(string path, int size)
    {
        try
        {
            var smallPath = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "@16.png");
            if (size == 16 && File.Exists(smallPath))
                path = smallPath;
            if (!File.Exists(path))
                return null;
            using var stream = File.OpenRead(path);
            var bitmap = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            if (bitmap.PixelWidth != bitmap.PixelHeight || bitmap.PixelWidth < size)
                return null;
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static ImageSource CreateDevIcon()
    {
        return CreateTextIcon("DEV", 7.0);
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
