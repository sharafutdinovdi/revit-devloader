using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RevitDevLoader.Core;

namespace RevitDevLoader.Views;

internal sealed class DevLoaderSettingsEditorModel
{
    public string FeedRepository { get; set; } = string.Empty;

    public string FeedTag { get; set; } = DevUpdateLocations.DefaultFeedTag;

    public string FeedAsset { get; set; } = DevUpdateLocations.DefaultFeedAsset;

    public string UpdatesFolder { get; set; } = DevUpdateLocations.GetDefaultUpdatesFolder();

    public int RunRetentionCount { get; set; } = DevLoaderSettings.DefaultRunRetentionCount;

    public bool UseLocalUpdatesFallback { get; set; }

    public string UnmanagedFeedUrl { get; set; } = string.Empty;
}

internal static class DevLoaderSettingsEditorStore
{
    private static readonly string[] ManagedKeys =
    {
        "feedRepo",
        "feedTag",
        "feedAsset",
        "updatesFolder",
        "runRetentionCount",
        "useLocalUpdatesFallback"
    };

    public static DevLoaderSettingsEditorModel Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Settings path is required.", nameof(path));

        var values = ReadValues(path);
        var model = new DevLoaderSettingsEditorModel
        {
            FeedRepository = Get(values, "feedRepo"),
            FeedTag = DefaultIfEmpty(Get(values, "feedTag"), DevUpdateLocations.DefaultFeedTag),
            FeedAsset = DefaultIfEmpty(Get(values, "feedAsset"), DevUpdateLocations.DefaultFeedAsset),
            UpdatesFolder = DefaultIfEmpty(Get(values, "updatesFolder"), DevUpdateLocations.GetDefaultUpdatesFolder()),
            RunRetentionCount = ParseRetention(Get(values, "runRetentionCount")),
            UseLocalUpdatesFallback = string.Equals(
                Get(values, "useLocalUpdatesFallback"),
                "true",
                StringComparison.OrdinalIgnoreCase)
        };

        if (string.IsNullOrWhiteSpace(model.FeedRepository))
        {
            var feedUrl = Get(values, "feedUrl");
            if (!TryApplyGitHubFeedUrl(feedUrl, model))
                model.UnmanagedFeedUrl = feedUrl;
        }

        return model;
    }

    public static void Save(string path, DevLoaderSettingsEditorModel model)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Settings path is required.", nameof(path));
        if (model is null)
            throw new ArgumentNullException(nameof(model));
        if (model.RunRetentionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(model), "Retention count must be at least one.");

        var repo = model.FeedRepository.Trim();
        var tag = DefaultIfEmpty(model.FeedTag, DevUpdateLocations.DefaultFeedTag);
        var asset = DefaultIfEmpty(model.FeedAsset, DevUpdateLocations.DefaultFeedAsset);
        if (repo.Length > 0)
            _ = DevUpdateLocations.BuildGitHubReleaseFeedUrl(repo, tag, asset);

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["feedRepo"] = repo,
            ["feedTag"] = tag,
            ["feedAsset"] = asset,
            ["updatesFolder"] = model.UpdatesFolder.Trim(),
            ["runRetentionCount"] = model.RunRetentionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["useLocalUpdatesFallback"] = model.UseLocalUpdatesFallback ? "true" : "false"
        };
        var preservedFeedUrl = repo.Length == 0 ? model.UnmanagedFeedUrl.Trim() : string.Empty;

        var sourceLines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        var writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var line in sourceLines)
        {
            var key = TryGetKey(line);
            if (string.Equals(key, "feedUrl", StringComparison.OrdinalIgnoreCase))
            {
                if (preservedFeedUrl.Length > 0 && writtenKeys.Add("feedUrl"))
                    result.Add($"feedUrl={preservedFeedUrl}");
                continue;
            }
            if (key is null || !replacements.TryGetValue(key, out var value))
            {
                result.Add(line);
                continue;
            }

            if (writtenKeys.Add(key))
                result.Add($"{key}={value}");
        }

        foreach (var key in ManagedKeys.Where(key => writtenKeys.Add(key)))
            result.Add($"{key}={replacements[key]}");
        if (preservedFeedUrl.Length > 0 && writtenKeys.Add("feedUrl"))
            result.Add($"feedUrl={preservedFeedUrl}");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, string.Join(Environment.NewLine, result) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static Dictionary<string, string> ReadValues(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return values;

        foreach (var line in File.ReadAllLines(path))
        {
            var key = TryGetKey(line);
            if (key is null)
                continue;

            var separator = line.IndexOf('=');
            values[key] = line.Substring(separator + 1).Trim();
        }

        return values;
    }

    private static string? TryGetKey(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
            return null;

        var separator = trimmed.IndexOf('=');
        return separator <= 0 ? null : trimmed.Substring(0, separator).Trim();
    }

    private static bool TryApplyGitHubFeedUrl(string value, DevLoaderSettingsEditorModel model)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "github-release", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || string.IsNullOrWhiteSpace(uri.Host))
            return false;

        model.FeedRepository = $"{uri.Host}/{Uri.UnescapeDataString(segments[0])}";
        model.FeedTag = Uri.UnescapeDataString(segments[1]);
        model.FeedAsset = Uri.UnescapeDataString(segments[segments.Length - 1]);
        return true;
    }

    private static int ParseRetention(string value)
    {
        return int.TryParse(value, out var count) && count >= 1
            ? count
            : DevLoaderSettings.DefaultRunRetentionCount;
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
    }

    private static string DefaultIfEmpty(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }
}
