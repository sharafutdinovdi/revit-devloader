using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace RevitDevLoader.Core;

public sealed class DevPayloadInstaller
{
    public DevPayloadInstallResult Install(
        string packagePath,
        string localAppDataRoot,
        IEnumerable<string> requiredVersions,
        int? runRetentionCount = null,
        string? applicationDataRoot = null)
    {
        if (string.IsNullOrWhiteSpace(localAppDataRoot))
            throw new ArgumentException("Local app data root is required.", nameof(localAppDataRoot));
        if (requiredVersions is null)
            throw new ArgumentNullException(nameof(requiredVersions));
        if (runRetentionCount.HasValue && runRetentionCount.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(runRetentionCount), "Retention count must be at least 1.");

        var versions = requiredVersions.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (versions.Count == 0)
            throw new ArgumentException("At least one required version is required.", nameof(requiredVersions));

        var packageInfo = new DevPayloadPackageDiscovery().ReadPackageInfo(packagePath);
        var pluginId = ValidatePathSegment(packageInfo.PluginId, "plugin id");
        var releaseId = ValidateReleaseId(packageInfo.ReleaseId);
        var registry = new DevPluginRegistry(localAppDataRoot);
        int? commandSlot = null;
        if (packageInfo.PluginType == DevPluginType.Command)
        {
            if (!registry.TryGetCommandSlot(pluginId, out var assignedSlot, out var slotError))
                return DevPayloadInstallResult.Failure(packageInfo.PluginId, packageInfo.DisplayName, slotError);

            commandSlot = assignedSlot;
        }

        var resolvedApplicationDataRoot = applicationDataRoot;
        if (packageInfo.PluginType == DevPluginType.Application)
        {
            resolvedApplicationDataRoot ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(resolvedApplicationDataRoot))
                throw new DevManifestException("Application data root is unavailable.");

            foreach (var version in versions)
            {
                var addinPath = DevPluginRegistry.GetApplicationManifestPath(resolvedApplicationDataRoot, version, pluginId);
                if (!registry.CanReplaceApplicationManifest(addinPath, pluginId, out var warning))
                    return DevPayloadInstallResult.Failure(packageInfo.PluginId, packageInfo.DisplayName, warning);
            }
        }

        var registryRoot = Path.Combine(localAppDataRoot, "RevitDevLoader", "plugins");
        var runRoot = GetAvailableRunRoot(registryRoot, pluginId, releaseId);
        EnsureChildPath(registryRoot, runRoot);

        using var archive = ZipFile.OpenRead(packagePath);
        ValidateRequiredAssemblies(archive, packageInfo, versions, packagePath);

        Directory.CreateDirectory(runRoot);

        foreach (var version in versions)
            ExtractVersion(archive, version, runRoot);

        var icon = archive.GetEntry("icon.png");
        if (icon is not null)
            icon.ExtractToFile(Path.Combine(runRoot, "icon.png"));

        var manifestVersions = versions
            .Select(version => new DevPluginVersionEntry(version, Path.Combine(runRoot, version, packageInfo.MainAssembly)))
            .ToList();
        var manifest = new DevPluginManifest(
            packageInfo.PluginId,
            packageInfo.DisplayName,
            packageInfo.CommandType,
            packageInfo.ReleaseId,
            packageInfo.AssemblyVersion,
            runRoot,
            packageInfo.PackagePath,
            commandSlot,
            DateTime.UtcNow,
            manifestVersions,
            packageInfo.PluginType,
            packageInfo.ApplicationClass);

        registry.Save(manifest);
        if (packageInfo.PluginType == DevPluginType.Application)
        {
            WriteApplicationManifests(
                resolvedApplicationDataRoot!,
                packageInfo,
                manifestVersions);
        }
        var retentionResult = DevRunRetentionResult.Empty;
        if (runRetentionCount.HasValue)
        {
            var currentManifest = registry.Load(packageInfo.PluginId);
            retentionResult = new DevRunRetentionService().Cleanup(
                Path.Combine(registryRoot, pluginId, "runs"),
                currentManifest.RunRoot,
                runRetentionCount.Value);
        }

