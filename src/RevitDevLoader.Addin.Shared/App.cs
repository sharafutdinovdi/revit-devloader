using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using RevitDevLoader.Core;
using RevitDevLoader.Infrastructure;

namespace RevitDevLoader;

public sealed class App : IExternalApplication
{
    private static bool _globalLoggingRegistered;

    public Result OnStartup(UIControlledApplication application)
    {
        var revitVersion = application.ControlledApplication.VersionNumber;
        var logger = FileLogger.Create(revitVersion).ForSource<App>();
        RegisterGlobalExceptionLogging(logger);
        logger.Info($"Application startup started. PluginVersion='{typeof(App).Assembly.GetName().Version}'. RevitVersion='{revitVersion}'. Settings='{DevUpdateLocations.GetSettingsPath()}'. Logs='{logger.Path}'.");

        try
        {
            RibbonRuntimeService.EnsureStartupButtons(application, logger);
            logger.Info("Application startup completed.");
            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            logger.Error("Application startup failed.", exception);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        var logger = FileLogger.Create(application.ControlledApplication.VersionNumber).ForSource<App>();
        logger.Info($"Application shutdown. PluginVersion='{typeof(App).Assembly.GetName().Version}'.");
        FileLogger.Shutdown();
        return Result.Succeeded;
    }

    private static void RegisterGlobalExceptionLogging(FileLogger logger)
    {
        if (_globalLoggingRegistered)
            return;

        _globalLoggingRegistered = true;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                logger.Error("Unhandled AppDomain exception.", exception);
            else
                logger.Info($"Unhandled AppDomain exception object: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }
}
