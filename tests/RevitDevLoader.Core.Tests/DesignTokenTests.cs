using System.Text.RegularExpressions;
using Xunit.Sdk;

namespace RevitDevLoader.Core.Tests;

public sealed class DesignTokenTests
{
    private const int ExpectedTokenCount = 45;

    private static readonly string[] BaseTokens =
    {
        "DSBackgroundApp",
        "DSBackgroundPanel",
        "DSBackgroundHeader",
        "DSBackgroundPressed",
        "DSBackgroundInput",
        "DSBackgroundHover",
        "DSBackgroundSelected",
        "DSTextPrimary",
        "DSTextSecondary",
        "DSTextAccent",
        "DSTextPlaceholder",
        "DSTextOnBrand",
        "DSBorder",
        "DSBorderInput",
        "DSBorderFocus",
        "DSActionPrimaryBg",
        "DSActionSecondaryBg",
        "DSActionSecondaryBorder",
        "DSStatusSuccess",
        "DSStatusWarning",
        "DSStatusError",
        "DSStatusSuccessBg",
        "DSStatusErrorBg"
    };

    private static readonly Regex TokenKeyPattern = new(
        "x:Key\\s*=\\s*\"(?<key>DS[^\"]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex DynamicResourcePattern = new(
        "\\{DynamicResource\\s+(?<key>DS[A-Za-z0-9_]+)\\s*\\}",
        RegexOptions.Compiled);

    private static readonly Regex ResourceApiPattern = new(
        "(?:SetResourceReference|FindResource)\\s*\\([^\\r\\n]*?\"(?<key>DS[^\"]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex DynamicExtensionPattern = new(
        "new\\s+DynamicResourceExtension\\s*\\(\\s*\"(?<key>DS[^\"]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex TokenLiteralPattern = new(
        "\"(?<key>DS(?:Background|Text|Border|Action|Status|Brand|Footer|Selected|Rule)[A-Za-z0-9_]*)\"",
        RegexOptions.Compiled);

    private static readonly Regex RawHexPattern = new(
        "#[0-9A-Fa-f]{6}(?![0-9A-Fa-f])",
        RegexOptions.Compiled);

