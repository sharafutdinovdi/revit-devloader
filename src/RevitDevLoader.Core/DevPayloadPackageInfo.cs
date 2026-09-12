using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitDevLoader.Core;

public sealed class DevPayloadPackageInfo
{
    public DevPayloadPackageInfo(
        string packagePath,
        string pluginId,
        string displayName,
        string releaseId,
        string assemblyVersion,
        string commandType,
        string mainAssembly,
        DateTime createdUtc,
        IEnumerable<string> versions,
        int schemaVersion,
        string source = "",
        string sha256 = "",
        long sizeBytes = 0,
        DevPluginType pluginType = DevPluginType.Command,
        string applicationClass = "",
        string iconPath = "")
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package path is required.", nameof(packagePath));
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin id is required.", nameof(pluginId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(releaseId))
            throw new ArgumentException("Release id is required.", nameof(releaseId));
        if (string.IsNullOrWhiteSpace(assemblyVersion))
            throw new ArgumentException("Assembly version is required.", nameof(assemblyVersion));
        if (pluginType == DevPluginType.Command && string.IsNullOrWhiteSpace(commandType))
            throw new ArgumentException("Command type is required.", nameof(commandType));
        if (pluginType == DevPluginType.Application && string.IsNullOrWhiteSpace(applicationClass))
            throw new ArgumentException("Application class is required.", nameof(applicationClass));
        if (string.IsNullOrWhiteSpace(mainAssembly))
            throw new ArgumentException("Main assembly is required.", nameof(mainAssembly));
        if (versions is null)
            throw new ArgumentNullException(nameof(versions));

        var versionList = versions
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (versionList.Count == 0)
            throw new ArgumentException("At least one Revit version is required.", nameof(versions));

        IconPath = iconPath;
        PackagePath = packagePath;
        PluginId = pluginId.Trim();
        DisplayName = displayName.Trim();
        ReleaseId = releaseId.Trim();
        AssemblyVersion = assemblyVersion.Trim();
        PluginType = pluginType;
        CommandType = (commandType ?? string.Empty).Trim();
        ApplicationClass = (applicationClass ?? string.Empty).Trim();
        MainAssembly = mainAssembly.Trim();
        CreatedUtc = createdUtc.Kind == DateTimeKind.Utc ? createdUtc : createdUtc.ToUniversalTime();
        Versions = versionList;
        SchemaVersion = schemaVersion;
        Source = string.IsNullOrWhiteSpace(source) ? "local" : source.Trim();
        Sha256 = string.IsNullOrWhiteSpace(sha256) ? string.Empty : sha256.Trim();
        SizeBytes = sizeBytes < 0 ? 0 : sizeBytes;
    }

    public string IconPath { get; }

    public string PackagePath { get; }

    public string PluginId { get; }

    public string DisplayName { get; }

    public string ReleaseId { get; }

    public string AssemblyVersion { get; }

    public DevPluginType PluginType { get; }

    public string CommandType { get; }

    public string ApplicationClass { get; }

    public string MainAssembly { get; }

    public DateTime CreatedUtc { get; }

    public IReadOnlyList<string> Versions { get; }

    public int SchemaVersion { get; }

    public string Source { get; }

    public string Sha256 { get; }

    public long SizeBytes { get; }

    public bool HasHash => !string.IsNullOrWhiteSpace(Sha256);

    public bool IsRemotePackage => Uri.TryCreate(PackagePath, UriKind.Absolute, out var uri) &&
        (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    public bool SupportsVersion(string revitVersion)
    {
        if (string.IsNullOrWhiteSpace(revitVersion))
            return false;

        return Versions.Contains(revitVersion.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}

public enum DevPluginType
{
    Command,
    Application
}

internal static class DevPluginTypeParser
{
    public static DevPluginType Parse(string? value, string source)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0 ||
            string.Equals(normalized, "command", StringComparison.OrdinalIgnoreCase))
            return DevPluginType.Command;

        if (string.Equals(normalized, "application", StringComparison.OrdinalIgnoreCase))
            return DevPluginType.Application;

        throw new DevManifestException($"Invalid pluginType '{value}' in {source}. Expected 'command' or 'application'.");
    }

    public static string Format(DevPluginType pluginType)
    {
        return pluginType == DevPluginType.Application ? "application" : "command";
    }
}
