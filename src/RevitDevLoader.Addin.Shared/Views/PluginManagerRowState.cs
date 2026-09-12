using System;
using System.Collections.Generic;
using System.Globalization;
using RevitDevLoader.Core;

namespace RevitDevLoader.Views;

internal enum PluginManagerPrimaryAction
{
    None,
    Install,
    Update,
    Reinstall
}

internal enum PluginManagerStatusIcon
{
    None,
    Installed,
    UpdateAvailable,
    ConventionalInstall,
    Error
}

internal static class PluginManagerSearchState
{
    public static bool ShouldShowPlaceholder(string? text, bool hasKeyboardFocus)
    {
        return string.IsNullOrEmpty(text) && !hasKeyboardFocus;
    }
}

internal sealed class PluginManagerRowState
{
    private PluginManagerRowState(
        DevPluginStatus status,
        string statusText,
        PluginManagerStatusIcon statusIcon,
        string reasonText,
        string statusToolTipText,
        PluginManagerPrimaryAction primaryAction)
    {
        StatusText = statusText;
        StatusIcon = statusIcon;
        ReasonText = reasonText;
        StatusToolTipText = statusToolTipText;
        PrimaryAction = primaryAction;
        VersionText = BuildVersionText(status);
        DateText = BuildDateText(status);
    }

    public string StatusText { get; }

    public PluginManagerStatusIcon StatusIcon { get; }

    public string ReasonText { get; }

    public string StatusToolTipText { get; }

    public PluginManagerPrimaryAction PrimaryAction { get; }

    public string VersionText { get; }

    public string DateText { get; }

    public bool UseAccentPrimaryAction => PrimaryAction == PluginManagerPrimaryAction.Update;

    public string PrimaryActionText => PrimaryAction switch
    {
        PluginManagerPrimaryAction.Install => "Install",
        PluginManagerPrimaryAction.Update => "Update",
        PluginManagerPrimaryAction.Reinstall => "Reinstall",
        _ => string.Empty
    };

    public static PluginManagerRowState Create(DevPluginStatus status)
    {
        if (status is null)
            throw new ArgumentNullException(nameof(status));

        if (status.Details.IndexOf(
                DevPluginStatusService.ConventionalInstallWarningPrefix,
                StringComparison.Ordinal) >= 0)
        {
            return new PluginManagerRowState(
                status,
                "Managed outside DevLoader",
                PluginManagerStatusIcon.ConventionalInstall,
                string.Empty,
                "The plugin is installed manually and managed outside DevLoader",
                PluginManagerPrimaryAction.None);
        }

        return status.Kind switch
        {
            DevPluginStatusKind.UpdateAvailable => new PluginManagerRowState(
                status,
                "Update available",
                PluginManagerStatusIcon.UpdateAvailable,
                string.Empty,
                "Update available",
                PluginManagerPrimaryAction.Update),
            DevPluginStatusKind.Latest when status.HasNewerInstalledVersion => new PluginManagerRowState(
                status,
                "Installed",
                PluginManagerStatusIcon.Installed,
                "Installed version is newer than the feed.",
                "Installed version is newer than the feed.",
                PluginManagerPrimaryAction.None),
            DevPluginStatusKind.Latest => new PluginManagerRowState(
                status,
                "Installed",
                PluginManagerStatusIcon.Installed,
                string.Empty,
                "Installed",
                PluginManagerPrimaryAction.Reinstall),
            DevPluginStatusKind.InstalledNoPackage => new PluginManagerRowState(
                status,
                "Installed",
                PluginManagerStatusIcon.Installed,
                "The plugin is missing from the update source.",
                "The plugin is missing from the update source.",
                PluginManagerPrimaryAction.None),
            DevPluginStatusKind.NotInstalled when status.Available is not null => new PluginManagerRowState(
                status,
                "Not installed",
                PluginManagerStatusIcon.None,
                string.Empty,
                "Not installed",
                PluginManagerPrimaryAction.Install),
            DevPluginStatusKind.NotInstalled => new PluginManagerRowState(
                status,
                "Not installed",
                PluginManagerStatusIcon.None,
                "The plugin is missing from the update source and cannot be installed.",
                "The plugin is missing from the update source and cannot be installed.",
                PluginManagerPrimaryAction.None),
            DevPluginStatusKind.UnsupportedRevitVersion => new PluginManagerRowState(
                status,
                "Unavailable",
                PluginManagerStatusIcon.Error,
                $"No version for Revit {status.RevitVersion}.",
                $"No version for Revit {status.RevitVersion}.",
                PluginManagerPrimaryAction.None),
            DevPluginStatusKind.CommandSlotUnavailable => new PluginManagerRowState(
                status,
                "No free slot",
                PluginManagerStatusIcon.Error,
                status.Details,
                status.Details,
                PluginManagerPrimaryAction.None),
            DevPluginStatusKind.PackageError => new PluginManagerRowState(
                status,
                "Package error",
                PluginManagerStatusIcon.Error,
                "Open the logs and check the update package.",
                "Open the logs and check the update package.",
                PluginManagerPrimaryAction.None),
            _ => new PluginManagerRowState(
                status,
                "Unknown status",
                PluginManagerStatusIcon.Error,
                "Check the logs.",
                "Check the logs.",
                PluginManagerPrimaryAction.None)
        };
    }

