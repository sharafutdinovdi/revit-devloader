namespace RevitDevLoader.Core.Tests;

public sealed class DevLoaderReleaseManifestTests
{
    [ReleaseManifestFact]
    public void EveryReleaseYearContainsAddinManifest()
    {
        var repoRoot = FindRepoRoot();
        var releaseRoot = Path.Combine(repoRoot, "build", "release");

        var releaseYears = Directory.EnumerateDirectories(releaseRoot)
            .Where(path => IsRevitYear(Path.GetFileName(path)))
            .Where(path => Directory.GetFiles(path, "RevitDevLoader.Addin.*.dll").Length > 0)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var releaseYear in releaseYears)
        {
            var manifestPath = Path.Combine(releaseYear, "RevitDevLoader.addin");
            Assert.True(
                File.Exists(manifestPath),
                $"Missing manifest for Revit {Path.GetFileName(releaseYear)}: {manifestPath}");
        }
    }

    internal static string? GetSkipReason()
    {
        if (!OperatingSystem.IsWindows())
            return "The release layout is built on Windows only.";

        var releaseRoot = Path.Combine(FindRepoRoot(), "build", "release");
        if (!Directory.Exists(releaseRoot))
            return "Release directory is missing. Run build-all.ps1 first.";

        return null;
    }

    internal static bool IsRevitYear(string directoryName)
    {
        return directoryName.Length == 4 &&
            directoryName.StartsWith("20", StringComparison.Ordinal) &&
            directoryName.All(char.IsDigit);
    }

    internal static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RevitDevLoader.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}

internal sealed class ReleaseManifestFactAttribute : FactAttribute
{
    public ReleaseManifestFactAttribute()
    {
        Skip = DevLoaderReleaseManifestTests.GetSkipReason();
    }
}
