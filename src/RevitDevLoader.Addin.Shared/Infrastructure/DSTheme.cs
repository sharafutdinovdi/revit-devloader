using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RevitDevLoader.Infrastructure;

public static class DSTheme
{
    private const bool PluginsUseLightTheme = true;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        if (window is null)
            return;

        var revitUsesDarkTheme = IsDarkTheme();
        var isDark = !PluginsUseLightTheme && revitUsesDarkTheme;

        try
        {
            ApplyTokenDictionary(window, isDark);
        }
        catch
        {
            if (isDark)
            {
                try
                {
                    ApplyTokenDictionary(window, false);
                }
                catch
                {
                    // The window keeps its default resources if token loading is unavailable.
                }
            }
        }

        ApplyTitleBar(window, isDark);
    }

    private static void ApplyTokenDictionary(Window window, bool isDark)
    {
        var assemblyName = window.GetType().Assembly.GetName().Name;
        var fileName = isDark ? "DSTokens.Dark.xaml" : "DSTokens.xaml";
        var source = new Uri(
            $"pack://application:,,,/{assemblyName};component/Resources/{fileName}",
            UriKind.Absolute);

        for (var index = window.Resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            var existingSource = window.Resources.MergedDictionaries[index].Source;
            if (existingSource is not null &&
                existingSource.OriginalString.IndexOf("/Resources/DSTokens", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                window.Resources.MergedDictionaries.RemoveAt(index);
            }
        }

        window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = source });
    }

    private static void ApplyTitleBar(Window window, bool isDark)
    {
        void Handler(object? sender, EventArgs e)
        {
            window.SourceInitialized -= Handler;
            TrySetImmersiveDarkMode(window, isDark);
        }

        window.SourceInitialized += Handler;
    }

    private static void TrySetImmersiveDarkMode(Window window, bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var enabled = isDark ? 1 : 0;
            var result = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
            if (result != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
        }
        catch
        {
            // The native title bar stays light on unsupported Windows builds.
        }
    }

    private static bool IsDarkTheme()
    {
        try
        {
            var themeManagerType = Type.GetType(
                "Autodesk.Revit.UI.UIThemeManager, RevitAPIUI",
                throwOnError: false);

            if (themeManagerType is null)
            {
                themeManagerType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("Autodesk.Revit.UI.UIThemeManager", throwOnError: false))
                    .FirstOrDefault(type => type is not null);
            }

            var themeProperty = themeManagerType?
                .GetProperty("CurrentTheme", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?? themeManagerType?
                    .GetProperty("CurrentCanvasTheme", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var currentTheme = themeProperty?.GetValue(null);

            return string.Equals(currentTheme?.ToString(), "Dark", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
