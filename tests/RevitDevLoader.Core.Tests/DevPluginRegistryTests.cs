using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevPluginRegistryTests
{
    [Fact]
    public void RegistryBuildsManifestPathUnderLocalAppDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var registry = new DevPluginRegistry(root);

        var path = registry.GetManifestPath("SampleCommand");

        Assert.Equal(
            Path.Combine(root, "RevitDevLoader", "plugins", "SampleCommand.devmanifest"),
            path);
    }

    [Fact]
    public void SaveAndLoadManifestRoundTripsThroughRegistry()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var registry = new DevPluginRegistry(root);
        var manifest = new DevPluginManifest(
            "SampleCommand",
            "SampleCommand Latest",
            "SampleCommand.Commands.OpenSampleCommandCommand",
            new DateTime(2026, 6, 4, 10, 0, 0, DateTimeKind.Utc),
            new[] { new DevPluginVersionEntry("2024", @"C:\dev\runs\003\2024\SampleCommand.dll") });

        registry.Save(manifest);
        var loaded = registry.Load("SampleCommand");

        Assert.Equal(manifest.PluginName, loaded.PluginName);
        Assert.Equal(@"C:\dev\runs\003\2024\SampleCommand.dll", loaded.GetAssemblyPath("2024"));
    }

    [Fact]
    public void SaveReplacesExistingManifestWithoutLeavingTemporaryFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var registry = new DevPluginRegistry(root);
        registry.Save(CreateManifest("release-a"));

        registry.Save(CreateManifest("release-b"));

        Assert.Equal("release-b", registry.Load("SampleCommand").ReleaseId);
        var manifestDirectory = Path.GetDirectoryName(registry.GetManifestPath("SampleCommand"))!;
        Assert.Empty(Directory.GetFiles(manifestDirectory, "*.tmp"));
    }

    [Fact]
    public void InterruptedTemporaryWriteKeepsPreviousManifestIntact()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var registry = new DevPluginRegistry(root);
        var previous = CreateManifest("release-a");
        registry.Save(previous);
        var manifestPath = registry.GetManifestPath("SampleCommand");
        var previousContent = File.ReadAllText(manifestPath);
        var interruptedRegistry = new DevPluginRegistry(root, (path, content) =>
        {
            File.WriteAllText(path, content.Substring(0, 12));
            throw new IOException("Simulated interrupted write.");
        });

        Assert.Throws<IOException>(() => interruptedRegistry.Save(CreateManifest("release-b")));

        var loaded = registry.Load("SampleCommand");
        Assert.Equal("release-a", loaded.ReleaseId);
        Assert.Equal(previousContent, File.ReadAllText(manifestPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(manifestPath)!, "*.tmp"));
    }

    [Fact]
    public void LoadThrowsClearExceptionWhenManifestIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var registry = new DevPluginRegistry(root);

        var exception = Assert.Throws<DevManifestException>(() => registry.Load("SampleCommand"));

        Assert.Contains("SampleCommand.devmanifest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeleteRemovesManifestWithoutTouchingRunFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var runRoot = Path.Combine(root, "runs", "release-a");
        Directory.CreateDirectory(Path.Combine(runRoot, "2026"));
        var assemblyPath = Path.Combine(runRoot, "2026", "SampleCommand.dll");
        File.WriteAllText(assemblyPath, "loaded");
        var registry = new DevPluginRegistry(root);
        var manifest = new DevPluginManifest(
            "SampleCommand",
            "SampleCommand Latest",
            "SampleCommand.Commands.OpenSampleCommandCommand",
            "release-a",
            "1.0.release-a",
            runRoot,
            @"C:\updates\SampleCommand.zip",
            new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc),
            new[] { new DevPluginVersionEntry("2026", assemblyPath) });
        registry.Save(manifest);

        Assert.True(registry.Exists("SampleCommand"));
        Assert.True(registry.Delete("SampleCommand"));

        Assert.False(registry.Exists("SampleCommand"));
        Assert.True(File.Exists(assemblyPath));
        Assert.False(registry.Delete("SampleCommand"));
    }

    [Fact]
    public void ExistingManifestsReceiveSlotsOnFirstStartup()
    {
        var registry = new DevPluginRegistry(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        registry.Save(CreateManifest("agr-release", "SampleTool"));
        registry.Save(CreateManifest("warning-release", "ExampleTool"));

        var errors = registry.EnsureCommandSlots();

        Assert.Empty(errors);
        Assert.Equal(1, registry.Load("SampleTool").CommandSlot);
        Assert.Equal(2, registry.Load("ExampleTool").CommandSlot);
    }

    [Fact]
    public void LegacyMigrationPreservesAlreadyAssignedSlots()
    {
        var registry = new DevPluginRegistry(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        registry.Save(CreateManifest("legacy", "SampleTool"));
        registry.Save(CreateManifest("assigned", "ExampleTool", 1));

        var errors = registry.EnsureCommandSlots();

        Assert.Empty(errors);
        Assert.Equal(2, registry.Load("SampleTool").CommandSlot);
        Assert.Equal(1, registry.Load("ExampleTool").CommandSlot);
    }

    [Fact]
    public void RepeatedAssignmentReusesExistingSlot()
    {
        var registry = new DevPluginRegistry(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        registry.Save(CreateManifest("release-a", "ExternalChecker", 4));

        Assert.True(registry.TryGetCommandSlot("ExternalChecker", out var first, out var firstError));
        Assert.True(registry.TryGetCommandSlot("ExternalChecker", out var second, out var secondError));

        Assert.Equal(4, first);
        Assert.Equal(first, second);
        Assert.Empty(firstError);
        Assert.Empty(secondError);
    }

    [Fact]
    public void SlotLookupResolvesAssignedManifest()
    {
        var registry = new DevPluginRegistry(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        registry.Save(CreateManifest("release-a", "ExternalChecker", 9));

        var found = registry.TryLoadByCommandSlot(9, out var manifest, out var error);

        Assert.True(found);
        Assert.NotNull(manifest);
        Assert.Equal("ExternalChecker", manifest.PluginName);
        Assert.Empty(error);
    }

    private static DevPluginManifest CreateManifest(
        string releaseId,
        string pluginId = "SampleCommand",
        int? commandSlot = null)
    {
        var runRoot = Path.Combine(Path.GetTempPath(), "runs", releaseId);
        return new DevPluginManifest(
            pluginId,
            pluginId + " Latest",
            pluginId + ".Commands.OpenCommand",
            releaseId,
            $"1.0.{releaseId}",
            runRoot,
            $@"C:\updates\{pluginId}-{releaseId}.zip",
            commandSlot,
            new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc),
            new[] { new DevPluginVersionEntry("2026", Path.Combine(runRoot, "2026", pluginId + ".dll")) });
    }
}
