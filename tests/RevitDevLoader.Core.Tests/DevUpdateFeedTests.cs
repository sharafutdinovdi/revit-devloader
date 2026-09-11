using System.IO.Compression;
using System.Text;
using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevUpdateFeedTests
{
    [Fact]
    public void FeedReaderParsesRelativePackageUrls()
    {
        var feedRoot = CreateTempFolder();
        var packagePath = Path.Combine(feedRoot, "packages", "ExampleCommand-DevPayload-release-a.zip");
        CreatePayloadZip(packagePath, "release-a", "ExampleCommand");
        var hash = DevUpdatePackageCache.ComputeSha256(packagePath);
        var feedPath = Path.Combine(feedRoot, "feed.json");
        File.WriteAllText(feedPath, FeedJson("ExampleCommand", "release-a", "packages/ExampleCommand-DevPayload-release-a.zip", hash, new FileInfo(packagePath).Length), Encoding.UTF8);

        var readResult = new DevUpdateFeedReader().ReadWithStatus(feedPath);
        var packages = readResult.Feed.ToPackageInfos(feedPath);

        Assert.False(readResult.UsedCache);
        var package = Assert.Single(packages);
        Assert.Equal("ExampleCommand", package.PluginId);
        Assert.Equal("release-a", package.ReleaseId);
        Assert.Equal(packagePath, package.PackagePath);
        Assert.Equal(hash, package.Sha256);
        Assert.Equal("feed", package.Source);
        Assert.Equal(DevPluginType.Command, package.PluginType);
    }

    [Fact]
    public void FeedReaderParsesApplicationDeliveryFields()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "plugins": [{
            "pluginId": "SampleApplication",
            "displayName": "SampleApplication",
            "versions": [{
              "releaseId": "release-a",
              "assemblyVersion": "1.0.0",
              "createdUtc": "2026-08-12T00:00:00Z",
              "supportedRevit": ["2026"],
              "url": "SampleApplication.zip",
              "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
              "size": 123,
              "pluginType": "application",
              "applicationClass": "SampleApplication.Application",
              "mainAssembly": "SampleApplication.dll"
            }]
          }]
        }
        """;

        var package = Assert.Single(new DevUpdateFeedReader().ReadJson(json).ToPackageInfos(CreateTempFolder()));

        Assert.Equal(DevPluginType.Application, package.PluginType);
        Assert.Equal("SampleApplication.Application", package.ApplicationClass);
        Assert.Empty(package.CommandType);
    }

    [Fact]
    public void FeedResolvesGithubReleasePackageUrls()
    {
        var packages = new DevUpdateFeedReader()
            .ReadJson(FeedJson("ExampleCommand", "release-a", "ExampleCommand-DevPayload-release-a.zip", new string('A', 64), 123))
            .ToPackageInfos("github-release://owner/repo/test-feed/feed.json");

        var package = Assert.Single(packages);
        Assert.Equal("github-release://owner/repo/test-feed/ExampleCommand-DevPayload-release-a.zip", package.PackagePath);
        Assert.Equal("feed", package.Source);
    }

    [Fact]
    public void PackageCacheRejectsHashMismatch()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-a.zip");
        CreatePayloadZip(packagePath, "release-a", "ExampleCommand");
        var package = new DevPayloadPackageInfo(
            packagePath,
            "ExampleCommand",
            "ExampleCommand",
            "release-a",
            "1.0.0.0",
            "ExampleCommand.Commands.OpenExampleCommandCommand",
            "ExampleCommand.dll",
            DateTime.UtcNow,
            new[] { "2024", "2026" },
            2,
            source: "feed",
            sha256: new string('0', 64),
            sizeBytes: new FileInfo(packagePath).Length);

        var exception = Assert.Throws<DevManifestException>(
            () => new DevUpdatePackageCache().PreparePackage(package, CreateTempFolder()));

        Assert.Contains("sha256", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceServiceCombinesFeedAndLocalFallback()
    {
        var root = CreateTempFolder();
        var feedRoot = Path.Combine(root, "feed");
        var updatesRoot = Path.Combine(root, "updates");
        Directory.CreateDirectory(feedRoot);
        Directory.CreateDirectory(updatesRoot);

        var feedPackage = Path.Combine(feedRoot, "ExampleCommand-DevPayload-feed.zip");
        var localPackage = Path.Combine(updatesRoot, "SampleCommand-DevPayload-local.zip");
        CreatePayloadZip(feedPackage, "feed-release", "ExampleCommand");
        CreatePayloadZip(localPackage, "local-release", "SampleCommand");
        var feedPath = Path.Combine(feedRoot, "feed.json");
        File.WriteAllText(
            feedPath,
            FeedJson("ExampleCommand", "feed-release", "ExampleCommand-DevPayload-feed.zip", DevUpdatePackageCache.ComputeSha256(feedPackage), new FileInfo(feedPackage).Length),
            Encoding.UTF8);

        var result = new DevUpdateSourceService().LoadPackages(
            updatesRoot,
            new DevLoaderSettings(feedPath, useLocalUpdatesFallback: true));

        Assert.Equal(2, result.Packages.Count);
        Assert.Equal(1, result.FeedPackageCount);
        Assert.Equal(1, result.LocalPackageCount);
        Assert.Contains(result.Packages, item => item.PluginId == "ExampleCommand" && item.Source == "feed");
        Assert.Contains(result.Packages, item => item.PluginId == "SampleCommand" && item.Source == "local");
    }

    [Fact]
    public void SourceServiceUsesEmergencyLocalSourceWhenFeedIsNotConfigured()
    {
        var updatesRoot = CreateTempFolder();
        CreatePayloadZip(
            Path.Combine(updatesRoot, "SampleCommand-DevPayload-local.zip"),
            "local-release",
            "SampleCommand");

        var result = new DevUpdateSourceService().LoadPackages(updatesRoot, null!);

        var package = Assert.Single(result.Packages);
        Assert.Equal("SampleCommand", package.PluginId);
        Assert.Equal(1, result.LocalPackageCount);
        Assert.True(result.IsEmergencyLocalSource);
        Assert.Contains("not configured", result.FeedStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("feedUrl", result.FeedStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceServiceUsesEmergencyLocalSourceWhenConfiguredFeedFails()
    {
        var updatesRoot = CreateTempFolder();
        CreatePayloadZip(
            Path.Combine(updatesRoot, "SampleCommand-DevPayload-local.zip"),
            "local-release",
            "SampleCommand");
        var missingFeed = Path.Combine(CreateTempFolder(), "missing-feed.json");

        var result = new DevUpdateSourceService().LoadPackages(
            updatesRoot,
            new DevLoaderSettings(missingFeed, useLocalUpdatesFallback: false));

        var package = Assert.Single(result.Packages);
        Assert.Equal("SampleCommand", package.PluginId);
        Assert.Equal(1, result.LocalPackageCount);
        Assert.True(result.IsEmergencyLocalSource);
        Assert.Contains("Feed error", result.FeedStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Emergency local source", result.FeedStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceServiceWarnsButReadsNewerFeedSchema()
    {
        var feedRoot = CreateTempFolder();
        var packagePath = Path.Combine(feedRoot, "ExampleCommand-DevPayload-release-a.zip");
        CreatePayloadZip(packagePath, "release-a", "ExampleCommand");
        var feedPath = Path.Combine(feedRoot, "feed.json");
        File.WriteAllText(
            feedPath,
            FeedJson(
                "ExampleCommand",
                "release-a",
                "ExampleCommand-DevPayload-release-a.zip",
                DevUpdatePackageCache.ComputeSha256(packagePath),
                new FileInfo(packagePath).Length,
                schemaVersion: DevFeedSchema.SupportedVersion + 1),
            Encoding.UTF8);

        var result = new DevUpdateSourceService().LoadPackages(
            CreateTempFolder(),
            new DevLoaderSettings(feedPath, useLocalUpdatesFallback: false));

        Assert.Single(result.Packages);
        Assert.True(result.HasUnsupportedFeedSchema);
        Assert.Contains("newer than supported", result.FeedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static string FeedJson(
        string pluginId,
        string releaseId,
        string url,
        string sha256,
        long size,
        int schemaVersion = DevFeedSchema.SupportedVersion)
    {
        return $$"""
        {
          "schemaVersion": {{schemaVersion}},
          "channel": "testing",
          "generatedUtc": "2026-06-24T00:00:00Z",
          "plugins": [
            {
              "pluginId": "{{pluginId}}",
              "displayName": "{{pluginId}}",
              "versions": [
                {
                  "releaseId": "{{releaseId}}",
                  "assemblyVersion": "1.0.{{releaseId}}",
                  "createdUtc": "2026-06-24T00:00:00Z",
                  "supportedRevit": ["2024", "2026"],
                  "url": "{{url}}",
                  "sha256": "{{sha256}}",
                  "size": {{size}},
                  "commandType": "{{pluginId}}.Commands.Open{{pluginId}}Command",
                  "mainAssembly": "{{pluginId}}.dll"
                }
              ]
            }
          ]
        }
        """;
    }

    private static string CreateTempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreatePayloadZip(string packagePath, string releaseId, string pluginId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        AddText(archive, "release-info.properties", string.Join(
            Environment.NewLine,
            "schemaVersion=2",
            $"pluginId={pluginId}",
            $"pluginName={pluginId}",
            $"displayName={pluginId}",
            $"releaseId={releaseId}",
            $"assemblyVersion=1.0.{releaseId}",
            "createdUtc=2026-06-24T00:00:00Z",
            $"commandType={pluginId}.Commands.Open{pluginId}Command",
            $"mainAssembly={pluginId}.dll",
            "versions=2024,2026"));
        AddText(archive, $"payload/2024/{pluginId}.dll", "2024");
        AddText(archive, $"payload/2026/{pluginId}.dll", "2026");
    }

    private static void AddText(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
