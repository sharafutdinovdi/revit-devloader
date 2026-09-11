using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace RevitDevLoader.Core;

public sealed class DevPayloadPackageDiscovery
{
    private const string PackagePattern = "*DevPayload*.zip";
    private const string ReleaseInfoEntryName = "release-info.properties";

    public DevPayloadPackageInfo? FindLatestPackage(string updatesFolder)
    {
        return FindLatestPackages(updatesFolder)
            .OrderByDescending(item => item.CreatedUtc)
            .ThenByDescending(item => File.GetLastWriteTimeUtc(item.PackagePath))
            .FirstOrDefault();
    }

    public DevPayloadPackageInfo? FindLatestPackage(string updatesFolder, string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin id is required.", nameof(pluginId));

        return FindLatestPackages(updatesFolder)
            .Where(item => string.Equals(item.PluginId, pluginId.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedUtc)
            .ThenByDescending(item => File.GetLastWriteTimeUtc(item.PackagePath))
            .FirstOrDefault();
    }

    public IReadOnlyList<DevPayloadPackageInfo> FindLatestPackages(string updatesFolder)
    {
        return FindPackages(updatesFolder)
            .GroupBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.CreatedUtc)
                .ThenByDescending(item => File.GetLastWriteTimeUtc(item.PackagePath))
                .First())
            .OrderByDescending(item => item.CreatedUtc)
            .ToList();
    }

    public IReadOnlyList<DevPayloadPackageInfo> FindPackages(string updatesFolder)
    {
        if (string.IsNullOrWhiteSpace(updatesFolder) || !Directory.Exists(updatesFolder))
            return Array.Empty<DevPayloadPackageInfo>();

        return Directory.GetFiles(updatesFolder, PackagePattern, SearchOption.TopDirectoryOnly)
            .Select(TryReadPackageInfo)
            .Where(item => item is not null)
            .OrderByDescending(item => item!.CreatedUtc)
            .ThenByDescending(item => File.GetLastWriteTimeUtc(item!.PackagePath))
            .Select(item => item!)
            .ToList();
    }

    public DevPayloadPackageInfo ReadPackageInfo(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package path is required.", nameof(packagePath));
        if (!File.Exists(packagePath))
            throw new DevManifestException($"Dev payload package not found: {packagePath}");

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var releaseInfo = archive.GetEntry(ReleaseInfoEntryName);
            if (releaseInfo is null)
                throw new DevManifestException($"Dev payload package is missing {ReleaseInfoEntryName}: {packagePath}");

            var values = ReadProperties(releaseInfo);
            var schemaVersion = ParseSchemaVersion(GetOptional(values, "schemaVersion"));
            var pluginName = GetOptional(values, "pluginId");
            if (string.IsNullOrWhiteSpace(pluginName))
                pluginName = Require(values, "pluginName", packagePath);
            var displayName = GetOptional(values, "displayName");
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = pluginName;
            var releaseId = Require(values, "releaseId", packagePath);
            var assemblyVersion = GetOptional(values, "assemblyVersion");
            if (string.IsNullOrWhiteSpace(assemblyVersion))
                assemblyVersion = releaseId;
            var pluginType = DevPluginTypeParser.Parse(GetOptional(values, "pluginType"), packagePath);
            var commandType = GetOptional(values, "commandType");
            var applicationClass = GetOptional(values, "applicationClass");
            if (pluginType == DevPluginType.Command && string.IsNullOrWhiteSpace(commandType))
                commandType = Require(values, "commandType", packagePath);
            if (pluginType == DevPluginType.Application && string.IsNullOrWhiteSpace(applicationClass))
                applicationClass = Require(values, "applicationClass", packagePath);
            var mainAssembly = GetOptional(values, "mainAssembly");
            if (string.IsNullOrWhiteSpace(mainAssembly))
                mainAssembly = pluginName + ".dll";
            var versions = ParseVersions(Require(values, "versions", packagePath), packagePath);
            var createdUtc = ParseCreatedUtc(Require(values, "createdUtc", packagePath), packagePath);
            return new DevPayloadPackageInfo(
                packagePath,
                pluginName,
                displayName,
                releaseId,
                assemblyVersion,
                commandType,
                mainAssembly,
                createdUtc,
                versions,
                schemaVersion,
                pluginType: pluginType,
                applicationClass: applicationClass);
        }
        catch (DevManifestException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DevManifestException($"Cannot read dev payload package: {packagePath}", exception);
        }
    }

    private DevPayloadPackageInfo? TryReadPackageInfo(string packagePath)
    {
        try
        {
            return ReadPackageInfo(packagePath);
        }
        catch
        {
            return null;
        }
    }

    internal static Dictionary<string, string> ReadProperties(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return ReadProperties(reader.ReadToEnd());
    }

    internal static Dictionary<string, string> ReadProperties(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            result[line.Substring(0, separatorIndex).Trim()] = line.Substring(separatorIndex + 1).Trim();
        }

        return result;
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string key, string packagePath)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new DevManifestException($"Dev payload package is missing '{key}': {packagePath}");

        return value;
    }

    private static string GetOptional(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
    }

    private static int ParseSchemaVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 1;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var schemaVersion)
            ? schemaVersion
            : 1;
    }

    private static IReadOnlyList<string> ParseVersions(string value, string packagePath)
    {
        var versions = value
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (versions.Count == 0)
            throw new DevManifestException($"Dev payload package has no Revit versions: {packagePath}");

        return versions;
    }

    private static DateTime ParseCreatedUtc(string value, string packagePath)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var createdUtc))
            throw new DevManifestException($"Dev payload package has invalid createdUtc: {packagePath}");

        return DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc);
    }
}
