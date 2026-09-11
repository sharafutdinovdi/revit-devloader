using System;
using System.IO;
using System.Linq;

namespace RevitDevLoader.Core;

public static class DevUpdateLocations
{
    public const string DefaultFeedTag = "";
    public const string DefaultFeedAsset = "feed.json";

    public static string GetDefaultUpdatesFolder()
    {
        return GetDefaultUpdatesFolder(GetLocalApplicationDataRoot());
    }

    public static string GetDefaultUpdatesFolder(string localApplicationDataRoot)
    {
        if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
            throw new ArgumentException("Local application data root is required.", nameof(localApplicationDataRoot));

        return Path.Combine(localApplicationDataRoot, "RevitDevLoader", "updates");
    }

    public static string GetDefaultTestFeedManifestPath()
    {
        return GetDefaultTestFeedManifestPath(GetLocalApplicationDataRoot());
    }

    public static string GetDefaultTestFeedManifestPath(string localApplicationDataRoot)
    {
        if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
            throw new ArgumentException("Local application data root is required.", nameof(localApplicationDataRoot));

        return Path.Combine(localApplicationDataRoot, "RevitDevLoader", "test-feed", "feed.json");
    }

    public static string BuildGitHubReleaseFeedUrl(
        string repo,
        string tag = DefaultFeedTag,
        string asset = DefaultFeedAsset)
    {
        var normalizedRepo = ValidateGitHubRepository(repo);
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("GitHub release tag is required.", nameof(tag));
        if (string.IsNullOrWhiteSpace(asset))
            throw new ArgumentException("GitHub release asset name is required.", nameof(asset));

        return $"github-release://{normalizedRepo}/{Uri.EscapeDataString(tag.Trim())}/{Uri.EscapeDataString(asset.Trim())}";
    }

    public static string GetDefaultCacheFolder()
    {
        return Path.Combine(GetLocalApplicationDataRoot(), "RevitDevLoader", "cache");
    }

    public static string GetSettingsPath()
    {
        return Path.Combine(GetLocalApplicationDataRoot(), "RevitDevLoader", "settings.properties");
    }

    private static string ValidateGitHubRepository(string repo)
    {
        if (string.IsNullOrWhiteSpace(repo))
            throw new ArgumentException("GitHub repository must use the 'owner/name' format.", nameof(repo));

        var normalized = repo.Trim();
        var segments = normalized.Split('/');
        if (segments.Length != 2 ||
            segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment.Any(char.IsWhiteSpace) || segment.Contains('\\')))
        {
            throw new ArgumentException("GitHub repository must use the 'owner/name' format without spaces or nested slashes.", nameof(repo));
        }

        return normalized;
    }

    private static string GetLocalApplicationDataRoot()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localApplicationData) ? Path.GetTempPath() : localApplicationData;
    }
}
