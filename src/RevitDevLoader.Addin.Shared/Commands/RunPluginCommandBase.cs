using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitDevLoader.Core;
using RevitDevLoader.Infrastructure;

namespace RevitDevLoader.Commands;

[Transaction(TransactionMode.Manual)]
public abstract class RunPluginCommandBase : IExternalCommand
{
    protected abstract int CommandSlot { get; }

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var stopwatch = Stopwatch.StartNew();
        var revitVersion = GetRevitMajorVersion(commandData);
        var logger = FileLogger.Create(revitVersion).ForSource<RunPluginCommandBase>();
        var result = Result.Failed;
        var processedCount = 0;
        var skippedCount = 0;
        var errorCount = 0;
        var pluginId = $"slot {CommandSlot:00}";
        logger.Info($"Command started. PluginVersion='{typeof(RunPluginCommandBase).Assembly.GetName().Version}'. RevitVersion='{revitVersion}'. DocumentPath='{commandData.Application.ActiveUIDocument?.Document?.PathName ?? string.Empty}'. CommandSlot='{CommandSlot}'.");

        try
        {
            var registry = new DevPluginRegistry();
            var manifest = LoadManifest(registry, logger, ref message);
            if (manifest is null)
            {
                errorCount++;
                result = Result.Cancelled;
                return result;
            }

            pluginId = manifest.PluginName;
            logger.Info($"PluginId='{pluginId}'. RevitVersion='{revitVersion}'. Manifest='{registry.GetManifestPath(pluginId)}'. CommandSlot='{CommandSlot}'.");

            var assemblyPath = manifest.GetAssemblyPath(revitVersion) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                skippedCount++;
                result = Fail($"The manifest has no assembly for {pluginId} for Revit {revitVersion}.", pluginId, logger, ref message);
                return result;
            }

            if (!File.Exists(assemblyPath))
            {
                errorCount++;
                result = Fail($"Assembly file {pluginId} was not found:\n{assemblyPath}", pluginId, logger, ref message);
                return result;
            }

            var commandType = manifest.Commands.Single(command => command.Slot == CommandSlot).Class;
            logger.Info($"Loading CommandType='{commandType}'. Assembly='{assemblyPath}'.");
            result = new ShadowCopyCommandRunner(logger).Run(assemblyPath, commandType, commandData, ref message);
            processedCount = 1;
            logger.Info($"Run plugin command completed. PluginId='{pluginId}'. Result='{result}'. Message='{message}'.");
            return result;
        }
        catch (Exception exception)
        {
            logger.Error($"Run plugin command failed. PluginId='{pluginId}'. CommandSlot='{CommandSlot}'.", exception);
            message = $"DevLoader failed. Log: {logger.Path}";
            TaskDialog.Show("DevLoaderLoader", $"DevLoader could not run {pluginId}.\n\n{exception.Message}\n\nLog:\n{logger.Path}");
            errorCount++;
            result = Result.Cancelled;
            return result;
        }
        finally
        {
            stopwatch.Stop();
            logger.Info($"Command finished. Result='{result}'. DurationMs={stopwatch.ElapsedMilliseconds}. Processed={processedCount}. Skipped={skippedCount}. Errors={errorCount}.");
        }
    }

    private DevPluginManifest? LoadManifest(DevPluginRegistry registry, FileLogger logger, ref string message)
    {
        if (registry.TryLoadByCommandSlot(CommandSlot, out var manifest, out var error))
            return manifest;

        logger.Warn($"Cannot resolve command slot. CommandSlot='{CommandSlot}'. Error='{error}'.");
        message = $"DevLoader slot is not assigned. Log: {logger.Path}";
        TaskDialog.Show(
            "DevLoaderLoader",
            $"{error}\n\nLog:\n{logger.Path}");
        return null;
    }

    private Result Fail(string content, string pluginId, FileLogger logger, ref string message)
    {
        logger.Info(content.Replace(Environment.NewLine, " "));
        message = $"{pluginId} cannot run. Log: {logger.Path}";
        TaskDialog.Show(
            "DevLoaderLoader",
            $"{content}\n\nReinstall the package through the DevLoader manager.\n\nLog:\n{logger.Path}");
        return Result.Cancelled;
    }

    private static string GetRevitMajorVersion(ExternalCommandData commandData)
    {
        var version = commandData?.Application?.Application?.VersionNumber ?? string.Empty;
        return version.Length >= 4 ? version.Substring(0, 4) : string.Empty;
    }
}
