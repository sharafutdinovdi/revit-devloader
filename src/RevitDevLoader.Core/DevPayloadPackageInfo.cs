using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

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
        string iconPath = "",
        string description = "",
        DevPackageManifest? packageManifest = null)
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
        Description = description;
        PackageManifest = packageManifest;
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

    public string Description { get; }

    public DevPackageManifest? PackageManifest { get; }

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

[DataContract]
public sealed class DevPackageCommand
{
    [DataMember(Name = "id")] public string Id { get; set; } = "";
    [DataMember(Name = "class")] public string Class { get; set; } = "";
    [DataMember(Name = "text")] public string Text { get; set; } = "";
    [DataMember(Name = "tooltip")] public string Tooltip { get; set; } = "";
    [DataMember(Name = "icon")] public string Icon { get; set; } = "";
    [DataMember(Name = "slot", EmitDefaultValue = false)] public int? Slot { get; set; }

    public DevPackageCommand WithSlot(int? slot) => new()
    {
        Id = Id,
        Class = Class,
        Text = Text,
        Tooltip = Tooltip,
        Icon = Icon,
        Slot = slot
    };
}

[DataContract]
public sealed class DevPackageEntry
{
    [DataMember(Name = "assembly")] public string Assembly { get; set; } = "";
    [DataMember(Name = "applicationClass")] public string ApplicationClass { get; set; } = "";
}

[DataContract]
public sealed class DevPackageManifest
{
    [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
    [DataMember(Name = "id")] public string Id { get; set; } = "";
    [DataMember(Name = "displayName")] public string DisplayName { get; set; } = "";
    [DataMember(Name = "version")] public string Version { get; set; } = "";
    [DataMember(Name = "description")] public string Description { get; set; } = "";
    [DataMember(Name = "author")] public string Author { get; set; } = "";
    [DataMember(Name = "revit")] public List<string> Revit { get; set; } = new();
    [DataMember(Name = "icon")] public string Icon { get; set; } = "";
    [DataMember(Name = "entry")] public DevPackageEntry Entry { get; set; } = new();
    [DataMember(Name = "commands")] public List<DevPackageCommand> Commands { get; set; } = new();

    public static DevPackageManifest Parse(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var manifest = (DevPackageManifest?)new DataContractJsonSerializer(typeof(DevPackageManifest)).ReadObject(stream)
            ?? throw new DevManifestException("plugin.json is empty.");
        manifest.Validate();
        return manifest;
    }

    public string Serialize()
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(typeof(DevPackageManifest)).WriteObject(stream, this);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public string GetAssemblyEntry(string year)
    {
        if (!Revit.Contains(year, StringComparer.Ordinal))
            throw new DevManifestException($"Package does not support Revit {year}.");
        return year + Entry.Assembly.Substring(Entry.Assembly.IndexOf('/'));
    }

    private void Validate()
    {
        if (SchemaVersion != 2)
            throw new DevManifestException("plugin.json requires schemaVersion 2.");
        if (!Regex.IsMatch(Id ?? "", @"^[A-Za-z0-9][A-Za-z0-9._-]*$") ||
            !Regex.IsMatch(Version ?? "", @"^[A-Za-z0-9][A-Za-z0-9._-]*$"))
            throw new DevManifestException("Package id and version must be safe path segments.");
        if (string.IsNullOrWhiteSpace(DisplayName) || Revit is null || Revit.Count == 0 ||
            Revit.Any(year => !Regex.IsMatch(year ?? "", @"^20\d{2}$")) || Revit.Distinct().Count() != Revit.Count)
            throw new DevManifestException("Package requires a displayName and distinct Revit years.");
        if (Entry is null || string.IsNullOrWhiteSpace(Entry.Assembly))
            throw new DevManifestException("Package requires entry.assembly.");
        ValidateRelativePath(Entry.Assembly);
        if (!Entry.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            !Revit.Contains(Entry.Assembly.Split('/')[0]))
            throw new DevManifestException("entry.assembly must start with a declared Revit year and end in .dll.");
        ValidateIcon(Icon);
        Commands ??= new List<DevPackageCommand>();
        if (Commands.Count == 0 && string.IsNullOrWhiteSpace(Entry.ApplicationClass))
            throw new DevManifestException("Application packages require entry.applicationClass.");
        if (Commands.Count != 0 && !string.IsNullOrWhiteSpace(Entry.ApplicationClass))
            throw new DevManifestException("A package declares commands or an application entry point.");
        var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in Commands)
        {
            if (command is null || !Regex.IsMatch(command.Id ?? "", @"^[A-Za-z0-9][A-Za-z0-9._-]*$") ||
                !identifiers.Add(command.Id!) || string.IsNullOrWhiteSpace(command.Class) || string.IsNullOrWhiteSpace(command.Text))
                throw new DevManifestException("Commands require unique ids, class and text.");
            if (!string.IsNullOrEmpty(command.Icon))
                ValidateIcon(command.Icon);
        }
    }

    private static void ValidateIcon(string path)
    {
        ValidateRelativePath(path);
        if (!path.StartsWith("icons/", StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new DevManifestException("Package icons must be PNG paths under icons/.");
    }

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(new[] { '\\', ':', '*', '?', '<', '>', '|', '\0' }) >= 0 ||
            path.Split('/').Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." ||
                segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal)))
            throw new DevManifestException($"Unsafe package path: {path}");
    }
}
