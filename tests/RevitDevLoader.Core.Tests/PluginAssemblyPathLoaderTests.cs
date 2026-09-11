using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class PluginAssemblyPathLoaderTests
{
    [Fact]
    public void LoadMainAssemblyLoadsSameIdentityFromDifferentPathsAsDistinctAssemblies()
    {
        var sourceAssembly = typeof(DevPluginManifest).Assembly.Location;
        var firstPath = CopyToTempRunFolder(sourceAssembly);
        var secondPath = CopyToTempRunFolder(sourceAssembly);
        var firstLoader = new PluginAssemblyPathLoader(Path.GetDirectoryName(firstPath)!);
        var secondLoader = new PluginAssemblyPathLoader(Path.GetDirectoryName(secondPath)!);

        var firstAssembly = firstLoader.LoadMainAssembly(firstPath);
        var secondAssembly = secondLoader.LoadMainAssembly(secondPath);

        Assert.NotSame(firstAssembly, secondAssembly);
        Assert.Equal(firstAssembly.GetName().FullName, secondAssembly.GetName().FullName);
        Assert.Equal(firstPath, firstAssembly.Location);
        Assert.Equal(secondPath, secondAssembly.Location);
    }

    [Fact]
    public void LoadMainAssemblyReusesAssemblyWithinSameRun()
    {
        var assemblyPath = CopyToTempRunFolder(typeof(DevPluginManifest).Assembly.Location);
        var loader = new PluginAssemblyPathLoader(Path.GetDirectoryName(assemblyPath)!);

        var firstAssembly = loader.LoadMainAssembly(assemblyPath);
        var secondAssembly = loader.LoadMainAssembly(assemblyPath);

        Assert.Same(firstAssembly, secondAssembly);
    }

    [Fact]
    public void FindConventionalInstallsScansUserAndMachineDirectoriesByAssemblyName()
    {
        var root = CreateTempFolder();
        var userDirectory = Path.Combine(root, "user", "Autodesk", "Revit", "Addins", "2026");
        var machineDirectory = Path.Combine(root, "machine", "Autodesk", "Revit", "Addins", "2026");
        Directory.CreateDirectory(userDirectory);
        Directory.CreateDirectory(machineDirectory);
        WriteAddin(Path.Combine(userDirectory, "UserCopy.addin"), "Renamed plugin", @"C:\Plugins\SampleTool.dll");
        WriteAddin(Path.Combine(machineDirectory, "MachineCopy.addin"), "SampleTool", @"D:\Company\SAMPLETOOL.DLL");
        WriteAddin(Path.Combine(machineDirectory, "Different.addin"), "SampleTool", @"D:\Company\Different.dll");

        var directories = PluginAssemblyPathLoader.GetConventionalAddinsDirectories(
            "2026",
            Path.Combine(root, "user"),
            Path.Combine(root, "machine"));
        var matches = PluginAssemblyPathLoader.FindConventionalInstalls(directories, "SampleTool.dll");

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, match => match.AddinPath.EndsWith("UserCopy.addin", StringComparison.Ordinal));
        Assert.Contains(matches, match => match.AddinPath.EndsWith("MachineCopy.addin", StringComparison.Ordinal));
        Assert.DoesNotContain(matches, match => match.AddinPath.EndsWith("Different.addin", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadMainAssemblyWarnsAndContinuesWhenConventionalCopyExists()
    {
        var assemblyPath = CopyToTempRunFolder(typeof(DevPluginManifest).Assembly.Location);
        var addinsDirectory = CreateTempFolder();
        WriteAddin(
            Path.Combine(addinsDirectory, "Conventional.addin"),
            "DevLoader Core copy",
            @"C:\Program Files\RevitDevLoader.Core.dll");
        var messages = new List<string>();
        var loader = new PluginAssemblyPathLoader(
            Path.GetDirectoryName(assemblyPath)!,
            messages.Add,
            conventionalAddinsDirectories: new[] { addinsDirectory });

        var assembly = loader.LoadMainAssembly(assemblyPath);

        Assert.Equal(assemblyPath, assembly.Location);
        Assert.Contains(messages, message =>
            message.Contains("WARNING", StringComparison.Ordinal) &&
            message.Contains("will continue", StringComparison.Ordinal));
    }

    private static string CopyToTempRunFolder(string sourceAssembly)
    {
        var folder = CreateTempFolder();
        var destination = Path.Combine(folder, Path.GetFileName(sourceAssembly));
        File.Copy(sourceAssembly, destination);
        return destination;
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void WriteAddin(string path, string name, string assemblyPath)
    {
        File.WriteAllText(
            path,
            $"<RevitAddIns><AddIn Type=\"Command\"><Name>{name}</Name>" +
            $"<Assembly>{assemblyPath}</Assembly></AddIn></RevitAddIns>");
    }
}
