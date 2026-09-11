using System.Windows.Media;

namespace RevitDevLoader.Infrastructure;

public static class DSTokenColors
{
    // Bitmap ribbon icons are outside the WPF resource tree. Raw colors are centralized here by token name.
    public static readonly Color DSActionPrimaryBg = FromHex("#075BFF");
    public static readonly Color DSBrandGradientStart = FromHex("#0A5BFF");
    public static readonly Color DSBrandGradientEnd = FromHex("#0B84FF");
    public static readonly Color DSIconDetail = FromHex("#E0E0E0");
    public static readonly Color DSTextSecondary = FromHex("#5A6878");
    public static readonly Color DSTextOnBrand = FromHex("#FFFFFF");

    private static Color FromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex);
    }
}
