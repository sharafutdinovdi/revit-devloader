using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitDevLoader.Core;

public sealed class DevPluginManifest
{
    private readonly List<DevPluginVersionEntry> _versions;

    public DevPluginManifest(
        string pluginName,
        string displayName,
        string commandType,
        DateTime updatedUtc,
        IEnumerable<DevPluginVersionEntry> versions)
        : this(
            pluginName,
            displayName,
            commandType,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            updatedUtc,
            versions)
    {
    }

    public DevPluginManifest(
        string pluginName,
        string displayName,
        string commandType,
        string releaseId,
        string assemblyVersion,
        string runRoot,
        string packagePath,
        DateTime updatedUtc,
        IEnumerable<DevPluginVersionEntry> versions)
        : this(
            pluginName,
            displayName,
            commandType,
            releaseId,
            assemblyVersion,
            runRoot,
            packagePath,
            null,
            updatedUtc,
            versions)
    {
    }

    public DevPluginManifest(
        string pluginName,
        string displayName,
        string commandType,
        string releaseId,
        string assemblyVersion,
        string runRoot,
        string packagePath,
        int? commandSlot,
        DateTime updatedUtc,
        IEnumerable<DevPluginVersionEntry> versions,
        DevPluginType pluginType = DevPluginType.Command,
        string applicationClass = "")
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            throw new ArgumentException("Plugin name is required.", nameof(pluginName));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        if (pluginType == DevPluginType.Command && string.IsNullOrWhiteSpace(commandType))
            throw new ArgumentException("Command type is required.", nameof(commandType));
        if (pluginType == DevPluginType.Application && string.IsNullOrWhiteSpace(applicationClass))
            throw new ArgumentException("Application class is required.", nameof(applicationClass));
        if (versions is null)
            throw new ArgumentNullException(nameof(versions));
        if (commandSlot is < 1 or > DevPluginRegistry.CommandSlotCount)
            throw new ArgumentOutOfRangeException(nameof(commandSlot), $"Command slot must be between 1 and {DevPluginRegistry.CommandSlotCount}.");
        if (pluginType == DevPluginType.Application && commandSlot.HasValue)
            throw new ArgumentException("Application plugins do not use command slots.", nameof(commandSlot));

        _versions = versions.ToList();
        if (_versions.Count == 0)
            throw new ArgumentException("At least one version entry is required.", nameof(versions));

        PluginName = pluginName.Trim();
        DisplayName = displayName.Trim();
        PluginType = pluginType;
        CommandType = (commandType ?? string.Empty).Trim();
        ApplicationClass = (applicationClass ?? string.Empty).Trim();
        ReleaseId = (releaseId ?? string.Empty).Trim();
        AssemblyVersion = (assemblyVersion ?? string.Empty).Trim();
        RunRoot = (runRoot ?? string.Empty).Trim();
        PackagePath = (packagePath ?? string.Empty).Trim();
        CommandSlot = commandSlot;
        UpdatedUtc = updatedUtc.Kind == DateTimeKind.Utc ? updatedUtc : updatedUtc.ToUniversalTime();
    }

    public string PluginName { get; }

    public string DisplayName { get; }

    public DevPluginType PluginType { get; }

    public string CommandType { get; }

    public string ApplicationClass { get; }

    public string ReleaseId { get; }

    public string AssemblyVersion { get; }

    public string RunRoot { get; }

    public string PackagePath { get; }

    public int? CommandSlot { get; }

    public DateTime UpdatedUtc { get; }

    public IReadOnlyList<DevPluginVersionEntry> Versions => _versions;

    public string? GetAssemblyPath(string revitVersion)
    {
        if (string.IsNullOrWhiteSpace(revitVersion))
            return null;

        return _versions
            .FirstOrDefault(item => string.Equals(item.RevitVersion, revitVersion.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.AssemblyPath;
    }
}
