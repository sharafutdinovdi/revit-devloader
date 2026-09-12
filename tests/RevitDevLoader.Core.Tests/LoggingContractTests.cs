using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RevitDevLoader.Infrastructure;
using Xunit.Abstractions;

namespace RevitDevLoader.Core.Tests;

public sealed class LoggingContractTests
{
    private readonly ITestOutputHelper _output;

    public LoggingContractTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void SerilogSink_WritesUnifiedFormatTypedSourceAndExceptionStack()
    {
        var logsDirectory = Path.Combine(Path.GetTempPath(), "RevitDevLoader.LoggingContractTests", Guid.NewGuid().ToString("N"));
        try
        {
            var logger = FileLogger.Create("2026", logsDirectory).ForSource<LoggingContractTests>();
            logger.Info("Formatter check. Processed=1. Skipped=0. Errors=0.");
            logger.CreateLogger<LoggingContractTests>().LogInformation("Typed logger check. Count={Count}.", 1);
            try
            {
                ThrowSampleException();
            }
            catch (InvalidOperationException exception)
            {
                logger.Error("Exception formatter check.", exception);
            }

            var path = logger.Path;
            FileLogger.Shutdown();
            var text = File.ReadAllText(path);
            var firstLine = File.ReadLines(path).First();
            _output.WriteLine(firstLine);
            Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[INF\] \[RevitDevLoader\.Core\.Tests\.LoggingContractTests\] Formatter check\. Processed=1\. Skipped=0\. Errors=0\.$"), firstLine);
            Assert.Contains("Typed logger check. Count=1.", text, StringComparison.Ordinal);
            Assert.Contains("System.InvalidOperationException: formatter-boom", text, StringComparison.Ordinal);
            Assert.Contains(nameof(ThrowSampleException), text, StringComparison.Ordinal);
        }
        finally
        {
            FileLogger.Shutdown();
            if (Directory.Exists(logsDirectory))
                Directory.Delete(logsDirectory, recursive: true);
        }
    }

    [Fact]
    public void SerilogSink_UsesBoundedDailyRollingContract()
    {
        Assert.Equal(10 * 1024 * 1024, FileLogger.FileSizeLimitBytes);
        Assert.Equal(14, FileLogger.RetainedFileCountLimit);
        Assert.Contains("{Timestamp:yyyy-MM-dd HH:mm:ss.fff}", FileLogger.OutputTemplate, StringComparison.Ordinal);
        Assert.Contains("[{Level:u3}]", FileLogger.OutputTemplate, StringComparison.Ordinal);
        Assert.Contains("[{SourceContext}]", FileLogger.OutputTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void LogsRootResolver_CreatesLoaderRoot()
    {
        var localApplicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "RevitDevLoader.LogsRootTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var logsRoot = FileLogger.ResolveLogsRoot(localApplicationDataRoot);

            Assert.Equal(Path.Combine(localApplicationDataRoot, "RevitDevLoader"), logsRoot);
            Assert.True(Directory.Exists(logsRoot));
        }
        finally
        {
            if (Directory.Exists(localApplicationDataRoot))
                Directory.Delete(localApplicationDataRoot, recursive: true);
        }
    }

    private static void ThrowSampleException() => throw new InvalidOperationException("formatter-boom");
}
