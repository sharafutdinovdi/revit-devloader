using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace RevitDevLoader.Infrastructure;

public sealed class FileLogger
{
    public const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";
    public const long FileSizeLimitBytes = 10 * 1024 * 1024;
    public const int RetainedFileCountLimit = 14;

    private static readonly object SyncRoot = new();
    private static Serilog.ILogger? _rootLogger;
    private static SerilogLoggerFactory? _loggerFactory;
    private static string? _path;
    private readonly Serilog.ILogger _logger;

    private FileLogger(string path, Serilog.ILogger logger)
    {
        Path = path;
        _logger = logger;
    }

    public string Path { get; }

    public static string LogsRoot => ResolveLogsRoot(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static FileLogger Create(string? revitVersion = null, string? logsDirectory = null)
    {
        lock (SyncRoot)
        {
            EnsureConfigured(revitVersion, logsDirectory);
            return new FileLogger(_path!, _rootLogger!.ForContext("SourceContext", "RevitDevLoader"));
        }
    }

    public FileLogger ForSource<T>() => new(Path, _logger.ForContext<T>());

    public ILogger<T> CreateLogger<T>()
    {
        lock (SyncRoot)
        {
            if (_loggerFactory is null)
                throw new InvalidOperationException("Logging is not configured.");
            return _loggerFactory.CreateLogger<T>();
        }
    }

    public void Debug(string message) => _logger.Debug("{LogMessage}", message);
    public void Info(string message) => _logger.Information("{LogMessage}", message);
    public void Warn(string message) => _logger.Warning("{LogMessage}", message);
    public void Error(string message, Exception exception) => _logger.Error(exception, "{LogMessage}", message);

    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            _loggerFactory?.Dispose();
            _loggerFactory = null;
            (_rootLogger as IDisposable)?.Dispose();
            _rootLogger = null;
            _path = null;
            Log.Logger = Serilog.Core.Logger.None;
        }
    }

    private static void EnsureConfigured(string? revitVersion, string? logsDirectory)
    {
        if (_rootLogger is not null)
            return;

        var folder = ResolveLogFolder(logsDirectory);
        var rollingPath = System.IO.Path.Combine(folder, "DevLoader-.log");
        _path = System.IO.Path.Combine(folder, $"DevLoader-{DateTime.Now:yyyyMMdd}.log");
        _rootLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("Plugin", "RevitDevLoader")
            .Enrich.WithProperty("RevitVersion", revitVersion ?? "unknown")
            .WriteTo.File(
                rollingPath,
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: RetainedFileCountLimit,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
        Log.Logger = _rootLogger;
        _loggerFactory = new SerilogLoggerFactory(_rootLogger, dispose: false);
    }

    private static string ResolveLogFolder(string? logsDirectory)
    {
        try
        {
            var folder = logsDirectory ?? System.IO.Path.Combine(LogsRoot, "Logs");
            Directory.CreateDirectory(folder);
            return folder;
        }
        catch
        {
            var fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RevitDevLoader", "Logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    internal static string ResolveLogsRoot(string localApplicationDataRoot)
    {
        var root = System.IO.Path.Combine(localApplicationDataRoot, "RevitDevLoader");
        Directory.CreateDirectory(root);
        return root;
    }
}