    [Fact]
    public void AllDesignTokenReferencesResolve()
    {
        var pluginRoot = FindPluginRoot();
        var sourceRoot = Path.Combine(pluginRoot, "src");
        var tokenFiles = FindFiles(sourceRoot, "DSTokens.xaml").ToList();
        Assert.True(tokenFiles.Count > 0, $"No DSTokens.xaml files found under '{sourceRoot}'.");

        var declaredTokens = ReadTokenKeys(tokenFiles[0]);
        var viewFiles = FindViewXamlFiles(sourceRoot).ToList();
        var codeFiles = FindFiles(sourceRoot, "*.cs").ToList();
        Assert.True(viewFiles.Count + codeFiles.Count > 0, $"No UI source files found under '{sourceRoot}'.");

        var references = ReadReferences(pluginRoot, viewFiles, DynamicResourcePattern)
            .Concat(ReadReferences(pluginRoot, codeFiles, ResourceApiPattern, DynamicExtensionPattern, TokenLiteralPattern))
            .GroupBy(reference => (reference.File, reference.Line, reference.Key))
            .Select(group => group.First())
            .ToList();
        Assert.True(references.Count > 0, $"No DS token references found under '{sourceRoot}'.");

        var missing = references
            .Where(reference => !declaredTokens.Contains(reference.Key))
            .Select(reference => $"{reference.File}:{reference.Line}: undefined token '{reference.Key}'")
            .ToList();
        Assert.True(missing.Count == 0, "Undefined design tokens:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void LightAndDarkDictionariesMatchTheTokenContract()
    {
        var pluginRoot = FindPluginRoot();
        var sourceRoot = Path.Combine(pluginRoot, "src");
        var lightFiles = FindFiles(sourceRoot, "DSTokens.xaml").ToList();
        Assert.True(lightFiles.Count > 0, $"No DSTokens.xaml files found under '{sourceRoot}'.");

        HashSet<string>? canonicalKeys = null;
        foreach (var lightFile in lightFiles)
        {
            var darkFile = Path.Combine(Path.GetDirectoryName(lightFile)!, "DSTokens.Dark.xaml");
            Assert.True(File.Exists(darkFile), $"Dark token dictionary is missing next to '{Relative(pluginRoot, lightFile)}'.");

            var lightKeys = ReadTokenKeys(lightFile);
            var darkKeys = ReadTokenKeys(darkFile);
            Assert.Equal(ExpectedTokenCount, lightKeys.Count);
            Assert.Equal(ExpectedTokenCount, darkKeys.Count);
            Assert.True(lightKeys.SetEquals(darkKeys), DescribeSetDifference(pluginRoot, lightFile, darkFile, lightKeys, darkKeys));

            var missingBaseTokens = BaseTokens.Where(token => !lightKeys.Contains(token)).ToList();
            Assert.True(missingBaseTokens.Count == 0, $"{Relative(pluginRoot, lightFile)} is missing base tokens: {string.Join(", ", missingBaseTokens)}");

            canonicalKeys ??= lightKeys;
            Assert.True(canonicalKeys.SetEquals(lightKeys), $"{Relative(pluginRoot, lightFile)} does not match the plugin's canonical token key set.");
        }
    }

    [Fact]
    public void ViewXamlContainsNoRawHexColors()
    {
        var pluginRoot = FindPluginRoot();
        var sourceRoot = Path.Combine(pluginRoot, "src");
        var viewSourceFiles = FindFiles(sourceRoot, "*.*")
            .Where(file => IsInViewsDirectory(sourceRoot, file))
            .ToList();
        Assert.True(viewSourceFiles.Count > 0, $"No UI source files found in a Views folder under '{sourceRoot}'.");
        var viewFiles = viewSourceFiles
            .Where(file => file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var violations = new List<string>();
        foreach (var file in viewFiles)
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                foreach (Match match in RawHexPattern.Matches(line))
                    violations.Add($"{Relative(pluginRoot, file)}:{lineNumber}: raw color '{match.Value}'");
            }
        }

        Assert.True(violations.Count == 0, "Raw UI colors:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void ThemeUsesLightThemePolicy()
    {
        var pluginRoot = FindPluginRoot();
        var themeFiles = FindFiles(Path.Combine(pluginRoot, "src"), "DSTheme.cs").ToList();
        Assert.True(themeFiles.Count > 0, $"No DSTheme.cs files found under '{pluginRoot}'.");

        foreach (var file in themeFiles)
        {
            var source = File.ReadAllText(file);
            Assert.Contains("private const bool PluginsUseLightTheme = true;", source, StringComparison.Ordinal);
            Assert.Contains("!PluginsUseLightTheme && revitUsesDarkTheme", source, StringComparison.Ordinal);
            Assert.Contains("DSTokens.xaml", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PluginManagerUsesOnlySharedDesignTokens()
    {
        var pluginRoot = FindPluginRoot();
        var viewsRoot = Path.Combine(pluginRoot, "src", "RevitDevLoader.Addin.Shared", "Views");
        var sources = FindFiles(viewsRoot, "PluginManagerWindow*.cs")
            .Select(File.ReadAllText)
            .ToList();

        Assert.NotEmpty(sources);
        var combined = string.Join(Environment.NewLine, sources);
        Assert.DoesNotContain("ApplyPluginManagerPalette", combined, StringComparison.Ordinal);
        Assert.DoesNotMatch("PluginManager(?:Background|TextPrimary|TextSecondary|Accent|Danger)", combined);
    }

    [Fact]
    public void ManagerUsesThreeZonesAndBoundedSearch()
    {
        var pluginRoot = FindPluginRoot();
        var source = File.ReadAllText(Path.Combine(
            pluginRoot,
            "src", "RevitDevLoader.Addin.Shared",
            "Views",
            "PluginManagerWindow.cs"));
        var layoutStart = source.IndexOf("private UIElement BuildLayout()", StringComparison.Ordinal);
        var layoutEnd = source.IndexOf("private static string GetRevitMajorVersion", StringComparison.Ordinal);
        Assert.True(layoutStart >= 0 && layoutEnd > layoutStart);
        var layout = source[layoutStart..layoutEnd];
        Assert.Contains("Text = \"Search plugins\"", source, StringComparison.Ordinal);
        Assert.Contains("_searchBox.GotKeyboardFocus", layout, StringComparison.Ordinal);
        Assert.Contains("UpdateSearchPlaceholder()", layout, StringComparison.Ordinal);
        Assert.Contains("_searchBox.Width = 240;", layout, StringComparison.Ordinal);
        Assert.Contains("_searchBox.MaxWidth = 240;", layout, StringComparison.Ordinal);
        Assert.Contains("_searchBox.Height = 32;", layout, StringComparison.Ordinal);
        Assert.Contains("_searchBox.HorizontalAlignment = HorizontalAlignment.Left;", layout, StringComparison.Ordinal);
        Assert.Contains("_searchBox.HorizontalContentAlignment = HorizontalAlignment.Left;", layout, StringComparison.Ordinal);
        Assert.Contains("_searchBox.TextAlignment = TextAlignment.Left;", layout, StringComparison.Ordinal);
        Assert.Contains("_searchBox.Padding = new Thickness(32, 0, 32, 0);", layout, StringComparison.Ordinal);
        Assert.Contains("_searchPlaceholder.Margin = new Thickness(32, 0, 32, 0);", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("_searchContainer", layout, StringComparison.Ordinal);
        Assert.Contains("Padding = new Thickness(24, 12, 24, 12)", layout, StringComparison.Ordinal);
        Assert.Contains("BorderThickness = new Thickness(0, 0, 0, 1)", layout, StringComparison.Ordinal);
        var headerStart = source.IndexOf("private UIElement BuildHeader", StringComparison.Ordinal);
        var headerEnd = source.IndexOf("private UIElement BuildSearchControl", StringComparison.Ordinal);
        Assert.True(headerStart >= 0 && headerEnd > headerStart);
        var headerBody = source[headerStart..headerEnd];
        var middleStart = source.IndexOf("private UIElement BuildMiddleZone", StringComparison.Ordinal);
        var middleEnd = source.IndexOf("private void UpdateSearchPlaceholder", StringComparison.Ordinal);
        Assert.True(middleStart >= 0 && middleEnd > middleStart);
        var middleBody = source[middleStart..middleEnd];
        Assert.Contains("panel.Children.Add(BuildSearchControl());", headerBody, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(tools, 1);", headerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildSearchControl", middleBody, StringComparison.Ordinal);
        Assert.Contains("return BuildPluginList();", middleBody, StringComparison.Ordinal);
        var appToken = "DS" + "BackgroundApp";
        var headerToken = "DS" + "BackgroundHeader";
        Assert.Contains($"surface.SetResourceReference(BackgroundProperty, \"{headerToken}\")", layout, StringComparison.Ordinal);
        Assert.Contains($"pluginList.SetResourceReference(BackgroundProperty, \"{appToken}\")", layout, StringComparison.Ordinal);
        Assert.Contains($"refreshSurface.SetResourceReference(BackgroundProperty, \"{headerToken}\")", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Text = \"Plugins\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginAndServiceIconsUseDistinctRecipesAndButtonStates()
    {
        var pluginRoot = FindPluginRoot();
        var source = File.ReadAllText(Path.Combine(
            pluginRoot,
            "src", "RevitDevLoader.Addin.Shared",
            "Infrastructure",
            "RibbonIconFactory.cs"));
        var drawings = File.ReadAllText(Path.Combine(
            pluginRoot,
            "src", "RevitDevLoader.Addin.Shared",
            "Infrastructure",
            "CatalogIconDrawings.cs"));
        Assert.Contains("DrawFallback(context)", drawings, StringComparison.Ordinal);
        Assert.Contains("CreateTile(context => CatalogIconDrawings.DrawPlugin(context, pluginId))", source, StringComparison.Ordinal);
        Assert.Contains("CreateServiceIcon(CatalogIconDrawings.DrawSettings)", source, StringComparison.Ordinal);
        Assert.Contains("CreateServiceIcon(CatalogIconDrawings.DrawJournal)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTile(CatalogIconDrawings.DrawSettings)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTile(CatalogIconDrawings.DrawJournal)", source, StringComparison.Ordinal);
        Assert.Contains("context.DrawRoundedRectangle(BrandBrush", source, StringComparison.Ordinal);
        Assert.Contains("private static ImageSource CreateServiceIcon", source, StringComparison.Ordinal);
        Assert.Contains("D" + "SBrandGradientStart", source, StringComparison.Ordinal);
        Assert.Contains("D" + "SBrandGradientEnd", source, StringComparison.Ordinal);
        Assert.Contains("new LinearGradientBrush(", source, StringComparison.Ordinal);
        Assert.Contains("new Point(0, 1)", source, StringComparison.Ordinal);
        Assert.Contains("new Point(1, 0)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static readonly Brush BrandBrush = new SolidColorBrush", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Segoe MDL2 Assets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLibraryGlyph", drawings, StringComparison.Ordinal);

        var controls = File.ReadAllText(Path.Combine(pluginRoot, "src", "RevitDevLoader.Addin.Shared", "Views", "PluginManagerWindow.Controls.cs"));
        Assert.Contains("button.Width = 32;", controls, StringComparison.Ordinal);
        Assert.Contains("RibbonIconFactory.CreateSettingsIcon()", controls, StringComparison.Ordinal);
        Assert.Contains("RibbonIconFactory.CreateJournalIcon()", controls, StringComparison.Ordinal);
        Assert.Contains("button.Background = Brushes.Transparent;", controls, StringComparison.Ordinal);
        Assert.Contains("button.BorderBrush = Brushes.Transparent;", controls, StringComparison.Ordinal);
        Assert.Contains("button.BorderThickness = new Thickness(1);", controls, StringComparison.Ordinal);
        Assert.Contains("new CornerRadius(7)", controls, StringComparison.Ordinal);
        Assert.Contains("UIElement.IsMouseOverProperty, true, \"DSBackgroundHeader\", \"DSBackgroundHeader\"", controls, StringComparison.Ordinal);
        Assert.Contains("ButtonBase.IsPressedProperty, true, \"DSBorder\", \"DSBorder\"", controls, StringComparison.Ordinal);
        Assert.DoesNotContain("hoverOverlay", controls, StringComparison.Ordinal);
        Assert.Contains("CreateSearchIcon()", controls, StringComparison.Ordinal);
        Assert.DoesNotContain("Segoe MDL2 Assets", controls, StringComparison.Ordinal);
    }

    [Fact]
    public void ConventionalInstallUsesSecondaryVectorLockWithoutActionGlyph()
    {
        var pluginRoot = FindPluginRoot();
        var source = File.ReadAllText(Path.Combine(
            pluginRoot,
            "src", "RevitDevLoader.Addin.Shared",
            "Views",
            "PluginManagerWindow.Cards.cs"));

        var secondaryToken = "DS" + "TextSecondary";
        Assert.Contains($"lockGlyph.SetResourceReference(Shape.StrokeProperty, \"{secondaryToken}\")", source, StringComparison.Ordinal);
        Assert.Contains("Data = Geometry.Parse(\"M6,9", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Segoe MDL2 Assets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PluginManagerStatusIcon.ConventionalInstall => \"DSStatusError\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PluginManagerStatusIcon.ConventionalInstall => \"M ", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginRowsUseFeedIconsAndMousePointMenus()
    {
        var pluginRoot = FindPluginRoot();
        var source = File.ReadAllText(Path.Combine(
            pluginRoot,
            "src", "RevitDevLoader.Addin.Shared",
            "Views",
            "PluginManagerWindow.Cards.cs"));

        Assert.Contains("DevPluginIconFallback.GetColor(status.Plugin.PluginId)", source, StringComparison.Ordinal);
        Assert.Contains("rowState.StatusIcon == PluginManagerStatusIcon.None", source, StringComparison.Ordinal);
        Assert.Contains("return null;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Plugin.IconText", source, StringComparison.Ordinal);
        Assert.Contains("border.MouseLeftButtonUp", source, StringComparison.Ordinal);
        Assert.Contains("OpenRowMenu(border, status);", source, StringComparison.Ordinal);
        Assert.Contains("Placement = PlacementMode.MousePoint", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RestartNotationsAreNotRenderedInManagerMessages()
    {
        var pluginRoot = FindPluginRoot();
        var viewsRoot = Path.Combine(pluginRoot, "src", "RevitDevLoader.Addin.Shared", "Views");
        var sources = FindFiles(viewsRoot, "PluginManager*.cs")
            .Select(File.ReadAllText)
            .ToList();
        var combined = string.Join(Environment.NewLine, sources);

        Assert.DoesNotContain("Installed. Restart Revit to load the plugin.", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Files are installed, but the ribbon button is missing. Restart Revit.", combined, StringComparison.Ordinal);
    }

    private static string FindPluginRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;
        }

        throw new XunitException($"Could not find a plugin root containing 'src' above '{AppContext.BaseDirectory}'.");
    }

    private static IEnumerable<string> FindFiles(string root, string pattern)
    {
        return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(file => !HasIgnoredDirectory(root, file));
    }

    private static IEnumerable<string> FindViewXamlFiles(string sourceRoot)
    {
        return FindFiles(sourceRoot, "*.xaml")
            .Where(file => IsInViewsDirectory(sourceRoot, file));
    }

    private static bool IsInViewsDirectory(string sourceRoot, string file)
    {
        return Relative(sourceRoot, file)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains("Views", StringComparer.Ordinal);
    }

    private static bool HasIgnoredDirectory(string root, string file)
    {
        return Relative(root, file)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> ReadTokenKeys(string file)
    {
        return TokenKeyPattern.Matches(File.ReadAllText(file))
            .Cast<Match>()
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string ReadMethodBody(string source, string methodName)
    {
        var start = source.IndexOf($"static void {methodName}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method '{methodName}' was not found.");
        var nextMethod = source.IndexOf("static void ", start + methodName.Length, StringComparison.Ordinal);
        return source[start..(nextMethod >= 0 ? nextMethod : source.Length)];
    }

    private static IEnumerable<TokenReference> ReadReferences(string pluginRoot, IEnumerable<string> files, params Regex[] patterns)
    {
        foreach (var file in files)
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                foreach (var pattern in patterns)
                foreach (Match match in pattern.Matches(line))
                    yield return new TokenReference(Relative(pluginRoot, file), lineNumber, match.Groups["key"].Value);
            }
        }
    }

    private static Dictionary<string, string> ComparableFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !HasIgnoredDirectory(root, file))
            .Where(file => !file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.EndsWith(".addin", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(file => Relative(root, file), file => file, StringComparer.Ordinal);
    }

    private static string DescribeSetDifference(
        string pluginRoot,
        string lightFile,
        string darkFile,
        HashSet<string> lightKeys,
        HashSet<string> darkKeys)
    {
        var lightOnly = lightKeys.Except(darkKeys).OrderBy(key => key);
        var darkOnly = darkKeys.Except(lightKeys).OrderBy(key => key);
        return $"Token key mismatch between '{Relative(pluginRoot, lightFile)}' and '{Relative(pluginRoot, darkFile)}'. " +
            $"Light only: [{string.Join(", ", lightOnly)}]. Dark only: [{string.Join(", ", darkOnly)}].";
    }

    private static string Relative(string root, string file)
    {
        return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
    }

    private sealed record TokenReference(string File, int Line, string Key);
}
