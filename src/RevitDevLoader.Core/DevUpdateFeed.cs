using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace RevitDevLoader.Core;

[DataContract]
public sealed class DevUpdateFeed
{
    [DataMember(Name = "schemaVersion")]
    public int SchemaVersion { get; set; }

    [DataMember(Name = "channel")]
    public string? Channel { get; set; }

    [DataMember(Name = "generatedUtc")]
    public string? GeneratedUtc { get; set; }

    [DataMember(Name = "plugins")]
    public List<DevUpdateFeedPlugin>? Plugins { get; set; }

    public IReadOnlyList<DevPayloadPackageInfo> ToPackageInfos(string source)
    {
        var plugins = Plugins ?? new List<DevUpdateFeedPlugin>();
        return plugins
            .Where(plugin => !string.IsNullOrWhiteSpace(plugin.PluginId))
            .SelectMany(plugin => ToPackageInfos(plugin, source))
            .OrderByDescending(item => item.CreatedUtc)
            .ToList();
    }

    private static IEnumerable<DevPayloadPackageInfo> ToPackageInfos(DevUpdateFeedPlugin plugin, string source)
    {
        foreach (var version in plugin.Versions ?? new List<DevUpdateFeedVersion>())
        {
            var pluginType = DevPluginTypeParser.Parse(version.PluginType, "feed");
            if (string.IsNullOrWhiteSpace(version.ReleaseId) ||
                string.IsNullOrWhiteSpace(version.AssemblyVersion) ||
                string.IsNullOrWhiteSpace(version.MainAssembly) ||
                string.IsNullOrWhiteSpace(version.Url))
                continue;
            if (pluginType == DevPluginType.Command && string.IsNullOrWhiteSpace(version.CommandType))
                continue;
            if (pluginType == DevPluginType.Application && string.IsNullOrWhiteSpace(version.ApplicationClass))
                continue;

            var packageUrl = version.Url!;
            yield return new DevPayloadPackageInfo(
                ResolvePackagePath(source, packageUrl),
                plugin.PluginId!,
                string.IsNullOrWhiteSpace(plugin.DisplayName) ? plugin.PluginId! : plugin.DisplayName!,
                version.ReleaseId!,
                version.AssemblyVersion!,
                version.CommandType!,
                version.MainAssembly!,
                ParseDate(version.CreatedUtc),
                version.SupportedRevit ?? new List<string>(),
                schemaVersion: DevPayloadSchema.SupportedVersion,
                source: "feed",
                sha256: version.Sha256 ?? string.Empty,
                sizeBytes: version.Size,
                pluginType: pluginType,
                applicationClass: version.ApplicationClass ?? string.Empty);
        }
    }

    private static DateTime ParseDate(string? value)
    {
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : DateTime.UtcNow;
    }

    private static string ResolvePackagePath(string source, string packageUrl)
    {
        if (Uri.TryCreate(packageUrl, UriKind.Absolute, out var packageUri))
            return packageUri.IsFile ? packageUri.LocalPath : packageUri.ToString();

        if (Uri.TryCreate(source, UriKind.Absolute, out var sourceUri))
        {
            if (!sourceUri.IsFile)
                return new Uri(sourceUri, packageUrl).ToString();

            var directory = Path.GetDirectoryName(sourceUri.LocalPath) ?? string.Empty;
            return Path.GetFullPath(Path.Combine(directory, packageUrl.Replace('/', Path.DirectorySeparatorChar)));
        }

        var baseDirectory = File.Exists(source)
            ? Path.GetDirectoryName(Path.GetFullPath(source)) ?? Directory.GetCurrentDirectory()
            : Path.GetFullPath(source);
        return Path.GetFullPath(Path.Combine(baseDirectory, packageUrl.Replace('/', Path.DirectorySeparatorChar)));
    }
}

[DataContract]
public sealed class DevUpdateFeedPlugin
{
    [DataMember(Name = "pluginId")]
    public string? PluginId { get; set; }

    [DataMember(Name = "displayName")]
    public string? DisplayName { get; set; }

    [DataMember(Name = "versions")]
    public List<DevUpdateFeedVersion>? Versions { get; set; }
}

[DataContract]
public sealed class DevUpdateFeedVersion
{
    [DataMember(Name = "releaseId")]
    public string? ReleaseId { get; set; }

    [DataMember(Name = "assemblyVersion")]
    public string? AssemblyVersion { get; set; }

    [DataMember(Name = "createdUtc")]
    public string? CreatedUtc { get; set; }

    [DataMember(Name = "supportedRevit")]
    public List<string>? SupportedRevit { get; set; }

    [DataMember(Name = "url")]
    public string? Url { get; set; }

    [DataMember(Name = "sha256")]
    public string? Sha256 { get; set; }

    [DataMember(Name = "size")]
    public long Size { get; set; }

    [DataMember(Name = "commandType")]
    public string? CommandType { get; set; }

    [DataMember(Name = "pluginType")]
    public string? PluginType { get; set; }

    [DataMember(Name = "applicationClass")]
    public string? ApplicationClass { get; set; }

    [DataMember(Name = "mainAssembly")]
    public string? MainAssembly { get; set; }
}
