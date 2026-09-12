using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RevitDevLoader.Core;

public sealed class DevPluginStatusService
{
    public const string ConventionalInstallWarningPrefix = "Conventional installation found:";

    private readonly DevPluginRegistry _registry;
    private readonly ILogger<DevPluginStatusService> _logger;

    public DevPluginStatusService(DevPluginRegistry registry, ILogger<DevPluginStatusService>? logger = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? NullLogger<DevPluginStatusService>.Instance;
    }

    public IReadOnlyList<DevPluginStatus> BuildStatuses(
        IEnumerable<DevPluginCatalogItem> catalog,
        IEnumerable<DevPayloadPackageInfo> packages,
        string revitVersion,
        string? revitAddinsDirectory = null)
    {
        _logger.LogInformation("BuildStatuses started. RevitVersion='{RevitVersion}'.", revitVersion);
        if (catalog is null)
            throw new ArgumentNullException(nameof(catalog));
        if (packages is null)
            throw new ArgumentNullException(nameof(packages));

        var packageList = packages.ToList();
        var slotErrors = _registry.EnsureCommandSlots();
        foreach (var slotError in slotErrors)
            _logger.LogWarning("Command slot assignment skipped. Error='{Error}'.", slotError);

        var packageByPlugin = packageList
            .GroupBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => SelectBestPackage(group, revitVersion),
                StringComparer.OrdinalIgnoreCase);

