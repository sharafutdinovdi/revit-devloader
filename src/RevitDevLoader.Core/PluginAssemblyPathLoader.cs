using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace RevitDevLoader.Core;

public sealed class PluginAssemblyPathLoader
{
    private readonly string _pluginDirectory;
    private readonly Action<string>? _logInfo;
    private readonly Action<string, Exception>? _logError;
    private readonly IReadOnlyList<string>? _conventionalAddinsDirectories;
    private readonly HashSet<string> _warnedConventionalAddins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _loadedByName = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

    public PluginAssemblyPathLoader(
        string pluginDirectory,
        Action<string>? logInfo = null,
        Action<string, Exception>? logError = null,
        IEnumerable<string>? conventionalAddinsDirectories = null)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            throw new ArgumentException("Plugin directory is required.", nameof(pluginDirectory));

        _pluginDirectory = Path.GetFullPath(pluginDirectory);
        _logInfo = logInfo;
        _logError = logError;
        _conventionalAddinsDirectories = conventionalAddinsDirectories?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Assembly LoadMainAssembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));

        WarnAboutConventionalInstalls(assemblyPath);
        return LoadFromPath(assemblyPath);
    }

    public Assembly? Resolve(ResolveEventArgs args)
    {
        if (args is null)
            throw new ArgumentNullException(nameof(args));

        var assemblyName = new AssemblyName(args.Name).Name;
        if (string.IsNullOrWhiteSpace(assemblyName) || assemblyName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return null;

        var candidate = Path.Combine(_pluginDirectory, assemblyName + ".dll");
        if (!File.Exists(candidate))
            return null;

        try
        {
            _logInfo?.Invoke($"Resolving dependency '{args.Name}' from '{candidate}'.");
            return LoadFromPath(candidate);
        }
        catch (Exception exception)
        {
            _logError?.Invoke($"Failed to resolve dependency '{args.Name}' from '{candidate}'.", exception);
            return null;
        }
    }

    public static ConventionalPluginInstall? FindConventionalInstall(
        string addinsDirectory,
        string pluginName,
        string mainAssembly)
    {
        if (string.IsNullOrWhiteSpace(addinsDirectory))
            throw new ArgumentException("Add-ins directory is required.", nameof(addinsDirectory));
        if (string.IsNullOrWhiteSpace(pluginName))
            throw new ArgumentException("Plugin name is required.", nameof(pluginName));
        if (string.IsNullOrWhiteSpace(mainAssembly))
            throw new ArgumentException("Main assembly is required.", nameof(mainAssembly));
        return FindConventionalInstalls(new[] { addinsDirectory }, mainAssembly).FirstOrDefault();
    }

    public static IReadOnlyList<ConventionalPluginInstall> FindConventionalInstalls(
        IEnumerable<string> addinsDirectories,
        string mainAssembly)
    {
        if (addinsDirectories is null)
            throw new ArgumentNullException(nameof(addinsDirectories));
        if (string.IsNullOrWhiteSpace(mainAssembly))
            throw new ArgumentException("Main assembly is required.", nameof(mainAssembly));

        var normalizedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in addinsDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                normalizedDirectories.Add(Path.GetFullPath(path));
            }
            catch
            {
            }
        }

        var matches = new List<ConventionalPluginInstall>();
        foreach (var addinsDirectory in normalizedDirectories)
        {
            if (!Directory.Exists(addinsDirectory))
                continue;

            IReadOnlyList<string> addinPaths;
            try
            {
                addinPaths = Directory.EnumerateFiles(addinsDirectory, "*.addin", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                continue;
            }

            foreach (var addinPath in addinPaths)
            {
                try
                {
                    var document = XDocument.Load(addinPath);
                    var addinName = document.Descendants()
                        .Where(element => element.Name.LocalName == "Name")
                        .Select(element => element.Value.Trim())
                        .FirstOrDefault(value => value.Length > 0) ?? string.Empty;
                    var matchedAssembly = document.Descendants()
                        .Where(element => element.Name.LocalName == "Assembly")
                        .Select(element => element.Value.Trim())
                        .FirstOrDefault(value => string.Equals(
                            GetPortableFileName(value),
                            mainAssembly,
                            StringComparison.OrdinalIgnoreCase));
                    if (matchedAssembly is not null)
                        matches.Add(new ConventionalPluginInstall(addinPath, addinName, matchedAssembly));
                }
                catch
                {
                }
            }
        }

        return matches;
    }

    public static IReadOnlyList<string> GetConventionalAddinsDirectories(
        string revitVersion,
        string? applicationDataRoot = null,
        string? commonApplicationDataRoot = null)
    {
        if (string.IsNullOrWhiteSpace(revitVersion))
            return Array.Empty<string>();

        var roots = new[]
        {
            applicationDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            commonApplicationDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };
        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.Combine(root, "Autodesk", "Revit", "Addins", revitVersion.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void WarnAboutConventionalInstalls(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var addinsDirectories = _conventionalAddinsDirectories ??
            GetConventionalAddinsDirectories(GetRevitVersionFromAssemblyPath(fullPath));
        try
        {
            foreach (var match in FindConventionalInstalls(addinsDirectories, Path.GetFileName(fullPath)))
            {
                if (PathsEqual(match.AssemblyPath, fullPath) || !_warnedConventionalAddins.Add(match.AddinPath))
                    continue;

                _logInfo?.Invoke(
                    $"WARNING: Conventional Revit add-in uses assembly '{match.AssemblyPath}'. " +
                    $"AddinPath='{match.AddinPath}'. DevLoader assembly load will continue.");
            }
        }
        catch (Exception exception)
        {
            _logError?.Invoke("Failed to inspect conventional Revit add-ins. DevLoader assembly load will continue.", exception);
        }
    }

    private Assembly LoadFromPath(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Plugin assembly not found.", fullPath);

        var simpleName = AssemblyName.GetAssemblyName(fullPath).Name;
        if (string.IsNullOrWhiteSpace(simpleName))
            simpleName = Path.GetFileNameWithoutExtension(fullPath);

        if (_loadedByName.TryGetValue(simpleName, out var loadedAssembly))
            return loadedAssembly;

        var assembly = Assembly.LoadFile(fullPath);
        _loadedByName[simpleName] = assembly;
        return assembly;
    }

    private static string GetPortableFileName(string path)
    {
        return path.Replace('\\', '/').Split('/').LastOrDefault() ?? string.Empty;
    }

    private static string GetRevitVersionFromAssemblyPath(string assemblyPath)
    {
        var directory = Path.GetDirectoryName(assemblyPath);
        var candidate = string.IsNullOrWhiteSpace(directory)
            ? string.Empty
            : new DirectoryInfo(directory).Name;
        return candidate.Length == 4 && candidate.All(char.IsDigit) ? candidate : string.Empty;
    }

    private static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            return false;

        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

public sealed class ConventionalPluginInstall
{
    public ConventionalPluginInstall(string addinPath, string addinName, string assemblyPath)
    {
        AddinPath = addinPath;
        AddinName = addinName;
        AssemblyPath = assemblyPath;
    }

    public string AddinPath { get; }

    public string AddinName { get; }

    public string AssemblyPath { get; }
}
