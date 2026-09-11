using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RevitDevLoader.Core;

public sealed class DevRunRetentionService
{
    private readonly Action<string> _deleteDirectory;
    private readonly ILogger<DevRunRetentionService> _logger;

    public DevRunRetentionService(ILogger<DevRunRetentionService>? logger = null)
        : this(logger ?? NullLogger<DevRunRetentionService>.Instance, path => Directory.Delete(path, recursive: true))
    {
    }

    internal DevRunRetentionService(Action<string> deleteDirectory)
        : this(NullLogger<DevRunRetentionService>.Instance, deleteDirectory)
    {
    }

    internal DevRunRetentionService(ILogger<DevRunRetentionService> logger, Action<string> deleteDirectory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deleteDirectory = deleteDirectory ?? throw new ArgumentNullException(nameof(deleteDirectory));
    }

    public DevRunRetentionResult Cleanup(string runsRoot, string currentRunRoot, int retentionCount)
    {
        _logger.LogInformation("Cleanup started. RunsRoot='{RunsRoot}'. CurrentRunRoot='{CurrentRunRoot}'. RetentionCount={RetentionCount}.", runsRoot, currentRunRoot, retentionCount);
        if (string.IsNullOrWhiteSpace(runsRoot))
            throw new ArgumentException("Runs root is required.", nameof(runsRoot));
        if (string.IsNullOrWhiteSpace(currentRunRoot))
            throw new ArgumentException("Current run root is required.", nameof(currentRunRoot));
        if (retentionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionCount), "Retention count must be at least 1.");

        if (!Directory.Exists(runsRoot))
        {
            _logger.LogInformation("Cleanup completed. Deleted=0. Skipped=0. Retained=0.");
            return DevRunRetentionResult.Empty;
        }

        var skipped = new List<DevRunRetentionSkip>();
        List<RunFolder> folders;
        try
        {
            folders = Directory.GetDirectories(runsRoot)
                .Select(path => ReadRunFolder(path, skipped))
                .Where(folder => folder is not null)
                .Cast<RunFolder>()
                .OrderByDescending(folder => folder.LastWriteTimeUtc)
                .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to enumerate run folders. RunsRoot='{RunsRoot}'.", runsRoot);
            skipped.Add(new DevRunRetentionSkip(runsRoot, exception.Message));
            return new DevRunRetentionResult(Array.Empty<string>(), skipped, CountExistingDirectories(runsRoot));
        }

        var currentPath = NormalizePath(currentRunRoot);
        var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = folders.FirstOrDefault(folder => string.Equals(folder.FullPath, currentPath, StringComparison.OrdinalIgnoreCase));
        if (current is not null)
            retained.Add(current.FullPath);

        foreach (var folder in folders)
        {
            if (retained.Count >= retentionCount)
                break;

            retained.Add(folder.FullPath);
        }

        var deleted = new List<string>();
        foreach (var folder in folders.Where(folder => !retained.Contains(folder.FullPath)).Reverse())
        {
            try
            {
                _deleteDirectory(folder.FullPath);
                deleted.Add(folder.FullPath);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to delete run folder. Folder='{Folder}'.", folder.FullPath);
                skipped.Add(new DevRunRetentionSkip(folder.FullPath, exception.Message));
            }
        }

        var retainedCount = folders.Count - deleted.Count;
        _logger.LogInformation("Cleanup completed. Deleted={DeletedCount}. Skipped={SkippedCount}. Retained={RetainedCount}.", deleted.Count, skipped.Count, retainedCount);
        return new DevRunRetentionResult(deleted, skipped, retainedCount);
    }

    private static RunFolder? ReadRunFolder(string path, ICollection<DevRunRetentionSkip> skipped)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return new RunFolder(NormalizePath(path), directory.Name, directory.LastWriteTimeUtc);
        }
        catch (Exception exception)
        {
            skipped.Add(new DevRunRetentionSkip(path, exception.Message));
            return null;
        }
    }

    private static int CountExistingDirectories(string runsRoot)
    {
        try
        {
            return Directory.GetDirectories(runsRoot).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class RunFolder
    {
        public RunFolder(string fullPath, string name, DateTime lastWriteTimeUtc)
        {
            FullPath = fullPath;
            Name = name;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public string FullPath { get; }

        public string Name { get; }

        public DateTime LastWriteTimeUtc { get; }
    }
}

public sealed class DevRunRetentionResult
{
    internal static readonly DevRunRetentionResult Empty = new(
        Array.Empty<string>(),
        Array.Empty<DevRunRetentionSkip>(),
        retainedCount: 0);

    public DevRunRetentionResult(
        IReadOnlyList<string> deletedFolders,
        IReadOnlyList<DevRunRetentionSkip> skippedFolders,
        int retainedCount)
    {
        DeletedFolders = deletedFolders ?? Array.Empty<string>();
        SkippedFolders = skippedFolders ?? Array.Empty<DevRunRetentionSkip>();
        RetainedCount = retainedCount;
    }

    public IReadOnlyList<string> DeletedFolders { get; }

    public IReadOnlyList<DevRunRetentionSkip> SkippedFolders { get; }

    public int RetainedCount { get; }
}

public sealed class DevRunRetentionSkip
{
    public DevRunRetentionSkip(string folderPath, string reason)
    {
        FolderPath = folderPath ?? string.Empty;
        Reason = reason ?? string.Empty;
    }

    public string FolderPath { get; }

    public string Reason { get; }
}
