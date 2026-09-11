using System.IO;

namespace RevitDevLoader.Infrastructure;

public static class DevLoaderPaths
{
    public static string ProductFolder => FileLogger.LogsRoot;

    public static string LogsRoot => FileLogger.LogsRoot;

    public static string LogsFolder
    {
        get
        {
            var folder = Path.Combine(LogsRoot, "Logs");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