        return new DevPayloadInstallResult(
            packageInfo.PluginId,
            packageInfo.DisplayName,
            releaseId,
            packageInfo.AssemblyVersion,
            runRoot,
            registry.GetManifestPath(packageInfo.PluginId),
            manifestVersions,
            retentionResult,
            commandSlot,
            packageInfo.PluginType);
    }

    private static void WriteApplicationManifests(
        string applicationDataRoot,
        DevPayloadPackageInfo packageInfo,
        IEnumerable<DevPluginVersionEntry> versions)
    {
        var addInId = CreateStableAddInId(packageInfo.PluginId);
        foreach (var version in versions)
        {
            var addinPath = DevPluginRegistry.GetApplicationManifestPath(
                applicationDataRoot,
                version.RevitVersion,
                packageInfo.PluginId);
            var document = new XDocument(
                new XElement(
                    "RevitAddIns",
                    new XElement(
                        "AddIn",
                        new XAttribute("Type", "Application"),
                        new XElement("Name", packageInfo.DisplayName),
                        new XElement("Assembly", version.AssemblyPath),
                        new XElement("AddInId", addInId.ToString("D").ToUpperInvariant()),
                        new XElement("FullClassName", packageInfo.ApplicationClass),
                        new XElement("VendorId", "DSHA"),
                        new XElement("VendorDescription", "Dinar Sharafutdinov, https://github.com/sharafutdinovdi/revit-devloader"))));
            DevPluginRegistry.WriteAtomically(addinPath, document.ToString());
        }
    }

    internal static Guid CreateStableAddInId(string pluginId)
    {
        var normalized = "RevitDevLoader.AddInId:" + pluginId.Trim().ToUpperInvariant();
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, guidBytes.Length);
        return new Guid(guidBytes);
    }

    private static string GetAvailableRunRoot(string registryRoot, string pluginId, string releaseId)
    {
        var runsRoot = Path.Combine(registryRoot, pluginId, "runs");
        var baseRunRoot = Path.Combine(runsRoot, releaseId);
        if (!Directory.Exists(baseRunRoot))
            return baseRunRoot;

        for (var index = 2; index < 1000; index++)
        {
            var candidate = Path.Combine(runsRoot, $"{releaseId}-r{index}");
            if (!Directory.Exists(candidate))
                return candidate;
        }

        throw new DevManifestException($"Cannot find a free run folder for release '{releaseId}'.");
    }

    private static void ValidateRequiredAssemblies(ZipArchive archive, DevPayloadPackageInfo packageInfo, IEnumerable<string> versions, string packagePath)
    {
        foreach (var version in versions)
        {
            var requiredEntry = $"payload/{version}/{packageInfo.MainAssembly}";
            if (GetEntryByNormalizedName(archive, requiredEntry) is null)
                throw new DevManifestException($"Dev payload package is missing required Revit {version} assembly '{requiredEntry}': {packagePath}");
        }
    }

    private static void ExtractVersion(ZipArchive archive, string version, string runRoot)
    {
        var prefix = $"payload/{version}/";
        var destinationRoot = Path.Combine(runRoot, version);
        Directory.CreateDirectory(destinationRoot);

        foreach (var entry in archive.Entries)
        {
            var normalizedName = NormalizeEntryName(entry.FullName);
            if (!normalizedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (normalizedName.EndsWith("/", StringComparison.Ordinal))
                continue;

            var relativeName = normalizedName.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relativeName))
                continue;

            var destinationPath = Path.Combine(destinationRoot, relativeName);
            EnsureChildPath(destinationRoot, destinationPath);
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static ZipArchiveEntry? GetEntryByNormalizedName(ZipArchive archive, string normalizedName)
    {
        return archive.Entries.FirstOrDefault(
            entry => string.Equals(NormalizeEntryName(entry.FullName), normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeEntryName(string entryName)
    {
        return entryName.Replace('\\', '/');
    }

    private static string ValidateReleaseId(string releaseId)
    {
        return ValidatePathSegment(releaseId, "release id");
    }

    private static string ValidatePathSegment(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DevManifestException($"Dev payload package {label} is empty.");

        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.IndexOf("\\", StringComparison.Ordinal) >= 0 ||
            value.IndexOf("/", StringComparison.Ordinal) >= 0)
            throw new DevManifestException($"Dev payload package {label} is unsafe: {value}");

        return value.Trim();
    }

    private static void EnsureChildPath(string parent, string child)
    {
        var resolvedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedChild = Path.GetFullPath(child);
        if (!resolvedChild.StartsWith(resolvedParent, StringComparison.OrdinalIgnoreCase))
            throw new DevManifestException($"Path escapes expected folder: {resolvedChild}");
    }
}
