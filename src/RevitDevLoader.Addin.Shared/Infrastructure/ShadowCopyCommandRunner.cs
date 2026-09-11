using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitDevLoader.Core;

namespace RevitDevLoader.Infrastructure;

public sealed class ShadowCopyCommandRunner
{
    private readonly FileLogger _logger;

    public ShadowCopyCommandRunner(FileLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Result Run(string assemblyPath, string commandTypeName, ExternalCommandData commandData, ref string message)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        if (string.IsNullOrWhiteSpace(commandTypeName))
            throw new ArgumentException("Command type name is required.", nameof(commandTypeName));
        if (commandData is null)
            throw new ArgumentNullException(nameof(commandData));

        var pluginDirectory = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            throw new InvalidOperationException($"Cannot resolve plugin directory: {assemblyPath}");

        var loader = new PluginAssemblyPathLoader(pluginDirectory, _logger.Info, _logger.Error);
        ResolveEventHandler handler = (_, args) => loader.Resolve(args);
        AppDomain.CurrentDomain.AssemblyResolve += handler;

        try
        {
            _logger.Info($"Loading plugin assembly from '{assemblyPath}'.");
            var assembly = loader.LoadMainAssembly(assemblyPath);
            var commandType = assembly.GetType(commandTypeName, throwOnError: true);
            if (commandType is null)
                throw new InvalidOperationException($"Command type not found: {commandTypeName}");

            var command = Activator.CreateInstance(commandType);
            if (command is null)
                throw new InvalidOperationException($"Cannot create command instance: {commandTypeName}");

            var execute = commandType.GetMethod(
                "Execute",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(ExternalCommandData), typeof(string).MakeByRefType(), typeof(ElementSet) },
                null);
            if (execute is null)
                throw new InvalidOperationException($"Command does not expose expected Execute signature: {commandTypeName}");

            object?[] arguments = { commandData, message, new ElementSet() };
            var rawResult = execute.Invoke(command, arguments);
            message = arguments[1] as string ?? string.Empty;

            if (rawResult is Result result)
                return result;

            if (rawResult is not null && Enum.TryParse(rawResult.ToString(), out Result parsed))
                return parsed;

            throw new InvalidOperationException($"Command returned unexpected result: {rawResult}");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            _logger.Error("Loaded command threw an exception.", exception.InnerException);
            throw exception.InnerException;
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
        }
    }
}
