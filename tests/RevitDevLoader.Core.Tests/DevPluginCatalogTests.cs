using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevPluginCatalogTests
{
    [Theory]
    [InlineData("1.0.0", "1.1.0", DevPluginStatusKind.UpdateAvailable)]
    [InlineData("1.9.0", "1.10.0", DevPluginStatusKind.UpdateAvailable)]
    [InlineData("1.1.0", "1.1.0", DevPluginStatusKind.Latest)]
    [InlineData("1.1.0.0", "1.1.0", DevPluginStatusKind.Latest)]
    [InlineData("2.0.0", "1.1.0", DevPluginStatusKind.Latest)]
    public void StatusComparesReleaseVersionsWithoutOfferingDowngrades(string installedVersion, string feedVersion, DevPluginStatusKind expected)
    {
        var root = CreateTempFolder();
        var registry = new DevPluginRegistry(root);
        registry.Save(new DevPluginManifest("SampleCommand", "Sample command", "SampleCommand.Commands.OpenSampleCommandCommand",
            installedVersion, "1.0.0.0", root, string.Empty, 1, DateTime.UtcNow,
            new[] { new DevPluginVersionEntry("2026", Path.Combine(root, "SampleCommand.dll")) }));
        var package = CreatePackage(feedVersion, DateTime.UtcNow.AddDays(-1), "2026");
        var status = Assert.Single(new DevPluginStatusService(registry).BuildStatuses(
            Array.Empty<DevPluginCatalogItem>(), new[] { package }, "2026", CreateTempFolder()));
        Assert.Equal(expected, status.Kind);
        Assert.Equal(installedVersion == "2.0.0", status.HasNewerInstalledVersion);
    }

    [Fact]
    public void HighestCompatibleVersionWinsEvenWhenOlderPackageWasPublishedLater()
    {
        var latest = CreatePackage("1.10.0", DateTime.UtcNow.AddDays(-2), "2026");
        var republished = CreatePackage("1.9.0", DateTime.UtcNow, "2026");
        var incompatible = CreatePackage("2.0.0", DateTime.UtcNow, "2027");
        var status = Assert.Single(new DevPluginStatusService(new DevPluginRegistry(CreateTempFolder())).BuildStatuses(
            Array.Empty<DevPluginCatalogItem>(), new[] { republished, incompatible, latest }, "2026", CreateTempFolder()));
        Assert.Same(latest, status.Available);
    }

    [Fact]
    public void DefaultCatalogIsEmpty()
    {
        Assert.Empty(DevPluginCatalog.CreateDefault());
    }

    [Fact]
    public void StatusServiceReturnsEveryCatalogPluginWithoutPackages()
    {
        var catalog = CreateCatalog();
        var registry = new DevPluginRegistry(CreateTempFolder());

        var statuses = new DevPluginStatusService(registry).BuildStatuses(
            catalog,
            Array.Empty<DevPayloadPackageInfo>(),
            "2026");

        Assert.Equal(catalog.Count, statuses.Count);
        Assert.Contains(statuses, item => item.Plugin.PluginId == "SampleCommand" && item.Kind == DevPluginStatusKind.NotInstalled);
        Assert.Contains(statuses, item => item.Plugin.PluginId == "LegacyCommand" && item.Kind == DevPluginStatusKind.UnsupportedRevitVersion);
        Assert.All(catalog, plugin => Assert.Contains(statuses, status => status.Plugin.PluginId == plugin.PluginId));
    }

    [Fact]
    public void StatusServiceIncludesPluginThatIsNotHardCodedInCatalog()
    {
        var package = new DevPayloadPackageInfo(
            Path.Combine(CreateTempFolder(), "ExternalChecker.zip"),
            "ExternalChecker",
            "External repository command",
            "release-a",
            "1.0.0",
            "ExternalChecker.Commands.RunCommand",
            "ExternalChecker.dll",
            DateTime.UtcNow,
            new[] { "2026" },
            schemaVersion: 2);

        var statuses = new DevPluginStatusService(new DevPluginRegistry(CreateTempFolder())).BuildStatuses(
            DevPluginCatalog.CreateDefault(),
            new[] { package },
            "2026",
            CreateTempFolder());

        var status = Assert.Single(statuses, item => item.Plugin.PluginId == "ExternalChecker");
        Assert.Equal("External repository command", status.Plugin.DisplayName);
        Assert.Equal(DevPluginStatusKind.NotInstalled, status.Kind);
        Assert.True(status.CanInstallOrUpdate);
    }

    [Fact]
    public void StatusServiceExplainsWhenExternalPluginHasNoFreeSlot()
    {
        var root = CreateTempFolder();
        var registry = new DevPluginRegistry(root);
        for (var slot = 1; slot <= DevPluginRegistry.CommandSlotCount; slot++)
        {
            var pluginId = $"Plugin{slot:00}";
            registry.Save(new DevPluginManifest(
                pluginId,
                pluginId,
                pluginId + ".Command",
                "release-a",
                "1.0.0",
                root,
                string.Empty,
                slot,
                DateTime.UtcNow,
                new[] { new DevPluginVersionEntry("2026", Path.Combine(root, pluginId + ".dll")) }));
        }

        var package = new DevPayloadPackageInfo(
            Path.Combine(root, "OverflowPlugin.zip"),
            "OverflowPlugin",
            "Overflow plugin",
            "release-a",
            "1.0.0",
            "OverflowPlugin.Command",
            "OverflowPlugin.dll",
            DateTime.UtcNow,
            new[] { "2026" },
            schemaVersion: 2);

        var statuses = new DevPluginStatusService(registry).BuildStatuses(
            Array.Empty<DevPluginCatalogItem>(),
            new[] { package },
            "2026",
            CreateTempFolder());

        var status = Assert.Single(statuses, item => item.Plugin.PluginId == "OverflowPlugin");
        Assert.Equal(DevPluginStatusKind.CommandSlotUnavailable, status.Kind);
        Assert.Contains("20/20", status.Details);
        Assert.False(status.CanInstallOrUpdate);
    }

    [Fact]
    public void StatusServiceAllowsApplicationWhenAllCommandSlotsAreOccupied()
    {
        var root = CreateTempFolder();
        var registry = new DevPluginRegistry(root);
        for (var slot = 1; slot <= DevPluginRegistry.CommandSlotCount; slot++)
        {
            var pluginId = $"Plugin{slot:00}";
            registry.Save(new DevPluginManifest(
                pluginId,
                pluginId,
                pluginId + ".Command",
                "release-a",
                "1.0.0",
                root,
                string.Empty,
                slot,
                DateTime.UtcNow,
                new[] { new DevPluginVersionEntry("2026", Path.Combine(root, pluginId + ".dll")) }));
        }

        var package = new DevPayloadPackageInfo(
            Path.Combine(root, "SampleApplication.zip"),
            "SampleApplication",
            "SampleApplication",
            "release-a",
            "1.0.0",
            string.Empty,
            "SampleApplication.dll",
            DateTime.UtcNow,
            new[] { "2026" },
            schemaVersion: 2,
            pluginType: DevPluginType.Application,
            applicationClass: "SampleApplication.Application");

        var status = Assert.Single(new DevPluginStatusService(registry).BuildStatuses(
            Array.Empty<DevPluginCatalogItem>(),
            new[] { package },
            "2026",
            CreateTempFolder()), item => item.Plugin.PluginId == "SampleApplication");

        Assert.Equal(DevPluginStatusKind.NotInstalled, status.Kind);
        Assert.True(status.CanInstallOrUpdate);
    }

    [Theory]
    [InlineData("Example command", "Example\ncommand")]
    [InlineData("Very long name of an external command plugin", "Very long name\nof an externa…")]
    public void RibbonTextUsesDisplayNameAndKeepsLinesCompact(string displayName, string expected)
    {
        Assert.Equal(expected, DevPluginCatalogItem.FormatRibbonText(displayName));
    }

    [Fact]
    public void StatusServicePrefersLatestCompatiblePackageForRevitVersion()
    {
        var catalog = CreateCatalog().Where(item => item.PluginId == "SampleCommand").ToList();
        var registry = new DevPluginRegistry(CreateTempFolder());
        var newer2026Only = CreatePackage("new-2026", new DateTime(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc), "2026");
        var older2024 = CreatePackage("old-2024", new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc), "2024");

        var status = Assert.Single(new DevPluginStatusService(registry).BuildStatuses(
            catalog,
            new[] { newer2026Only, older2024 },
            "2024"));

        Assert.Equal(DevPluginStatusKind.NotInstalled, status.Kind);
        Assert.NotNull(status.Available);
        Assert.Equal("old-2024", status.Available.ReleaseId);
    }

    [Fact]
    public void StatusServiceReportsDamagedManifestWithoutThrowing()
    {
        var root = CreateTempFolder();
        var registry = new DevPluginRegistry(root);
        var manifestPath = registry.GetManifestPath("SampleCommand");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, "pluginName=SampleCommand\nbroken line");
        var catalog = CreateCatalog().Where(item => item.PluginId == "SampleCommand");

        var status = Assert.Single(new DevPluginStatusService(registry).BuildStatuses(
            catalog,
            Array.Empty<DevPayloadPackageInfo>(),
            "2026",
            CreateTempFolder()));

        Assert.Equal(DevPluginStatusKind.PackageError, status.Kind);
        Assert.Contains("corrupt", status.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(manifestPath, status.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatusServiceWarnsAboutConventionalAddinWithMatchingMainAssembly()
    {
        var addinsDirectory = CreateTempFolder();
        var addinPath = Path.Combine(addinsDirectory, "CustomerInstall.addin");
        File.WriteAllText(
            addinPath,
            "<RevitAddIns><AddIn Type=\"Application\"><Name>Customer Agr Plugin</Name>" +
            "<Assembly>SampleTool\\SampleTool.dll</Assembly></AddIn></RevitAddIns>");
        var catalog = CreateCatalog().Where(item => item.PluginId == "SampleTool");

        var status = Assert.Single(new DevPluginStatusService(new DevPluginRegistry(CreateTempFolder())).BuildStatuses(
            catalog,
            Array.Empty<DevPayloadPackageInfo>(),
            "2026",
            addinsDirectory));

        Assert.Contains("two copies", status.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SampleTool.dll", status.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(addinPath, status.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatusServiceIgnoresMatchingAddinNameWithDifferentAssembly()
    {
        var addinsDirectory = CreateTempFolder();
        File.WriteAllText(
            Path.Combine(addinsDirectory, "CustomerInstall.addin"),
            "<RevitAddIns><AddIn Type=\"Application\"><Name>SampleTool</Name>" +
            "<Assembly>OtherPlugin.dll</Assembly></AddIn></RevitAddIns>");
        var catalog = CreateCatalog().Where(item => item.PluginId == "SampleTool");

        var status = Assert.Single(new DevPluginStatusService(new DevPluginRegistry(CreateTempFolder())).BuildStatuses(
            catalog,
            Array.Empty<DevPayloadPackageInfo>(),
            "2026",
            addinsDirectory));

        Assert.DoesNotContain(
            DevPluginStatusService.ConventionalInstallWarningPrefix,
            status.Details,
            StringComparison.Ordinal);
    }

    private static string CreateTempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static IReadOnlyList<DevPluginCatalogItem> CreateCatalog() => new[]
    {
        new DevPluginCatalogItem("SampleCommand", "Sample command", "SampleCommand.Commands.OpenSampleCommandCommand", "SampleCommand.dll", new[] { "2022", "2023", "2024", "2025", "2026" }, "SC"),
        new DevPluginCatalogItem("LegacyCommand", "Legacy command", "LegacyCommand.Command", "LegacyCommand.dll", new[] { "2022", "2023", "2024" }, "LC"),
        new DevPluginCatalogItem("SampleTool", "Sample tool", "SampleTool.Commands.OpenSampleToolCommand", "SampleTool.dll", new[] { "2022", "2023", "2024", "2025", "2026" }, "ST")
    };

    private static DevPayloadPackageInfo CreatePackage(string releaseId, DateTime createdUtc, params string[] versions)
    {
        return new DevPayloadPackageInfo(
            Path.Combine(CreateTempFolder(), $"SampleCommand-DevPayload-{releaseId}.zip"),
            "SampleCommand",
            "SampleCommand",
            releaseId,
            $"1.0.{releaseId}",
            "SampleCommand.Commands.OpenSampleCommandCommand",
            "SampleCommand.dll",
            createdUtc,
            versions,
            schemaVersion: 2);
    }
}
