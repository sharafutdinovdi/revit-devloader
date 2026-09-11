using System.Text.RegularExpressions;

namespace RevitDevLoader.Core.Tests;

public sealed class RunCatalogPluginCommandTests
{
    [Fact]
    public void CatalogRunCommands_HaveExplicitTransactionAttribute()
    {
        var source = File.ReadAllText(GetCommandSourcePath());
        var matches = Regex.Matches(source, @"(?ms)(?<attrs>(?:^\[[^\]]+\]\s*)*)^public sealed class (?<name>Run\w+Command)\s*:");

        Assert.NotEmpty(matches);
        foreach (Match match in matches)
        {
            var className = match.Groups["name"].Value;
            var attributes = match.Groups["attrs"].Value;
            Assert.Contains("[Transaction(TransactionMode.Manual)]", attributes);
            Assert.DoesNotContain("RunPluginCommandBase", className);
        }

        Assert.Equal(RevitDevLoader.Core.DevPluginRegistry.CommandSlotCount, matches.Count);
        Assert.Equal(
            Enumerable.Range(1, RevitDevLoader.Core.DevPluginRegistry.CommandSlotCount)
                .Select(slot => $"RunPluginSlot{slot:00}Command"),
            matches.Select(match => match.Groups["name"].Value));
    }

    private static string GetCommandSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RevitDevLoader.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(
            directory!.FullName,
            "src", "RevitDevLoader.Addin.Shared",
            "Commands",
            "RunCatalogPluginCommands.cs");
    }
}
