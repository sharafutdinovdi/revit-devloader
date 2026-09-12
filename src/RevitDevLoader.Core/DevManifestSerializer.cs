using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Linq;
using System.Text;

namespace RevitDevLoader.Core;

public static class DevManifestSerializer
{
    private const string PluginNameKey = "pluginName";
    private const string DisplayNameKey = "displayName";
    private const string PluginTypeKey = "pluginType";
    private const string CommandTypeKey = "commandType";
    private const string ApplicationClassKey = "applicationClass";
    private const string ReleaseIdKey = "releaseId";
    private const string AssemblyVersionKey = "assemblyVersion";
    private const string RunRootKey = "runRoot";
    private const string PackagePathKey = "packagePath";
    private const string CommandSlotKey = "commandSlot";
    private const string UpdatedUtcKey = "updatedUtc";
    private const string VersionPrefix = "version.";
    private const string AssemblyPathSuffix = ".assemblyPath";

    public static DevPluginManifest Parse(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var values = ParseKeyValuePairs(text);
        var pluginName = Require(values, PluginNameKey);
        var displayName = Require(values, DisplayNameKey);
        var pluginType = DevPluginTypeParser.Parse(GetOptional(values, PluginTypeKey), "dev manifest");
        var commandType = GetOptional(values, CommandTypeKey);
        var applicationClass = GetOptional(values, ApplicationClassKey);
        if (pluginType == DevPluginType.Command)
            commandType = Require(values, CommandTypeKey);
        else
            applicationClass = Require(values, ApplicationClassKey);
        var releaseId = GetOptional(values, ReleaseIdKey);
        var assemblyVersion = GetOptional(values, AssemblyVersionKey);
        var runRoot = GetOptional(values, RunRootKey);
        var packagePath = GetOptional(values, PackagePathKey);
        var commandSlot = ParseCommandSlot(GetOptional(values, CommandSlotKey));
        var updatedUtc = ParseUpdatedUtc(Require(values, UpdatedUtcKey));
        var versions = ParseVersions(values).ToList();
        if (versions.Count == 0)
            throw new DevManifestException("Manifest must contain at least one version.*.assemblyPath entry.");

        return new DevPluginManifest(
            pluginName,
            displayName,
            commandType,
            releaseId,
            assemblyVersion,
            runRoot,
            packagePath,
            commandSlot,
            updatedUtc,
            versions,
            pluginType,
            applicationClass,
            GetOptional(values, "iconPath"),
            DecodeText(GetOptional(values, "descriptionBase64")),
            ParseCommands(GetOptional(values, "commandsBase64")));
    }

    public static string Serialize(DevPluginManifest manifest)
    {
        if (manifest is null)
            throw new ArgumentNullException(nameof(manifest));

        var builder = new StringBuilder();
        builder.AppendLine($"iconPath={manifest.IconPath}");
        builder.AppendLine($"descriptionBase64={Convert.ToBase64String(Encoding.UTF8.GetBytes(manifest.Description))}");
        using var commandStream = new MemoryStream();
        new DataContractJsonSerializer(typeof(List<DevPackageCommand>)).WriteObject(commandStream, manifest.Commands.ToList());
        builder.AppendLine($"commandsBase64={Convert.ToBase64String(commandStream.ToArray())}");
        builder.AppendLine($"{PluginNameKey}={manifest.PluginName}");
        builder.AppendLine($"{DisplayNameKey}={manifest.DisplayName}");
        builder.AppendLine($"{PluginTypeKey}={DevPluginTypeParser.Format(manifest.PluginType)}");
        if (manifest.PluginType == DevPluginType.Application)
            builder.AppendLine($"{ApplicationClassKey}={manifest.ApplicationClass}");
        else
            builder.AppendLine($"{CommandTypeKey}={manifest.CommandType}");
        builder.AppendLine($"{ReleaseIdKey}={manifest.ReleaseId}");
        builder.AppendLine($"{AssemblyVersionKey}={manifest.AssemblyVersion}");
        builder.AppendLine($"{RunRootKey}={manifest.RunRoot}");
        builder.AppendLine($"{PackagePathKey}={manifest.PackagePath}");
        if (manifest.CommandSlot.HasValue)
            builder.AppendLine($"{CommandSlotKey}={manifest.CommandSlot.Value}");
        builder.AppendLine($"{UpdatedUtcKey}={manifest.UpdatedUtc.ToUniversalTime():O}");
        foreach (var version in manifest.Versions.OrderBy(item => item.RevitVersion, StringComparer.OrdinalIgnoreCase))
            builder.AppendLine($"{VersionPrefix}{version.RevitVersion}{AssemblyPathSuffix}={version.AssemblyPath}");

        return builder.ToString();
    }

    private static string DecodeText(string value) => string.IsNullOrEmpty(value) ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(value));

    private static List<DevPackageCommand>? ParseCommands(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        using var stream = new MemoryStream(Convert.FromBase64String(value));
        return (List<DevPackageCommand>?)new DataContractJsonSerializer(typeof(List<DevPackageCommand>)).ReadObject(stream);
    }

    private static Dictionary<string, string> ParseKeyValuePairs(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                throw new DevManifestException($"Invalid manifest line {index + 1}: expected key=value.");

            var key = line.Substring(0, separatorIndex).Trim();
            var value = line.Substring(separatorIndex + 1).Trim();
            if (key.Length == 0)
                throw new DevManifestException($"Invalid manifest line {index + 1}: key is empty.");

            result[key] = value;
        }

        return result;
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new DevManifestException($"Manifest is missing required key '{key}'.");

        return value;
    }

    private static string GetOptional(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
    }

    private static DateTime ParseUpdatedUtc(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var updatedUtc))
            throw new DevManifestException($"Manifest has invalid '{UpdatedUtcKey}' value.");

        return DateTime.SpecifyKind(updatedUtc, DateTimeKind.Utc);
    }

    private static int? ParseCommandSlot(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var slot) ||
            slot < 1 ||
            slot > DevPluginRegistry.CommandSlotCount)
        {
            throw new DevManifestException(
                $"Manifest has invalid '{CommandSlotKey}' value. Expected 1-{DevPluginRegistry.CommandSlotCount}.");
        }

        return slot;
    }

    private static IEnumerable<DevPluginVersionEntry> ParseVersions(IReadOnlyDictionary<string, string> values)
    {
        foreach (var pair in values)
        {
            if (!pair.Key.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase) ||
                !pair.Key.EndsWith(AssemblyPathSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var revitVersion = pair.Key.Substring(
                VersionPrefix.Length,
                pair.Key.Length - VersionPrefix.Length - AssemblyPathSuffix.Length);
            if (string.IsNullOrWhiteSpace(revitVersion))
                throw new DevManifestException("Manifest contains a version entry without Revit version.");

            yield return new DevPluginVersionEntry(revitVersion, pair.Value);
        }
    }
}