    public static bool MatchesSearch(DevPluginStatus status, string? query)
    {
        if (status is null)
            throw new ArgumentNullException(nameof(status));

        var text = query?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var searchText = text!;
        var row = Create(status);
        return Contains(status.Plugin.DisplayName, searchText) ||
            Contains(status.Plugin.PluginId, searchText) ||
            Contains(row.StatusText, searchText) ||
            Contains(row.ReasonText, searchText);
    }

    private static bool Contains(string value, string query)
    {
        return value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildVersionText(DevPluginStatus status)
    {
        if (status.Kind == DevPluginStatusKind.UpdateAvailable &&
            status.Installed is not null &&
            status.Available is not null)
        {
            return $"{FormatVersion(status.Installed.ReleaseId)} → {FormatVersion(status.Available.ReleaseId)}";
        }

        if (status.Installed is not null)
            return $"Version {FormatVersion(status.Installed.ReleaseId)}";

        return status.Available is not null
            ? $"Version {FormatVersion(status.Available.ReleaseId)}"
            : "Version unavailable";
    }

    private static string FormatVersion(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Version unknown" : value.Trim();
    }

    private static string BuildDateText(DevPluginStatus status)
    {
        DateTime? date = status.Kind == DevPluginStatusKind.UpdateAvailable
            ? status.Available?.CreatedUtc
            : status.Installed?.UpdatedUtc ?? status.Available?.CreatedUtc;
        return date?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
    }
}

internal enum PluginManagerRowMenuAction
{
    OpenFolder,
    ShowVersions,
    Remove
}

internal sealed class PluginManagerRowMenuItem
{
    public PluginManagerRowMenuItem(PluginManagerRowMenuAction action, bool isEnabled)
    {
        Action = action;
        IsEnabled = isEnabled;
    }

    public PluginManagerRowMenuAction Action { get; }

    public bool IsEnabled { get; }
}

internal static class PluginManagerRowMenuState
{
    public static IReadOnlyList<PluginManagerRowMenuItem> Create(DevPluginStatus status)
    {
        if (status is null)
            throw new ArgumentNullException(nameof(status));

        if (status.Details.IndexOf(
                DevPluginStatusService.ConventionalInstallWarningPrefix,
                StringComparison.Ordinal) >= 0)
        {
            return new[]
            {
                new PluginManagerRowMenuItem(PluginManagerRowMenuAction.OpenFolder, false),
                new PluginManagerRowMenuItem(PluginManagerRowMenuAction.ShowVersions, false),
                new PluginManagerRowMenuItem(PluginManagerRowMenuAction.Remove, false)
            };
        }

        var canOpenFolder = status.Installed is not null &&
            !string.IsNullOrWhiteSpace(status.Installed.RunRoot);
        return new[]
        {
            new PluginManagerRowMenuItem(PluginManagerRowMenuAction.OpenFolder, canOpenFolder),
            new PluginManagerRowMenuItem(PluginManagerRowMenuAction.ShowVersions, true),
            new PluginManagerRowMenuItem(PluginManagerRowMenuAction.Remove, status.Installed is not null)
        };
    }
}
