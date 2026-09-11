using System;
using System.Collections.Generic;
using System.IO;

namespace RevitDevLoader.Core;

public sealed class DevLoaderSettings
{
    public const int DefaultRunRetentionCount = 3;

    public DevLoaderSettings(
        string feedUrl,
        bool useLocalUpdatesFallback,
        string? updatesFolder = null,
        string? testFeedPath = null,
        int runRetentionCount = DefaultRunRetentionCount)
    {
        FeedUrl = feedUrl ?? string.Empty;
        UseLocalUpdatesFallback = useLocalUpdatesFallback;
        UpdatesFolder = NormalizeOptionalPath(updatesFolder, DevUpdateLocations.GetDefaultUpdatesFolder());
        TestFeedPath = NormalizeOptionalPath(testFeedPath, DevUpdateLocations.GetDefaultTestFeedManifestPath());
        RunRetentionCount = runRetentionCount < 1 ? DefaultRunRetentionCount : runRetentionCount;
    }

    public string FeedUrl { get; }

    public bool UseLocalUpdatesFallback { get; }

    public string UpdatesFolder { get; }

    public string TestFeedPath { get; }

    public int RunRetentionCount { get; }

    public static DevLoaderSettings Load()
    {
        var values = ReadSettingsFile(DevUpdateLocations.GetSettingsPath());
        return Load(values, Environment.GetEnvironmentVariable);
    }

    public static DevLoaderSettings Load(
        IReadOnlyDictionary<string, string> values,
        Func<string, string?> getEnvironmentVariable)
    {
        if (values is null)
            throw new ArgumentNullException(nameof(values));
        if (getEnvironmentVariable is null)
            throw new ArgumentNullException(nameof(getEnvironmentVariable));

        var feedUrl = GetEnvironmentValue(getEnvironmentVariable, "REVITDEVLOADER_FEED_URL");
        if (string.IsNullOrWhiteSpace(feedUrl))
            feedUrl = Get(values, "feedUrl");
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            var environmentRepo = GetEnvironmentValue(getEnvironmentVariable, "REVITDEVLOADER_FEED_REPO");
            var settingsRepo = Get(values, "feedRepo");
            var repo = string.IsNullOrWhiteSpace(environmentRepo) ? settingsRepo : environmentRepo;
            if (!string.IsNullOrWhiteSpace(repo))
            {
                feedUrl = DevUpdateLocations.BuildGitHubReleaseFeedUrl(
                    repo,
                    DefaultIfEmpty(Get(values, "feedTag"), DevUpdateLocations.DefaultFeedTag),
                    DefaultIfEmpty(Get(values, "feedAsset"), DevUpdateLocations.DefaultFeedAsset));
            }
        }

        var fallback = string.Equals(Get(values, "useLocalUpdatesFallback"), "true", StringComparison.OrdinalIgnoreCase);
        var updatesFolder = ResolvePath(
            values,
            getEnvironmentVariable,
            "REVITDEVLOADER_UPDATES_DIR",
            "updatesFolder",
            DevUpdateLocations.GetDefaultUpdatesFolder());
        var testFeedPath = ResolvePath(
            values,
            getEnvironmentVariable,
            "REVITDEVLOADER_TEST_FEED",
            "testFeedPath",
            DevUpdateLocations.GetDefaultTestFeedManifestPath());
        var runRetentionCount = ResolveRunRetentionCount(values, getEnvironmentVariable);
        return new DevLoaderSettings(feedUrl.Trim(), fallback, updatesFolder, testFeedPath, runRetentionCount);
    }

    private static Dictionary<string, string> ReadSettingsFile(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return DevPayloadPackageDiscovery.ReadProperties(File.ReadAllText(path));
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
    }

    private static string GetEnvironmentValue(Func<string, string?> getEnvironmentVariable, string key)
    {
        return getEnvironmentVariable(key)?.Trim() ?? string.Empty;
    }

    private static string DefaultIfEmpty(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string ResolvePath(
        IReadOnlyDictionary<string, string> values,
        Func<string, string?> getEnvironmentVariable,
        string environmentKey,
        string settingsKey,
        string defaultValue)
    {
        var environmentValue = GetEnvironmentValue(getEnvironmentVariable, environmentKey);
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue;

        return DefaultIfEmpty(Get(values, settingsKey), defaultValue);
    }

    private static string NormalizeOptionalPath(string? value, string defaultValue)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(normalized) ? defaultValue : normalized;
    }

    private static int ResolveRunRetentionCount(
        IReadOnlyDictionary<string, string> values,
        Func<string, string?> getEnvironmentVariable)
    {
        var environmentValue = GetEnvironmentValue(getEnvironmentVariable, "REVITDEVLOADER_RUN_RETENTION");
        var value = string.IsNullOrWhiteSpace(environmentValue) ? Get(values, "runRetentionCount") : environmentValue;
        return int.TryParse(value, out var retentionCount) && retentionCount >= 1
            ? retentionCount
            : DefaultRunRetentionCount;
    }
}
