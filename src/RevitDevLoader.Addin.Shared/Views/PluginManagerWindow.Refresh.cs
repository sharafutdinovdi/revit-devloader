using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RevitDevLoader.Core;

namespace RevitDevLoader.Views;

public sealed partial class PluginManagerWindow
{
    private void LoadCachedRows()
    {
        try
        {
            _settings = DevLoaderSettings.Load();
            _updatesFolder = _settings.UpdatesFolder;
            Directory.CreateDirectory(_updatesFolder);
            var packages = LoadCachedPackages(_settings, _updatesFolder);
            ApplySnapshot(packages);
        }
        catch (Exception exception)
        {
            ShowRefreshError(exception, "Could not open the saved plugin list");
        }
    }

    private async void CheckUpdates()
    {
        if (_isChecking || _isClosed)
            return;

        _isChecking = true;
        if (_checkUpdatesButton is not null)
        {
            _checkUpdatesButton.IsEnabled = false;
            _checkUpdatesButton.Content = "Checking...";
        }

        try
        {
            _settings = DevLoaderSettings.Load();
            _updatesFolder = _settings.UpdatesFolder;
            Directory.CreateDirectory(_updatesFolder);
            var settings = _settings;
            var updatesFolder = _updatesFolder;
            var sourceResult = await Task.Run(() => _updateSourceService.LoadPackages(updatesFolder, settings));
            if (_isClosed)
                return;

            ApplySnapshot(sourceResult.Packages);
            if (sourceResult.IsEmergencyLocalSource || sourceResult.UsedCachedFeed)
                MessageBox.Show(this, "Could not refresh the feed. Showing saved or local packages. Check Settings or open the logs.",
                    "DevLoader", MessageBoxButton.OK, MessageBoxImage.Warning);
            _logger.Info($"Manager update check completed. RevitVersion='{_revitVersion}'. Packages='{sourceResult.Packages.Count}'. UsedCache='{sourceResult.UsedCachedFeed}'. FeedStatus='{sourceResult.FeedStatus}'.");
        }
        catch (Exception exception)
        {
            if (!_isClosed)
            {
                _logger.Error("Manager update check failed.", exception);
            }
        }
        finally
        {
            _isChecking = false;
            if (!_isClosed && _checkUpdatesButton is not null)
            {
                _checkUpdatesButton.IsEnabled = !_isBusy;
                _checkUpdatesButton.Content = "Check for updates";
            }
        }
    }

    private void ApplySnapshot(IReadOnlyList<DevPayloadPackageInfo> packages)
    {
        _availablePackages = packages ?? Array.Empty<DevPayloadPackageInfo>();
        _allStatuses = new DevPluginStatusService(_registry, _logger.CreateLogger<DevPluginStatusService>())
            .BuildStatuses(DevPluginCatalog.CreateDefault(), _availablePackages, _revitVersion);
        foreach (var status in _allStatuses.Where(HasConventionalInstallation))
        {
            _logger.Warn($"Plugin actions disabled. PluginId='{status.Plugin.PluginId}'. Reason='Conventional installation detected'.");
        }
        ApplySearchFilter();
    }

    private static bool HasConventionalInstallation(DevPluginStatus status)
    {
        return status.Details.IndexOf(
            DevPluginStatusService.ConventionalInstallWarningPrefix,
            StringComparison.Ordinal) >= 0;
    }

    private void RebuildStatusesFromCurrentPackages()
    {
        ApplySnapshot(_availablePackages);
    }

    private void ApplySearchFilter()
    {
        var visible = _allStatuses
            .Where(status => PluginManagerRowState.MatchesSearch(status, _searchBox.Text))
            .ToList();

        _rows.Children.Clear();
        if (visible.Count == 0)
            _rows.Children.Add(CreateEmptyState(_allStatuses.Count > 0));
        else
            foreach (var status in visible)
                _rows.Children.Add(CreatePluginRow(status));
    }

    private static IReadOnlyList<DevPayloadPackageInfo> LoadCachedPackages(
        DevLoaderSettings settings,
        string updatesFolder)
    {
        var packages = new List<DevPayloadPackageInfo>();
        var cachedFeedPath = GetCachedFeedPath(settings.FeedUrl);
        if (!string.IsNullOrWhiteSpace(cachedFeedPath) && File.Exists(cachedFeedPath))
        {
            var feed = new DevUpdateFeedReader().ReadJson(File.ReadAllText(cachedFeedPath));
            packages.AddRange(feed.ToPackageInfos(settings.FeedUrl));
        }
        else if (IsLocalFeed(settings.FeedUrl))
        {
            packages.AddRange(new DevUpdateFeedReader().Read(settings.FeedUrl).ToPackageInfos(settings.FeedUrl));
        }

        if (settings.UseLocalUpdatesFallback || packages.Count == 0)
            packages.AddRange(new DevPayloadPackageDiscovery().FindPackages(updatesFolder));

        return packages
            .GroupBy(item => item.PluginId + "|" + item.ReleaseId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => string.Equals(item.Source, "feed", StringComparison.OrdinalIgnoreCase)).First())
            .OrderByDescending(item => item.CreatedUtc)
            .ToList();
    }

    private static string? GetCachedFeedPath(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "github-release", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || string.IsNullOrWhiteSpace(uri.Host))
            return null;

        var repo = SafeCacheSegment($"{uri.Host}/{Uri.UnescapeDataString(segments[0])}");
        var tag = SafeCacheSegment(Uri.UnescapeDataString(segments[1]));
        var asset = Uri.UnescapeDataString(segments[segments.Length - 1]);
        return Path.Combine(DevUpdateLocations.GetDefaultCacheFolder(), "github-release", repo, tag, asset);
    }

    private static string SafeCacheSegment(string value)
    {
        return value.Replace("/", "_").Replace("\\", "_").Trim();
    }

    private static bool IsLocalFeed(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            return uri.IsFile;

        return File.Exists(source);
    }

    private void ShowRefreshError(Exception exception, string message)
    {
        _logger.Error("Manager refresh failed.", exception);
        _allStatuses = Array.Empty<DevPluginStatus>();
        _availablePackages = Array.Empty<DevPayloadPackageInfo>();
        _rows.Children.Clear();
        _rows.Children.Add(CreateErrorState($"{message}. Open the logs and check settings."));
    }

    private void OpenSettings()
    {
        var window = new PluginSettingsWindow(this, DevUpdateLocations.GetSettingsPath());
        if (window.ShowDialog() != true)
            return;

        LoadCachedRows();
        CheckUpdates();
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        _isClosed = true;
        _revitApiContextAvailable = false;
    }
}
