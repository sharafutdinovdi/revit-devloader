using RevitDevLoader.Core;

namespace RevitDevLoader.Views;

public sealed partial class PluginManagerWindow
{
    private static string BuildInstalledText(DevPluginStatus status)
    {
        return status.Installed is null
            ? "Installed: none"
            : $"Installed: {DevReleaseDisplayFormatter.Format(status.Installed.ReleaseId, status.Installed.AssemblyVersion)}";
    }

    private static string BuildAvailableText(DevPluginStatus status)
    {
        return status.Available is null
            ? "Available: none"
            : $"Available: {DevReleaseDisplayFormatter.Format(status.Available.ReleaseId, status.Available.AssemblyVersion)}";
    }

}
