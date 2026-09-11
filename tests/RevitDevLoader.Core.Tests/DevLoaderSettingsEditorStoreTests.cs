using RevitDevLoader.Views;

namespace RevitDevLoader.Core.Tests;

public sealed class DevLoaderSettingsEditorStoreTests
{
    [Fact]
    public void LoadsFieldsUsedBySettingsWindow()
    {
        var path = CreateSettingsFile(
            "feedRepo=owner/repo\n" +
            "feedTag=preview\n" +
            "feedAsset=custom.json\n" +
            "updatesFolder=C:\\Updates\n" +
            "runRetentionCount=7\n" +
            "useLocalUpdatesFallback=true\n");

        var model = DevLoaderSettingsEditorStore.Load(path);

        Assert.Equal("owner/repo", model.FeedRepository);
        Assert.Equal("preview", model.FeedTag);
        Assert.Equal("custom.json", model.FeedAsset);
        Assert.Equal("C:\\Updates", model.UpdatesFolder);
        Assert.Equal(7, model.RunRetentionCount);
        Assert.True(model.UseLocalUpdatesFallback);
    }

    [Fact]
    public void LoadsLegacyGitHubFeedUrlIntoEditableFields()
    {
        var path = CreateSettingsFile("feedUrl=github-release://owner/repo/testing/feed.json\n");

        var model = DevLoaderSettingsEditorStore.Load(path);

        Assert.Equal("owner/repo", model.FeedRepository);
        Assert.Equal("testing", model.FeedTag);
        Assert.Equal("feed.json", model.FeedAsset);
    }

    [Fact]
    public void SavesWindowFieldsAndPreservesUnmanagedSettings()
    {
        var path = CreateSettingsFile(
            "# keep this comment\n" +
            "feedUrl=github-release://old/repo/old/old.json\n" +
            "testFeedPath=C:\\Feeds\\test.json\n" +
            "runRetentionCount=2\n");
        var model = new DevLoaderSettingsEditorModel
        {
            FeedRepository = "new/repo",
            FeedTag = "preview",
            FeedAsset = "feed-preview.json",
            UpdatesFolder = "C:\\Updates",
            RunRetentionCount = 5,
            UseLocalUpdatesFallback = true
        };

        DevLoaderSettingsEditorStore.Save(path, model);
        var saved = File.ReadAllText(path);
        var reloaded = DevLoaderSettingsEditorStore.Load(path);

        Assert.Contains("# keep this comment", saved);
        Assert.Contains("testFeedPath=C:\\Feeds\\test.json", saved);
        Assert.DoesNotContain("feedUrl=", saved, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("new/repo", reloaded.FeedRepository);
        Assert.Equal("preview", reloaded.FeedTag);
        Assert.Equal("feed-preview.json", reloaded.FeedAsset);
        Assert.Equal("C:\\Updates", reloaded.UpdatesFolder);
        Assert.Equal(5, reloaded.RunRetentionCount);
        Assert.True(reloaded.UseLocalUpdatesFallback);
    }

    [Fact]
    public void PreservesDirectFeedUrlThatWindowDoesNotEdit()
    {
        var path = CreateSettingsFile("feedUrl=https://updates.example.test/feed.json\n");
        var model = DevLoaderSettingsEditorStore.Load(path);

        model.RunRetentionCount = 4;
        DevLoaderSettingsEditorStore.Save(path, model);

        Assert.Contains("feedUrl=https://updates.example.test/feed.json", File.ReadAllText(path));
        Assert.Equal("https://updates.example.test/feed.json", DevLoaderSettingsEditorStore.Load(path).UnmanagedFeedUrl);
    }

    private static string CreateSettingsFile(string contents)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "settings.properties");
        File.WriteAllText(path, contents);
        return path;
    }
}