        var addinsDirectory = revitAddinsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "Revit",
            "Addins",
            revitVersion);
        var statuses = BuildEffectiveCatalog(catalog, packageList, revitVersion)
            .Select(plugin => BuildStatus(plugin, packageByPlugin, revitVersion, addinsDirectory))
            .ToList();
        _logger.LogInformation("BuildStatuses completed. StatusCount={StatusCount}. RunnableCount={RunnableCount}.", statuses.Count, statuses.Count(item => item.CanRun));
        return statuses;
    }

    private DevPluginStatus BuildStatus(
        DevPluginCatalogItem plugin,
        IReadOnlyDictionary<string, DevPayloadPackageInfo> packageByPlugin,
        string revitVersion,
        string addinsDirectory)
    {
        var installed = TryLoad(plugin.PluginId, out var manifestError);
        packageByPlugin.TryGetValue(plugin.PluginId, out var available);
        DevPluginStatus status;

        if (manifestError is not null)
        {
            status = new DevPluginStatus(
                plugin,
                revitVersion,
                null,
                available,
                DevPluginStatusKind.PackageError,
                manifestError);
        }
        else if (!plugin.SupportsVersion(revitVersion))
        {
            status = new DevPluginStatus(
                plugin,
                revitVersion,
                installed,
                available,
                DevPluginStatusKind.UnsupportedRevitVersion,
                $"The plugin does not support Revit {revitVersion}.");
        }
        else if (available is not null && !available.SupportsVersion(revitVersion))
        {
            status = new DevPluginStatus(
                plugin,
                revitVersion,
                installed,
                available,
                DevPluginStatusKind.UnsupportedRevitVersion,
                $"The package has no assembly for Revit {revitVersion}.");
        }
        else if (installed is null)
        {
            if (available is { PluginType: DevPluginType.Command } &&
                !_registry.TryGetCommandSlot(plugin.PluginId, out _, out var slotError))
            {
                status = new DevPluginStatus(
                    plugin,
                    revitVersion,
                    null,
                    available,
                    DevPluginStatusKind.CommandSlotUnavailable,
                    slotError);
            }
            else
            {
                status = new DevPluginStatus(
                    plugin,
                    revitVersion,
                    null,
                    available,
                    DevPluginStatusKind.NotInstalled,
                    available is null ? "Update package not found." : "The plugin can be installed.");
            }
        }
        else if (installed.PluginType == DevPluginType.Command && !installed.CommandSlot.HasValue)
        {
            _registry.TryGetCommandSlot(plugin.PluginId, out _, out var slotError);
            status = new DevPluginStatus(
                plugin,
                revitVersion,
                installed,
                available,
                DevPluginStatusKind.CommandSlotUnavailable,
                string.IsNullOrWhiteSpace(slotError)
                    ? "The plugin has no DevLoader command slot assigned."
                    : slotError);
        }
        else if (available is null)
        {
            status = new DevPluginStatus(
                plugin,
                revitVersion,
                installed,
                null,
                DevPluginStatusKind.InstalledNoPackage,
                "The plugin is installed. No package is available in the updates folder for comparison.");
        }
        else if (CompareVersions(available.ReleaseId, available.AssemblyVersion,
                     installed.ReleaseId, installed.AssemblyVersion) > 0)
        {
            status = new DevPluginStatus(
                plugin,
                revitVersion,
                installed,
                available,
                DevPluginStatusKind.UpdateAvailable,
                "A new version is available.");
        }
        else
        {
            status = new DevPluginStatus(
                plugin,
                revitVersion,
                installed,
                available,
                DevPluginStatusKind.Latest,
                "The latest version is installed.");
        }

        return AddConventionalInstallWarning(status, addinsDirectory);
    }

    private IReadOnlyList<DevPluginCatalogItem> BuildEffectiveCatalog(
        IEnumerable<DevPluginCatalogItem> catalog,
        IReadOnlyList<DevPayloadPackageInfo> packages,
        string revitVersion)
    {
        var result = catalog.ToList();
        var pluginIds = new HashSet<string>(result.Select(item => item.PluginId), StringComparer.OrdinalIgnoreCase);
        foreach (var packageGroup in packages.GroupBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase))
        {
            if (!pluginIds.Add(packageGroup.Key))
                continue;

            var package = packageGroup.OrderByDescending(item => item.CreatedUtc).First();
            result.Add(new DevPluginCatalogItem(
                package.PluginId,
                package.DisplayName,
                package.CommandType,
                package.MainAssembly,
                packageGroup.SelectMany(item => item.Versions),
                string.Empty,
                package.PluginType,
                package.ApplicationClass));
        }

        foreach (var pluginId in _registry.GetRegisteredPluginNames())
        {
            if (!pluginIds.Add(pluginId))
                continue;

            try
            {
                var manifest = _registry.Load(pluginId);
                result.Add(new DevPluginCatalogItem(
                    manifest.PluginName,
                    manifest.DisplayName,
                    manifest.CommandType,
                    GetMainAssembly(manifest),
                    manifest.Versions.Select(item => item.RevitVersion),
                    string.Empty,
                    manifest.PluginType,
                    manifest.ApplicationClass));
            }
            catch (DevManifestException)
            {
                result.Add(new DevPluginCatalogItem(
                    pluginId,
                    pluginId,
                    pluginId + ".Command",
                    pluginId + ".dll",
                    new[] { revitVersion },
                    string.Empty));
            }
        }

        return result;
    }

    private static string GetMainAssembly(DevPluginManifest manifest)
    {
        var assemblyName = manifest.Versions
            .Select(item => Path.GetFileName(item.AssemblyPath))
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(assemblyName) ? manifest.PluginName + ".dll" : assemblyName!;
    }

    private DevPluginManifest? TryLoad(string pluginId, out string? error)
    {
        error = null;
        if (!_registry.Exists(pluginId))
            return null;

        try
        {
            return _registry.Load(pluginId);
        }
        catch (DevManifestException exception)
        {
            _logger.LogWarning(exception, "Manifest unavailable. PluginId='{PluginId}'.", pluginId);
            error = $"DevLoader manifest is corrupt: {_registry.GetManifestPath(pluginId)}. {exception.Message}";
            return null;
        }
    }

    private DevPluginStatus AddConventionalInstallWarning(DevPluginStatus status, string addinsDirectory)
    {
        try
        {
            var match = PluginAssemblyPathLoader.FindConventionalInstall(
                addinsDirectory,
                status.Plugin.PluginId,
                status.Plugin.MainAssembly);
            if (match is null)
                return status;
            if (_registry.IsManagedAssemblyPath(status.Plugin.PluginId, match.AssemblyPath))
                return status;

            var found = string.IsNullOrWhiteSpace(match.AssemblyPath)
                ? $"plugin '{match.AddinName}'"
                : $"assembly '{match.AssemblyPath}'";
            var warning = $"{ConventionalInstallWarningPrefix} {found} in manifest '{match.AddinPath}'. Two copies of the same plugin cannot run together. Disable one installation and restart Revit.";
            _logger.LogWarning("Conventional add-in conflicts with RevitDevLoader. PluginId='{PluginId}'. AddinPath='{AddinPath}'. AssemblyPath='{AssemblyPath}'.", status.Plugin.PluginId, match.AddinPath, match.AssemblyPath);
            return new DevPluginStatus(
                status.Plugin,
                status.RevitVersion,
                status.Installed,
                status.Available,
                status.Kind,
                status.Details + Environment.NewLine + warning);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cannot inspect conventional Revit add-ins. PluginId='{PluginId}'. AddinsDirectory='{AddinsDirectory}'.", status.Plugin.PluginId, addinsDirectory);
            return status;
        }
    }

    internal static int CompareVersions(string release, string assembly, string installedRelease, string installedAssembly)
    {
        if (TryParseVersion(release, out var available) && TryParseVersion(installedRelease, out var installed))
            return available!.CompareTo(installed);
        if (TryParseVersion(assembly, out available) && TryParseVersion(installedAssembly, out installed))
            return available!.CompareTo(installed);
        return 0;
    }

    private static bool TryParseVersion(string value, out Version? version)
    {
        if (!Version.TryParse(value.TrimStart('v', 'V'), out var parsed))
        {
            version = null;
            return false;
        }
        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
        return true;
    }

    private static DevPayloadPackageInfo SelectBestPackage(IEnumerable<DevPayloadPackageInfo> packages, string revitVersion)
    {
        var orderedPackages = packages
            .OrderByDescending(item => item.SupportsVersion(revitVersion))
            .ThenByDescending(item => item, Comparer<DevPayloadPackageInfo>.Create((left, right) =>
                CompareVersions(left.ReleaseId, left.AssemblyVersion, right.ReleaseId, right.AssemblyVersion)))
            .ThenByDescending(item => item.CreatedUtc)
            .ToList();

        return orderedPackages[0];
    }
}
