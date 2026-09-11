using RevitDevLoader.Core;
using Xunit.Abstractions;

namespace RevitDevLoader.Core.Tests;

public sealed class FeedDeliverySmokeTests
{
    private const string GateVariable = "REVITDEVLOADER_FEED_CHECK";

    private readonly ITestOutputHelper _output;

    public FeedDeliverySmokeTests(ITestOutputHelper output) => _output = output;

    private static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable(GateVariable), "1", StringComparison.Ordinal);

    [Fact]
    public void Feed_DeliversAndInstallsPlugin()
    {
        if (!Enabled)
        {
            return;
        }

        var settings = DevLoaderSettings.Load();
        _output.WriteLine($"feedUrl  = {settings.FeedUrl}");
        _output.WriteLine($"fallback = {settings.UseLocalUpdatesFallback}");
        _output.WriteLine($"updates  = {settings.UpdatesFolder}");

        Assert.False(string.IsNullOrWhiteSpace(settings.FeedUrl), "Feed is not configured. Set a feed before running this test.");

        var source = new DevUpdateSourceService();
        var packages = source.LoadPackages(settings.UpdatesFolder, settings);

        _output.WriteLine($"status   = {packages.FeedStatus}");
        _output.WriteLine($"from feed= {packages.FeedPackageCount}");
        _output.WriteLine($"local    = {packages.LocalPackageCount}");
        _output.WriteLine($"cached   = {packages.UsedCachedFeed}");
        _output.WriteLine($"emergency= {packages.IsEmergencyLocalSource}");

        Assert.False(packages.IsEmergencyLocalSource, $"Feed unavailable: {packages.FeedStatus}");
        Assert.True(packages.FeedPackageCount > 0, "The feed contains no packages.");

        var package = packages.Packages.FirstOrDefault(item =>
            string.Equals(item.Source, "feed", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(package);
        _output.WriteLine($"package  = {package!.PluginId} {package.ReleaseId}");
        _output.WriteLine($"path     = {package.PackagePath}");

        var cached = new DevUpdatePackageCache().PreparePackage(package, DevUpdateLocations.GetDefaultCacheFolder());
        _output.WriteLine($"downloaded= {cached}");
        Assert.True(File.Exists(cached), $"Feed package was not downloaded: {cached}");

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var installed = new DevPayloadInstaller().Install(cached, root, package.Versions);

        _output.WriteLine($"installed: {installed.PluginId} {installed.ReleaseId} (assembly {installed.AssemblyVersion})");
        _output.WriteLine($"directory: {installed.RunRoot}");
        _output.WriteLine($"manifest : {installed.ManifestPath}");

        Assert.True(File.Exists(installed.ManifestPath), "Installed plugin manifest was not created.");

        foreach (var version in installed.Versions)
        {
            _output.WriteLine($"  Revit {version.RevitVersion}: {version.AssemblyPath}");
            Assert.True(File.Exists(version.AssemblyPath), $"Assembly for Revit {version.RevitVersion} was not extracted.");
        }

        Assert.Equal(package.Versions.Count, installed.Versions.Count);
    }
}
