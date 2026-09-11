using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RevitDevLoader.Core;

public sealed class DevUpdateSourceService
{
    private readonly DevPayloadPackageDiscovery _discovery = new();
    private readonly DevUpdateFeedReader _feedReader = new();
    private readonly ILogger<DevUpdateSourceService> _logger;

    public DevUpdateSourceService(ILogger<DevUpdateSourceService>? logger = null)
    {
        _logger = logger ?? NullLogger<DevUpdateSourceService>.Instance;
    }

    public DevUpdateSourceResult LoadPackages(string updatesFolder, DevLoaderSettings settings)
    {
        _logger.LogInformation("LoadPackages started. UpdatesFolder='{UpdatesFolder}'. FeedUrl='{FeedUrl}'.", updatesFolder, settings?.FeedUrl ?? string.Empty);
        settings ??= new DevLoaderSettings(string.Empty, useLocalUpdatesFallback: false);
        var packages = new List<DevPayloadPackageInfo>();
        var feedStatus = "Feed: not configured";
        var feedPackageCount = 0;
        var localPackageCount = 0;
        var useEmergencyLocalSource = false;
        var usedCachedFeed = false;
        var hasUnsupportedFeedSchema = false;

        if (!string.IsNullOrWhiteSpace(settings.FeedUrl))
        {
            try
            {
                var readResult = _feedReader.ReadWithStatus(settings.FeedUrl);
                var feedPackages = readResult.Feed.ToPackageInfos(settings.FeedUrl);
                packages.AddRange(feedPackages);
                feedPackageCount = feedPackages.Count;
                usedCachedFeed = readResult.UsedCache;
                feedStatus = readResult.UsedCache
                    ? $"Feed cache used because the fresh source is unavailable: {settings.FeedUrl}"
                    : $"Feed: {settings.FeedUrl}";
                if (readResult.Feed.SchemaVersion > DevFeedSchema.SupportedVersion)
                {
                    hasUnsupportedFeedSchema = true;
                    feedStatus += $" Warning: feed schema version {readResult.Feed.SchemaVersion} is newer than supported version {DevFeedSchema.SupportedVersion}.";
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Feed load failed. FeedUrl='{FeedUrl}'.", settings.FeedUrl);
                useEmergencyLocalSource = true;
                feedStatus = $"Feed error: {exception.Message} Emergency local source: {updatesFolder}.";
            }
        }
        else
        {
            useEmergencyLocalSource = true;
            feedStatus = $"Feed is not configured. Set feedUrl or feedRepo in settings.properties. Emergency local source: {updatesFolder}.";
        }

        if (settings.UseLocalUpdatesFallback || useEmergencyLocalSource)
        {
            var localPackages = _discovery.FindPackages(updatesFolder);
            packages.AddRange(localPackages);
            localPackageCount = localPackages.Count;
        }

        var result = new DevUpdateSourceResult(
            packages
                .GroupBy(item => item.PluginId + "|" + item.ReleaseId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => string.Equals(item.Source, "feed", StringComparison.OrdinalIgnoreCase)).First())
                .OrderByDescending(item => item.CreatedUtc)
                .ToList(),
            feedStatus,
            feedPackageCount,
            localPackageCount,
            useEmergencyLocalSource,
            usedCachedFeed,
            hasUnsupportedFeedSchema);
        _logger.LogInformation("LoadPackages completed. Packages={PackageCount}. FeedPackages={FeedPackageCount}. LocalPackages={LocalPackageCount}. EmergencyLocalSource={EmergencyLocalSource}.", result.Packages.Count, feedPackageCount, localPackageCount, useEmergencyLocalSource);
        return result;
    }
}

public sealed class DevUpdateSourceResult
{
    public DevUpdateSourceResult(
        IReadOnlyList<DevPayloadPackageInfo> packages,
        string feedStatus,
        int feedPackageCount,
        int localPackageCount,
        bool isEmergencyLocalSource,
        bool usedCachedFeed,
        bool hasUnsupportedFeedSchema)
    {
        Packages = packages ?? Array.Empty<DevPayloadPackageInfo>();
        FeedStatus = feedStatus ?? string.Empty;
        FeedPackageCount = feedPackageCount;
        LocalPackageCount = localPackageCount;
        IsEmergencyLocalSource = isEmergencyLocalSource;
        UsedCachedFeed = usedCachedFeed;
        HasUnsupportedFeedSchema = hasUnsupportedFeedSchema;
    }

    public IReadOnlyList<DevPayloadPackageInfo> Packages { get; }

    public string FeedStatus { get; }

    public int FeedPackageCount { get; }

    public int LocalPackageCount { get; }

    public bool IsEmergencyLocalSource { get; }

    public bool UsedCachedFeed { get; }

    public bool HasUnsupportedFeedSchema { get; }
}
