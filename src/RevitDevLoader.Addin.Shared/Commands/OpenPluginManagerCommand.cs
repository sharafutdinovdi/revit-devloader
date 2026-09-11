using System;
using System.Diagnostics;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitDevLoader.Infrastructure;
using RevitDevLoader.Views;

namespace RevitDevLoader.Commands;

[Transaction(TransactionMode.Manual)]
public sealed class OpenPluginManagerCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var stopwatch = Stopwatch.StartNew();
        var revitVersion = commandData.Application.Application.VersionNumber;
        var logger = FileLogger.Create(revitVersion).ForSource<OpenPluginManagerCommand>();
        var result = Result.Failed;
        var processedCount = 0;
        var errorCount = 0;
        logger.Info($"Command started. PluginVersion='{typeof(OpenPluginManagerCommand).Assembly.GetName().Version}'. RevitVersion='{revitVersion}'. DocumentPath='{commandData.Application.ActiveUIDocument?.Document?.PathName ?? string.Empty}'. SettingsPath='{RevitDevLoader.Core.DevUpdateLocations.GetSettingsPath()}'.");

        try
        {
            var window = new PluginManagerWindow(commandData, logger);
            new WindowInteropHelper(window) { Owner = commandData.Application.MainWindowHandle };
            window.ShowDialog();
            logger.Info("DevLoader manager command completed.");
            processedCount = 1;
            result = Result.Succeeded;
            return result;
        }
        catch (Exception exception)
        {
            logger.Error("DevLoader manager command failed.", exception);
            message = $"DevLoader failed. Log: {logger.Path}";
            TaskDialog.Show("DevLoaderLoader", $"Could not open the plugin manager.\n\n{exception.Message}\n\nLog:\n{logger.Path}");
            errorCount++;
            result = Result.Cancelled;
            return result;
        }
        finally
        {
            stopwatch.Stop();
            logger.Info($"Command finished. Result='{result}'. DurationMs={stopwatch.ElapsedMilliseconds}. Processed={processedCount}. Skipped=0. Errors={errorCount}.");
        }
    }
}
