using System;

namespace RevitDevLoader.Core;

public sealed class DevPluginStatus
{
    public DevPluginStatus(
        DevPluginCatalogItem plugin,
        string revitVersion,
        DevPluginManifest? installed,
        DevPayloadPackageInfo? available,
        DevPluginStatusKind kind,
        string details)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        RevitVersion = revitVersion ?? string.Empty;
        Installed = installed;
        Available = available;
        Kind = kind;
        Details = details ?? string.Empty;
    }

    public DevPluginCatalogItem Plugin { get; }

    public string RevitVersion { get; }

    public DevPluginManifest? Installed { get; }

    public DevPayloadPackageInfo? Available { get; }

    public DevPluginStatusKind Kind { get; }

    public string Details { get; }

    public bool HasNewerInstalledVersion => Installed is not null && Available is not null &&
        DevPluginStatusService.CompareVersions(Installed.ReleaseId, Installed.AssemblyVersion,
            Available.ReleaseId, Available.AssemblyVersion) > 0;

    public bool CanRun => Installed is { PluginType: DevPluginType.Command, CommandSlot: not null } &&
        Installed.GetAssemblyPath(RevitVersion) is { Length: > 0 };

    public bool CanInstallOrUpdate => Available is not null &&
        (Kind == DevPluginStatusKind.NotInstalled ||
         Kind == DevPluginStatusKind.InstalledNoPackage ||
         Kind == DevPluginStatusKind.UpdateAvailable);
}
