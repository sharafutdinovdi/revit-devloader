using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevPayloadPackageTests
{
    [Fact]
    public void DefaultUpdatesFolderUsesLocalApplicationDataRoot()
    {
        var path = DevUpdateLocations.GetDefaultUpdatesFolder(@"C:\Users\tester\AppData\Local");

        Assert.Equal(Path.Combine(@"C:\Users\tester\AppData\Local", "RevitDevLoader", "updates"), path);
        Assert.DoesNotContain(path, character => character > 127);
    }

    [Fact]
    public void DefaultTestFeedPathUsesLocalApplicationDataRoot()
    {
        var path = DevUpdateLocations.GetDefaultTestFeedManifestPath(@"C:\Users\tester\AppData\Local");

        Assert.Equal(Path.Combine(@"C:\Users\tester\AppData\Local", "RevitDevLoader", "test-feed", "feed.json"), path);
        Assert.DoesNotContain(path, character => character > 127);
    }

    [Fact]
    public void FindLatestPackagePrefersNewestCreatedUtc()
    {
        var updatesFolder = CreateTempFolder();
        var older = Path.Combine(updatesFolder, "ExampleCommand-DevPayload-20260604-010000-old.zip");
        var newer = Path.Combine(updatesFolder, "ExampleCommand-DevPayload-20260604-020000-new.zip");
        CreatePayloadZip(older, "old", new DateTime(2026, 6, 4, 1, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);
        CreatePayloadZip(newer, "new", new DateTime(2026, 6, 4, 2, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);

        var latest = new DevPayloadPackageDiscovery().FindLatestPackage(updatesFolder);

        Assert.NotNull(latest);
        Assert.Equal("new", latest.ReleaseId);
        Assert.Equal(newer, latest.PackagePath);
    }

    [Fact]
    public void FindLatestPackageAcceptsTesterDropPrefixedPayloadName()
    {
        var updatesFolder = CreateTempFolder();
        var packagePath = Path.Combine(updatesFolder, "02-ExampleCommand-DevPayload-20260604-101005-cce731c5.zip");
        CreatePayloadZip(packagePath, "tester-drop", new DateTime(2026, 6, 4, 10, 10, 5, DateTimeKind.Utc), include2024: true, include2026: true);

        var latest = new DevPayloadPackageDiscovery().FindLatestPackage(updatesFolder);

        Assert.NotNull(latest);
        Assert.Equal("tester-drop", latest.ReleaseId);
        Assert.Equal(packagePath, latest.PackagePath);
    }

    [Fact]
    public void FindLatestPackageAcceptsStableLatestPayloadName()
    {
        var updatesFolder = CreateTempFolder();
        var packagePath = Path.Combine(updatesFolder, "ExampleCommand-DevPayload-latest.zip");
        CreatePayloadZip(packagePath, "stable-latest", new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);

        var latest = new DevPayloadPackageDiscovery().FindLatestPackage(updatesFolder);

        Assert.NotNull(latest);
        Assert.Equal("stable-latest", latest.ReleaseId);
        Assert.Equal(packagePath, latest.PackagePath);
    }

    [Fact]
    public void FindLatestPackagesKeepsLatestPackagePerPlugin()
    {
        var updatesFolder = CreateTempFolder();
        var exampleCommand = Path.Combine(updatesFolder, "ExampleCommand-DevPayload-latest.zip");
        var sampleCommandOld = Path.Combine(updatesFolder, "SampleCommand-DevPayload-old.zip");
        var sampleCommandNew = Path.Combine(updatesFolder, "SampleCommand-DevPayload-latest.zip");
        CreatePayloadZip(exampleCommand, "model-release", new DateTime(2026, 6, 4, 10, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);
        CreatePayloadZip(sampleCommandOld, "sample-old", new DateTime(2026, 6, 4, 11, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true, pluginId: "SampleCommand");
        CreatePayloadZip(sampleCommandNew, "sample-new", new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true, pluginId: "SampleCommand");

        var packages = new DevPayloadPackageDiscovery().FindLatestPackages(updatesFolder);

        Assert.Equal(2, packages.Count);
        Assert.Contains(packages, item => item.PluginId == "ExampleCommand" && item.ReleaseId == "model-release");
        Assert.Contains(packages, item => item.PluginId == "SampleCommand" && item.ReleaseId == "sample-new");
    }

    [Fact]
    public void FindPackagesKeepsArchiveVersions()
    {
        var updatesFolder = CreateTempFolder();
        CreatePayloadZip(Path.Combine(updatesFolder, "ExampleCommand-DevPayload-old.zip"), "old", new DateTime(2026, 6, 4, 10, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);
        CreatePayloadZip(Path.Combine(updatesFolder, "ExampleCommand-DevPayload-new.zip"), "new", new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);

        var packages = new DevPayloadPackageDiscovery().FindPackages(updatesFolder);

        Assert.Equal(2, packages.Count);
        Assert.Equal("new", packages[0].ReleaseId);
        Assert.Equal("old", packages[1].ReleaseId);
    }

    [Fact]
    public void PayloadWithoutPluginTypeDefaultsToCommand()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "LegacyPlugin-DevPayload.zip");
        CreatePayloadZip(packagePath, "legacy", DateTime.UtcNow, false, true, pluginId: "LegacyPlugin");

        var package = new DevPayloadPackageDiscovery().ReadPackageInfo(packagePath);

        Assert.Equal(DevPluginType.Command, package.PluginType);
        Assert.Equal("LegacyPlugin.Commands.OpenLegacyPluginCommand", package.CommandType);
    }

    [Fact]
    public void InstallPayloadCopiesVersionsAndWritesLocalManifest()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-a.zip");
        var localAppDataRoot = CreateTempFolder();
        CreatePayloadZip(packagePath, "release-a", new DateTime(2026, 6, 4, 3, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);

        var result = new DevPayloadInstaller().Install(packagePath, localAppDataRoot, new[] { "2024", "2026" });
        var manifest = new DevPluginRegistry(localAppDataRoot).Load("ExampleCommand");

        Assert.Equal("release-a", result.ReleaseId);
        Assert.Equal("ExampleCommand", result.PluginId);
        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2024", "ExampleCommand.dll")));
        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2026", "ExampleCommand.dll")));
        Assert.Equal(Path.Combine(result.RunRoot, "2024", "ExampleCommand.dll"), manifest.GetAssemblyPath("2024"));
        Assert.Equal(Path.Combine(result.RunRoot, "2026", "ExampleCommand.dll"), manifest.GetAssemblyPath("2026"));
        Assert.Equal("release-a", manifest.ReleaseId);
        Assert.Equal("1.0.release-a", manifest.AssemblyVersion);
        Assert.Equal(packagePath, manifest.PackagePath);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.CommandSlot);
        Assert.Equal(result.CommandSlot, manifest.CommandSlot);
    }

    [Fact]
    public void InstallPayloadWritesOnlyRequiredVersionEntries()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-current.zip");
        var localAppDataRoot = CreateTempFolder();
        CreatePayloadZip(packagePath, "release-current", new DateTime(2026, 6, 4, 3, 30, 0, DateTimeKind.Utc), include2024: false, include2026: true);

        var result = new DevPayloadInstaller().Install(packagePath, localAppDataRoot, new[] { "2026" });
        var manifest = new DevPluginRegistry(localAppDataRoot).Load("ExampleCommand");

        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2026", "ExampleCommand.dll")));
        Assert.Null(manifest.GetAssemblyPath("2024"));
        Assert.Equal(Path.Combine(result.RunRoot, "2026", "ExampleCommand.dll"), manifest.GetAssemblyPath("2026"));
        Assert.DoesNotContain(result.Versions, item => item.RevitVersion == "2024");
    }

    [Fact]
    public void InstallPayloadAcceptsWindowsSeparatorEntries()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-windows.zip");
        var localAppDataRoot = CreateTempFolder();
        CreatePayloadZip(
            packagePath,
            "release-windows",
            new DateTime(2026, 6, 4, 5, 0, 0, DateTimeKind.Utc),
            include2024: true,
            include2026: true,
            useWindowsSeparators: true);

        var result = new DevPayloadInstaller().Install(packagePath, localAppDataRoot, new[] { "2024", "2026" });

        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2024", "ExampleCommand.dll")));
        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2024", "ExampleCommand.Core.dll")));
        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2026", "ExampleCommand.dll")));
        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2026", "ExampleCommand.Core.dll")));
    }

    [Fact]
    public void InstallPayloadUsesNewRunFolderWhenSameReleaseIsAlreadyLoaded()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-repeat.zip");
        var localAppDataRoot = CreateTempFolder();
        CreatePayloadZip(packagePath, "release-repeat", new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc), include2024: true, include2026: true);
        var installer = new DevPayloadInstaller();
        var first = installer.Install(packagePath, localAppDataRoot, new[] { "2024", "2026" });
        var loadedDllPath = Path.Combine(first.RunRoot, "2024", "ExampleCommand.dll");

        using var loadedDll = File.Open(loadedDllPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var second = installer.Install(packagePath, localAppDataRoot, new[] { "2024", "2026" });
        var manifest = new DevPluginRegistry(localAppDataRoot).Load("ExampleCommand");

        Assert.Equal("release-repeat", second.ReleaseId);
        Assert.NotEqual(first.RunRoot, second.RunRoot);
        Assert.True(File.Exists(Path.Combine(second.RunRoot, "2024", "ExampleCommand.dll")));
        Assert.Equal(Path.Combine(second.RunRoot, "2024", "ExampleCommand.dll"), manifest.GetAssemblyPath("2024"));
    }

    [Fact]
    public void InstallPayloadKeepsAdvancingRunFoldersWhenPreviousRunsRemainLoaded()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-repeat.zip");
        var localAppDataRoot = CreateTempFolder();
        CreatePayloadZip(packagePath, "release-repeat", new DateTime(2026, 6, 5, 10, 30, 0, DateTimeKind.Utc), include2024: true, include2026: true);
        var installer = new DevPayloadInstaller();
        var first = installer.Install(packagePath, localAppDataRoot, new[] { "2024", "2026" });

        using var firstLoadedDll = File.Open(Path.Combine(first.RunRoot, "2024", "ExampleCommand.dll"), FileMode.Open, FileAccess.Read, FileShare.None);
        var second = installer.Install(packagePath, localAppDataRoot, new[] { "2024", "2026" });
        using var secondLoadedDll = File.Open(Path.Combine(second.RunRoot, "2024", "ExampleCommand.dll"), FileMode.Open, FileAccess.Read, FileShare.None);
        var third = installer.Install(packagePath, localAppDataRoot, new[] { "2024", "2026" });
        var manifest = new DevPluginRegistry(localAppDataRoot).Load("ExampleCommand");

        Assert.EndsWith("release-repeat-r2", second.RunRoot, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("release-repeat-r3", third.RunRoot, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.Combine(third.RunRoot, "2024", "ExampleCommand.dll"), manifest.GetAssemblyPath("2024"));
        Assert.Equal(first.CommandSlot, second.CommandSlot);
        Assert.Equal(second.CommandSlot, third.CommandSlot);
    }

    [Fact]
    public void RemovingPluginReleasesItsCommandSlot()
    {
        var root = CreateTempFolder();
        var firstPackage = Path.Combine(CreateTempFolder(), "FirstPlugin.zip");
        var secondPackage = Path.Combine(CreateTempFolder(), "SecondPlugin.zip");
        CreatePayloadZip(firstPackage, "first", DateTime.UtcNow, false, true, pluginId: "FirstPlugin");
        CreatePayloadZip(secondPackage, "second", DateTime.UtcNow, false, true, pluginId: "SecondPlugin");
        var installer = new DevPayloadInstaller();
        var registry = new DevPluginRegistry(root);

        var first = installer.Install(firstPackage, root, new[] { "2026" });
        Assert.True(registry.Delete("FirstPlugin"));
        var second = installer.Install(secondPackage, root, new[] { "2026" });

        Assert.Equal(1, first.CommandSlot);
        Assert.Equal(first.CommandSlot, second.CommandSlot);
    }

    [Fact]
    public void InstallReturnsClearFailureWhenAllCommandSlotsAreOccupied()
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
                Path.Combine(root, pluginId, "runs", "release-a"),
                string.Empty,
                slot,
                DateTime.UtcNow,
                new[] { new DevPluginVersionEntry("2026", Path.Combine(root, pluginId + ".dll")) }));
        }

        var packagePath = Path.Combine(CreateTempFolder(), "OverflowPlugin.zip");
        CreatePayloadZip(packagePath, "overflow", DateTime.UtcNow, false, true, pluginId: "OverflowPlugin");

        var result = new DevPayloadInstaller().Install(packagePath, root, new[] { "2026" });

        Assert.False(result.IsSuccess);
        Assert.Contains("20/20", result.ErrorMessage);
        Assert.Contains("Remove", result.ErrorMessage);
        Assert.False(registry.Exists("OverflowPlugin"));
    }

    [Fact]
    public void InstallApplicationWritesAndDeletesOwnedAddinManifest()
    {
        var root = CreateTempFolder();
        var applicationDataRoot = CreateTempFolder();
        var packagePath = Path.Combine(CreateTempFolder(), "SampleApplication-DevPayload-release-a.zip");
        CreatePayloadZip(
            packagePath,
            "release-a",
            DateTime.UtcNow,
            false,
            true,
            pluginId: "SampleApplication",
            pluginType: "application",
            applicationClass: "SampleApplication.Application");

        var result = new DevPayloadInstaller().Install(
            packagePath,
            root,
            new[] { "2026" },
            applicationDataRoot: applicationDataRoot);
        var addinPath = DevPluginRegistry.GetApplicationManifestPath(applicationDataRoot, "2026", "SampleApplication");
        var addIn = XDocument.Load(addinPath).Descendants("AddIn").Single();
        var manifest = new DevPluginRegistry(root).Load("SampleApplication");

        Assert.True(result.IsSuccess);
        Assert.Equal(DevPluginType.Application, result.PluginType);
        Assert.Null(result.CommandSlot);
        Assert.Equal(DevPluginType.Application, manifest.PluginType);
        Assert.Null(manifest.CommandSlot);
        Assert.Equal("Application", addIn.Attribute("Type")?.Value);
        Assert.Equal("SampleApplication.Application", addIn.Element("FullClassName")?.Value);
        Assert.Equal(Path.Combine(result.RunRoot, "2026", "SampleApplication.dll"), addIn.Element("Assembly")?.Value);

        var removed = new DevPluginRegistry(root).Delete("SampleApplication", applicationDataRoot, out var warnings);

        Assert.True(removed);
        Assert.Empty(warnings);
        Assert.False(File.Exists(addinPath));
        Assert.False(new DevPluginRegistry(root).Exists("SampleApplication"));
        Assert.True(File.Exists(Path.Combine(result.RunRoot, "2026", "SampleApplication.dll")));
    }

    [Fact]
    public void DeleteApplicationPreservesForeignAddinManifest()
    {
        var root = CreateTempFolder();
        var applicationDataRoot = CreateTempFolder();
        var packagePath = Path.Combine(CreateTempFolder(), "SampleApplication-DevPayload-release-a.zip");
        CreatePayloadZip(
            packagePath,
            "release-a",
            DateTime.UtcNow,
            false,
            true,
            pluginId: "SampleApplication",
            pluginType: "application",
            applicationClass: "SampleApplication.Application");
        new DevPayloadInstaller().Install(
            packagePath,
            root,
            new[] { "2026" },
            applicationDataRoot: applicationDataRoot);
        var addinPath = DevPluginRegistry.GetApplicationManifestPath(applicationDataRoot, "2026", "SampleApplication");
        File.WriteAllText(
            addinPath,
            "<RevitAddIns><AddIn Type=\"Application\"><Assembly>Customer\\SampleApplication.dll</Assembly></AddIn></RevitAddIns>");

        var removed = new DevPluginRegistry(root).Delete("SampleApplication", applicationDataRoot, out var warnings);

        Assert.True(removed);
        Assert.Single(warnings);
        Assert.Contains("not deleted", warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(addinPath));
        Assert.False(new DevPluginRegistry(root).Exists("SampleApplication"));
    }

    [Fact]
    public void InstallApplicationDoesNotAllocateCommandSlot()
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
                Path.Combine(root, pluginId, "runs", "release-a"),
                string.Empty,
                slot,
                DateTime.UtcNow,
                new[] { new DevPluginVersionEntry("2026", Path.Combine(root, pluginId + ".dll")) }));
        }

        var packagePath = Path.Combine(CreateTempFolder(), "SampleApplication-DevPayload-release-a.zip");
        CreatePayloadZip(
            packagePath,
            "release-a",
            DateTime.UtcNow,
            false,
            true,
            pluginId: "SampleApplication",
            pluginType: "application",
            applicationClass: "SampleApplication.Application");

        var result = new DevPayloadInstaller().Install(
            packagePath,
            root,
            new[] { "2026" },
            applicationDataRoot: CreateTempFolder());

        Assert.True(result.IsSuccess);
        Assert.Null(result.CommandSlot);
        Assert.Null(registry.Load("SampleApplication").CommandSlot);
    }

    [Fact]
    public void ApplicationAddInIdStaysStableAcrossUpdates()
    {
        var root = CreateTempFolder();
        var applicationDataRoot = CreateTempFolder();
        var firstPackage = Path.Combine(CreateTempFolder(), "SampleApplication-DevPayload-release-a.zip");
        var secondPackage = Path.Combine(CreateTempFolder(), "SampleApplication-DevPayload-release-b.zip");
        CreatePayloadZip(firstPackage, "release-a", DateTime.UtcNow, false, true, "SampleApplication", pluginType: "application", applicationClass: "SampleApplication.Application");
        CreatePayloadZip(secondPackage, "release-b", DateTime.UtcNow.AddMinutes(1), false, true, "SampleApplication", pluginType: "application", applicationClass: "SampleApplication.Application");
        var installer = new DevPayloadInstaller();
        var addinPath = DevPluginRegistry.GetApplicationManifestPath(applicationDataRoot, "2026", "SampleApplication");

        var first = installer.Install(firstPackage, root, new[] { "2026" }, applicationDataRoot: applicationDataRoot);
        var firstId = XDocument.Load(addinPath).Descendants("AddInId").Single().Value;
        var second = installer.Install(secondPackage, root, new[] { "2026" }, applicationDataRoot: applicationDataRoot);
        var updatedAddIn = XDocument.Load(addinPath);

        Assert.Equal(firstId, updatedAddIn.Descendants("AddInId").Single().Value);
        Assert.Equal(Path.Combine(second.RunRoot, "2026", "SampleApplication.dll"), updatedAddIn.Descendants("Assembly").Single().Value);
        Assert.NotEqual(first.RunRoot, second.RunRoot);
        Assert.True(Directory.Exists(first.RunRoot));
    }

    [Fact]
    public void InstallPayloadAppliesConfiguredRunRetention()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-retention.zip");
        var localAppDataRoot = CreateTempFolder();
        CreatePayloadZip(packagePath, "release-retention", DateTime.UtcNow, include2024: true, include2026: true);
        var installer = new DevPayloadInstaller();

        installer.Install(packagePath, localAppDataRoot, new[] { "2026" }, runRetentionCount: 2);
        installer.Install(packagePath, localAppDataRoot, new[] { "2026" }, runRetentionCount: 2);
        var result = installer.Install(packagePath, localAppDataRoot, new[] { "2026" }, runRetentionCount: 2);
        var runsRoot = Path.Combine(localAppDataRoot, "RevitDevLoader", "plugins", "ExampleCommand", "runs");

        Assert.Equal(2, Directory.GetDirectories(runsRoot).Length);
        Assert.Equal(2, result.RetentionResult.RetainedCount);
        Assert.Single(result.RetentionResult.DeletedFolders);
        Assert.True(Directory.Exists(result.RunRoot));
    }

    [Fact]
    public void InstallPayloadRejectsMissingRequiredVersionAssembly()
    {
        var packagePath = Path.Combine(CreateTempFolder(), "ExampleCommand-DevPayload-release-b.zip");
        var localAppDataRoot = CreateTempFolder();
        CreatePayloadZip(packagePath, "release-b", new DateTime(2026, 6, 4, 4, 0, 0, DateTimeKind.Utc), include2024: true, include2026: false);

        var exception = Assert.Throws<DevManifestException>(
            () => new DevPayloadInstaller().Install(packagePath, localAppDataRoot, new[] { "2024", "2026" }));

        Assert.Contains("2026", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreatePayloadZip(
        string packagePath,
        string releaseId,
        DateTime createdUtc,
        bool include2024,
        bool include2026,
        string pluginId = "ExampleCommand",
        bool useWindowsSeparators = false,
        string? pluginType = null,
        string applicationClass = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        var releaseInfo = new List<string>
        {
            "schemaVersion=2",
            $"pluginId={pluginId}",
            $"pluginName={pluginId}",
            $"displayName={pluginId} Latest",
            $"releaseId={releaseId}",
            $"assemblyVersion=1.0.{releaseId}",
            $"createdUtc={createdUtc:O}",
            $"mainAssembly={pluginId}.dll",
            "versions=2024,2026"
        };
        if (!string.IsNullOrWhiteSpace(pluginType))
            releaseInfo.Add($"pluginType={pluginType}");
        if (string.Equals(pluginType, "application", StringComparison.OrdinalIgnoreCase))
            releaseInfo.Add($"applicationClass={applicationClass}");
        else
            releaseInfo.Add($"commandType={pluginId}.Commands.Open{pluginId}Command");
        AddText(archive, "release-info.properties", string.Join(Environment.NewLine, releaseInfo));

        if (include2024)
        {
            AddPayloadText(archive, "2024", $"{pluginId}.dll", "dll-2024", useWindowsSeparators);
            AddPayloadText(archive, "2024", $"{pluginId}.Core.dll", "core-2024", useWindowsSeparators);
        }

        if (include2026)
        {
            AddPayloadText(archive, "2026", $"{pluginId}.dll", "dll-2026", useWindowsSeparators);
            AddPayloadText(archive, "2026", $"{pluginId}.Core.dll", "core-2026", useWindowsSeparators);
        }
    }

    private static void AddPayloadText(ZipArchive archive, string version, string fileName, string text, bool useWindowsSeparators)
    {
        var separator = useWindowsSeparators ? "\\" : "/";
        AddText(archive, string.Join(separator, "payload", version, fileName), text);
    }

    private static void AddText(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
