using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RevitDevLoader.Core;
using RevitDevLoader.Infrastructure;

namespace RevitDevLoader.Views;

public sealed partial class PluginManagerWindow
{
    private async void Install(DevPluginStatus status)
    {
        if (_isBusy || status.Available is null)
            return;

        SetBusy(true);
        try
        {
            var result = await InstallInternalAsync(status);
            if (!result.IsSuccess)
            {
                _logger.Warn($"Install skipped. PluginId='{status.Plugin.PluginId}'. Reason='{result.ErrorMessage}'.");
                MessageBox.Show(
                    this,
                    result.ErrorMessage,
                    "DevLoader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var message = $"✓ Installed {result.ReleaseId} just now";
            if (result.PluginType == DevPluginType.Application)
            {
                message += ". Restart Revit to load it.";
                _logger.Info($"Revit restart required to load application plugin. PluginId='{status.Plugin.PluginId}'.");
            }
            _operationStatuses[status.Plugin.PluginId] = message;
        }
        catch (Exception exception)
        {
            _logger.Error($"Install failed. PluginId='{status.Plugin.PluginId}'.", exception);
            MessageBox.Show(
                this,
                $"Could not install {status.Plugin.DisplayName}.\n\nOpen the logs for details.",
                "DevLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }

        RebuildStatusesFromCurrentPackages();
    }

    private async Task<DevPayloadInstallResult> InstallInternalAsync(DevPluginStatus status)
    {
        if (status.Available is null)
            throw new DevManifestException("Update package not found.");

        var available = status.Available;
        var install = await Task.Run(() =>
        {
            var packagePath = _packageCache.PreparePackage(
                available,
                DevUpdateLocations.GetDefaultCacheFolder());
            var result = _installer.Install(
                packagePath,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                new[] { _revitVersion },
                _settings.RunRetentionCount,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            return new { Result = result, PackagePath = packagePath };
        });

        var result = install.Result;
        if (!result.IsSuccess)
            return result;

        _logger.Info($"Installed PluginId='{result.PluginId}'. ReleaseId='{result.ReleaseId}'. Package='{install.PackagePath}'. Manifest='{result.ManifestPath}'.");
        _logger.Info($"Run retention completed. Retained='{result.RetentionResult.RetainedCount}'. Deleted='{result.RetentionResult.DeletedFolders.Count}'. Skipped='{result.RetentionResult.SkippedFolders.Count}'.");
        foreach (var skipped in result.RetentionResult.SkippedFolders)
            _logger.Info($"Run retention skipped Folder='{skipped.FolderPath}'. Reason='{skipped.Reason}'.");

        if (result.PluginType == DevPluginType.Application)
            return result;

        if (!_revitApiContextAvailable)
        {
            _logger.Warn($"Ribbon update skipped. PluginId='{status.Plugin.PluginId}'. Reason='Revit API context unavailable'.");
            return result;
        }

        RibbonRuntimeService.EnsurePluginButton(_commandData.Application, status.Plugin, _logger);
        return result;
    }

    private void Remove(DevPluginStatus status)
    {
        if (_isBusy || status.Installed is null)
            return;

        try
        {
            var removed = _registry.Delete(
                status.Plugin.PluginId,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                out var warnings);
            if (status.Installed.PluginType == DevPluginType.Command)
                RibbonRuntimeService.HidePluginButton(_commandData.Application, status.Plugin.PluginId, _logger);
            _logger.Info($"Plugin removed from RevitDevLoader. PluginId='{status.Plugin.PluginId}'. Removed='{removed}'.");
            foreach (var warning in warnings)
                _logger.Warn($"Plugin add-in manifest preserved. PluginId='{status.Plugin.PluginId}'. Warning='{warning}'.");
            if (warnings.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join("\n\n", warnings),
                    "DevLoader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            _operationStatuses[status.Plugin.PluginId] = status.Installed.PluginType == DevPluginType.Application
                ? "✓ Uninstalled. Restart Revit to unload it. Run folder retained."
                : "✓ Uninstalled. Ribbon button hidden. Run folder retained.";
            RebuildStatusesFromCurrentPackages();
        }
        catch (Exception exception)
        {
            _logger.Error($"Remove failed. PluginId='{status.Plugin.PluginId}'.", exception);
            MessageBox.Show(
                this,
                $"Could not remove {status.Plugin.DisplayName}. Open the logs for details.",
                "DevLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        _rows.IsEnabled = !isBusy;
        if (_checkUpdatesButton is not null)
            _checkUpdatesButton.IsEnabled = !isBusy && !_isChecking;
        Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (!_isBusy)
            return;

        args.Cancel = true;
        _logger.Info("Manager close blocked while an install operation is running.");
        MessageBox.Show(
            this,
            "A plugin is being installed. Close this window after the operation finishes.",
            "DevLoader",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenLogsFolder()
    {
        using var process = Process.Start(new ProcessStartInfo { FileName = FileLogger.LogsRoot, UseShellExecute = true });
    }
}
