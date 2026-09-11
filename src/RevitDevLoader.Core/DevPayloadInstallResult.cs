using System;
using System.Collections.Generic;

namespace RevitDevLoader.Core;

public sealed class DevPayloadInstallResult
{
    public DevPayloadInstallResult(string releaseId, string runRoot, string manifestPath, IEnumerable<DevPluginVersionEntry> versions)
        : this(string.Empty, string.Empty, releaseId, string.Empty, runRoot, manifestPath, versions)
    {
    }

    public DevPayloadInstallResult(
        string pluginId,
        string displayName,
        string releaseId,
        string assemblyVersion,
        string runRoot,
        string manifestPath,
        IEnumerable<DevPluginVersionEntry> versions,
        DevRunRetentionResult? retentionResult = null,
        int? commandSlot = null,
        DevPluginType pluginType = DevPluginType.Command)
    {
        if (string.IsNullOrWhiteSpace(releaseId))
            throw new ArgumentException("Release id is required.", nameof(releaseId));
        if (string.IsNullOrWhiteSpace(runRoot))
            throw new ArgumentException("Run root is required.", nameof(runRoot));
        if (string.IsNullOrWhiteSpace(manifestPath))
            throw new ArgumentException("Manifest path is required.", nameof(manifestPath));

        PluginId = (pluginId ?? string.Empty).Trim();
        DisplayName = (displayName ?? string.Empty).Trim();
        ReleaseId = releaseId;
        AssemblyVersion = (assemblyVersion ?? string.Empty).Trim();
        RunRoot = runRoot;
        ManifestPath = manifestPath;
        Versions = new List<DevPluginVersionEntry>(versions ?? throw new ArgumentNullException(nameof(versions)));
        RetentionResult = retentionResult ?? DevRunRetentionResult.Empty;
        CommandSlot = commandSlot;
        PluginType = pluginType;
        ErrorMessage = string.Empty;
    }

    private DevPayloadInstallResult(string pluginId, string displayName, string errorMessage)
    {
        PluginId = (pluginId ?? string.Empty).Trim();
        DisplayName = (displayName ?? string.Empty).Trim();
        ReleaseId = string.Empty;
        AssemblyVersion = string.Empty;
        RunRoot = string.Empty;
        ManifestPath = string.Empty;
        Versions = Array.Empty<DevPluginVersionEntry>();
        RetentionResult = DevRunRetentionResult.Empty;
        CommandSlot = null;
        PluginType = DevPluginType.Command;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Could not install the plugin." : errorMessage.Trim();
    }

    public static DevPayloadInstallResult Failure(string pluginId, string displayName, string errorMessage)
    {
        return new DevPayloadInstallResult(pluginId, displayName, errorMessage);
    }

    public string PluginId { get; }

    public string DisplayName { get; }

    public string ReleaseId { get; }

    public string AssemblyVersion { get; }

    public string RunRoot { get; }

    public string ManifestPath { get; }

    public IReadOnlyList<DevPluginVersionEntry> Versions { get; }

    public DevRunRetentionResult RetentionResult { get; }

    public int? CommandSlot { get; }

    public DevPluginType PluginType { get; }

    public string ErrorMessage { get; }

    public bool IsSuccess => ErrorMessage.Length == 0;
}
