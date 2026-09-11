using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevLoaderSettingsTests
{
    [Fact]
    public void BuildGitHubReleaseFeedUrlRequiresExplicitTag()
    {
        Assert.Throws<ArgumentException>(() => DevUpdateLocations.BuildGitHubReleaseFeedUrl("owner/repo"));
    }

    [Fact]
    public void BuildGitHubReleaseFeedUrlUsesConfiguredTagAndAsset()
    {
        var url = DevUpdateLocations.BuildGitHubReleaseFeedUrl("owner/repo", "preview", "custom-feed.json");

        Assert.Equal("github-release://owner/repo/preview/custom-feed.json", url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("owner/repo/extra")]
    [InlineData("owner name/repo")]
    [InlineData("owner/repo name")]
    public void BuildGitHubReleaseFeedUrlRejectsInvalidRepository(string repo)
    {
        var exception = Assert.Throws<ArgumentException>(() => DevUpdateLocations.BuildGitHubReleaseFeedUrl(repo));

        Assert.Contains("owner/name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadResolvesFeedByDocumentedPriority()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["feedUrl"] = "settings-url",
            ["feedRepo"] = "settings/repo",
            ["feedTag"] = "settings-tag",
            ["feedAsset"] = "settings-asset.json"
        };

        var environmentUrl = DevLoaderSettings.Load(settings, key => key switch
        {
            "REVITDEVLOADER_FEED_URL" => "environment-url",
            "REVITDEVLOADER_FEED_REPO" => "environment/repo",
            _ => null
        });
        var settingsUrl = DevLoaderSettings.Load(settings, key =>
            key == "REVITDEVLOADER_FEED_REPO" ? "environment/repo" : null);
        settings.Remove("feedUrl");
        var environmentRepo = DevLoaderSettings.Load(settings, key =>
            key == "REVITDEVLOADER_FEED_REPO" ? "environment/repo" : null);
        var settingsRepo = DevLoaderSettings.Load(settings, _ => null);
        settings.Clear();
        var empty = DevLoaderSettings.Load(settings, _ => null);

        Assert.Equal("environment-url", environmentUrl.FeedUrl);
        Assert.Equal("settings-url", settingsUrl.FeedUrl);
        Assert.Equal("github-release://environment/repo/settings-tag/settings-asset.json", environmentRepo.FeedUrl);
        Assert.Equal("github-release://settings/repo/settings-tag/settings-asset.json", settingsRepo.FeedUrl);
        Assert.Empty(empty.FeedUrl);
    }

    [Fact]
    public void LoadResolvesConfiguredPathsByDocumentedPriority()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["updatesFolder"] = "settings-updates",
            ["testFeedPath"] = "settings-feed.json"
        };

        var environment = DevLoaderSettings.Load(settings, key => key switch
        {
            "REVITDEVLOADER_UPDATES_DIR" => "environment-updates",
            "REVITDEVLOADER_TEST_FEED" => "environment-feed.json",
            _ => null
        });
        var configured = DevLoaderSettings.Load(settings, _ => null);
        settings.Clear();
        var defaults = DevLoaderSettings.Load(settings, _ => null);

        Assert.Equal("environment-updates", environment.UpdatesFolder);
        Assert.Equal("environment-feed.json", environment.TestFeedPath);
        Assert.Equal("settings-updates", configured.UpdatesFolder);
        Assert.Equal("settings-feed.json", configured.TestFeedPath);
        Assert.Equal(DevUpdateLocations.GetDefaultUpdatesFolder(), defaults.UpdatesFolder);
        Assert.Equal(DevUpdateLocations.GetDefaultTestFeedManifestPath(), defaults.TestFeedPath);
    }

    [Fact]
    public void LoadResolvesRunRetentionByDocumentedPriority()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["runRetentionCount"] = "5"
        };

        var environment = DevLoaderSettings.Load(settings, key =>
            key == "REVITDEVLOADER_RUN_RETENTION" ? "7" : null);
        var configured = DevLoaderSettings.Load(settings, _ => null);

        Assert.Equal(7, environment.RunRetentionCount);
        Assert.Equal(5, configured.RunRetentionCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("invalid")]
    [InlineData("-1")]
    public void LoadUsesDefaultForInvalidRunRetention(string value)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["runRetentionCount"] = value
        };

        var result = DevLoaderSettings.Load(settings, _ => null);

        Assert.Equal(DevLoaderSettings.DefaultRunRetentionCount, result.RunRetentionCount);
    }
}
