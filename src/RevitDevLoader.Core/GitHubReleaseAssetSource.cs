using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RevitDevLoader.Core;

internal readonly struct GitHubReleaseAssetReference
{
    private GitHubReleaseAssetReference(string repo, string tag, string assetName)
    {
        Repo = repo;
        Tag = tag;
        AssetName = assetName;
    }

    public string Repo { get; }

    public string Tag { get; }

    public string AssetName { get; }

    public static bool TryParse(string value, out GitHubReleaseAssetReference reference)
    {
        reference = default;
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "github-release", StringComparison.OrdinalIgnoreCase))
            return false;

        var owner = uri.Host.Trim();
        var segments = uri.AbsolutePath
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        if (string.IsNullOrWhiteSpace(owner) || segments.Length < 3)
            return false;

        var repo = $"{owner}/{segments[0]}";
        var tag = segments[1];
        var assetName = segments[segments.Length - 1];
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(assetName))
            return false;

        reference = new GitHubReleaseAssetReference(repo, tag, assetName);
        return true;
    }
}

internal static class GitHubReleaseAssetSource
{
    public static GitHubReleaseTextResult DownloadText(string source, string cacheRoot)
    {
        if (!GitHubReleaseAssetReference.TryParse(source, out var reference))
            throw new DevManifestException($"GitHub release source is invalid: {source}");

        var folder = Path.Combine(cacheRoot, "github-release", SafeSegment(reference.Repo), SafeSegment(reference.Tag));
        Directory.CreateDirectory(folder);
        var cachedPath = Path.Combine(folder, reference.AssetName);
        string path;
        try
        {
            path = Download(reference, folder);
        }
        catch (Exception) when (File.Exists(cachedPath))
        {
            path = cachedPath;
            return new GitHubReleaseTextResult(File.ReadAllText(path, Encoding.UTF8), usedCache: true);
        }

        return new GitHubReleaseTextResult(File.ReadAllText(path, Encoding.UTF8), usedCache: false);
    }

    public static string Download(GitHubReleaseAssetReference reference, string targetFolder)
    {
        if (string.IsNullOrWhiteSpace(targetFolder))
            throw new ArgumentException("Target folder is required.", nameof(targetFolder));

        Directory.CreateDirectory(targetFolder);
        var destination = Path.Combine(targetFolder, reference.AssetName);
        var tempFolder = Path.Combine(targetFolder, ".download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        var tempPath = Path.Combine(tempFolder, reference.AssetName);

        var arguments = new[]
        {
            "release",
            "download",
            reference.Tag,
            "--repo",
            reference.Repo,
            "--pattern",
            reference.AssetName,
            "--dir",
            tempFolder
        };

        try
        {
            var result = RunGh(arguments);
            if (result.ExitCode != 0)
                throw CreateDownloadException(reference, result.Error);

            if (!File.Exists(tempPath))
                throw new GitHubReleaseAssetException(
                    GitHubReleaseErrorKind.Other,
                    $"GitHub release asset was not downloaded for {reference.Repo}/{reference.Tag}/{reference.AssetName}. Check release tag '{reference.Tag}' and asset name '{reference.AssetName}'.");

            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(tempPath, destination);
            return destination;
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try
                {
                    Directory.Delete(tempFolder, recursive: true);
                }
                catch
                {
                    // Best effort cleanup must not hide the download result.
                }
            }
        }
    }

    private static ProcessResult RunGh(string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "gh",
            Arguments = string.Join(" ", arguments.Select(Quote)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw CreateCliNotFoundException(new InvalidOperationException("Process.Start returned null."));
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(120000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Best effort cleanup.
                }

                throw new GitHubReleaseAssetException(
                    GitHubReleaseErrorKind.Other,
                    "GitHub CLI 'gh' timed out while downloading a release asset. Check network connectivity, release tag, and asset name.");
            }

            return new ProcessResult(process.ExitCode, output, error);
        }
        catch (FileNotFoundException exception)
        {
            throw CreateCliNotFoundException(exception);
        }
        catch (Win32Exception exception)
        {
            throw CreateCliNotFoundException(exception);
        }
        catch (DevManifestException)
        {
            throw;
        }
        catch (GitHubReleaseAssetException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new GitHubReleaseAssetException(
                GitHubReleaseErrorKind.Other,
                "GitHub CLI 'gh' failed while downloading a release asset. Check network connectivity, release tag, and asset name.",
                exception);
        }
    }

    private static GitHubReleaseAssetException CreateDownloadException(
        GitHubReleaseAssetReference reference,
        string error)
    {
        var kind = GitHubReleaseErrorClassifier.Classify(error);
        var message = kind == GitHubReleaseErrorKind.AuthenticationOrAccess
            ? $"GitHub CLI cannot access '{reference.Repo}'. Run 'gh auth login' and verify repository access. {error}"
            : $"GitHub release asset download failed for {reference.Repo}/{reference.Tag}/{reference.AssetName}. Check network connectivity, release tag '{reference.Tag}', and asset name '{reference.AssetName}'. {error}";
        return new GitHubReleaseAssetException(kind, message);
    }

    private static GitHubReleaseAssetException CreateCliNotFoundException(Exception exception)
    {
        return new GitHubReleaseAssetException(
            GitHubReleaseErrorKind.CliNotFound,
            "GitHub CLI 'gh' was not found in PATH. Install it with 'winget install GitHub.cli'.",
            exception);
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string SafeSegment(string value)
    {
        var result = value.Replace("/", "_").Replace("\\", "_").Trim();
        if (string.IsNullOrWhiteSpace(result) || result.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new DevManifestException($"Unsafe GitHub release cache segment: {value}");

        return result;
    }

    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
            Error = string.IsNullOrWhiteSpace(error) ? output ?? string.Empty : error;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }
    }
}

internal readonly struct GitHubReleaseTextResult
{
    public GitHubReleaseTextResult(string text, bool usedCache)
    {
        Text = text ?? string.Empty;
        UsedCache = usedCache;
    }

    public string Text { get; }

    public bool UsedCache { get; }
}
