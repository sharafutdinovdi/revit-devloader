using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevRunRetentionServiceTests
{
    [Fact]
    public void CleanupKeepsConfiguredNumberOfFreshestFolders()
    {
        var runsRoot = CreateRunsRoot("run-1", "run-2", "run-3", "run-4");
        SetAge(runsRoot, "run-1", 1);
        SetAge(runsRoot, "run-2", 2);
        SetAge(runsRoot, "run-3", 3);
        SetAge(runsRoot, "run-4", 4);

        var result = new DevRunRetentionService().Cleanup(
            runsRoot,
            Path.Combine(runsRoot, "run-4"),
            retentionCount: 3);

        Assert.Equal(3, result.RetainedCount);
        Assert.Single(result.DeletedFolders);
        Assert.False(Directory.Exists(Path.Combine(runsRoot, "run-1")));
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "run-2")));
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "run-3")));
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "run-4")));
    }

    [Fact]
    public void CleanupAlwaysKeepsCurrentManifestFolder()
    {
        var runsRoot = CreateRunsRoot("current", "newer-1", "newer-2");
        SetAge(runsRoot, "current", 1);
        SetAge(runsRoot, "newer-1", 2);
        SetAge(runsRoot, "newer-2", 3);

        var result = new DevRunRetentionService().Cleanup(
            runsRoot,
            Path.Combine(runsRoot, "current"),
            retentionCount: 2);

        Assert.Equal(2, result.RetainedCount);
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "current")));
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "newer-2")));
        Assert.False(Directory.Exists(Path.Combine(runsRoot, "newer-1")));
    }

    [Fact]
    public void CleanupWithOneKeepsOnlyCurrentFolder()
    {
        var runsRoot = CreateRunsRoot("old", "current");
        SetAge(runsRoot, "old", 1);
        SetAge(runsRoot, "current", 2);

        var result = new DevRunRetentionService().Cleanup(
            runsRoot,
            Path.Combine(runsRoot, "current"),
            retentionCount: 1);

        Assert.Equal(1, result.RetainedCount);
        Assert.False(Directory.Exists(Path.Combine(runsRoot, "old")));
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "current")));
    }

    [Fact]
    public void CleanupBreaksEqualTimestampsByFolderName()
    {
        var runsRoot = CreateRunsRoot("alpha", "bravo", "current");
        SetAge(runsRoot, "current", 1);
        SetAge(runsRoot, "alpha", 2);
        SetAge(runsRoot, "bravo", 2);

        var result = new DevRunRetentionService().Cleanup(
            runsRoot,
            Path.Combine(runsRoot, "current"),
            retentionCount: 2);

        Assert.Equal(2, result.RetainedCount);
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "current")));
        Assert.True(Directory.Exists(Path.Combine(runsRoot, "alpha")));
        Assert.False(Directory.Exists(Path.Combine(runsRoot, "bravo")));
    }

    [Fact]
    public void CleanupSkipsLockedFolderAndCountsItAsRetained()
    {
        var runsRoot = CreateRunsRoot("locked", "current");
        var lockedPath = Path.Combine(runsRoot, "locked");
        var service = new DevRunRetentionService(path =>
        {
            if (string.Equals(path, lockedPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Folder is locked by Revit.");

            Directory.Delete(path, recursive: true);
        });

        var result = service.Cleanup(
            runsRoot,
            Path.Combine(runsRoot, "current"),
            retentionCount: 1);

        var skipped = Assert.Single(result.SkippedFolders);
        Assert.Equal(lockedPath, skipped.FolderPath);
        Assert.Contains("locked", skipped.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, result.RetainedCount);
        Assert.True(Directory.Exists(lockedPath));
    }

    private static string CreateRunsRoot(params string[] folderNames)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "runs");
        Directory.CreateDirectory(root);
        foreach (var folderName in folderNames)
            Directory.CreateDirectory(Path.Combine(root, folderName));

        return root;
    }

    private static void SetAge(string runsRoot, string folderName, int minutes)
    {
        Directory.SetLastWriteTimeUtc(
            Path.Combine(runsRoot, folderName),
            new DateTime(2026, 1, 1, 0, minutes, 0, DateTimeKind.Utc));
    }
}
