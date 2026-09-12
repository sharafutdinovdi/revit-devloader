using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace RevitDevLoader.Core;

public sealed class DevUpdatePackageCache
{
    private static readonly HttpClient HttpClient = new();

    public string PrepareIcon(string source, string cacheRoot)
    {
        var localPath = GetLocalPackagePath(source);
        if (!string.IsNullOrEmpty(localPath))
            return localPath;

        using var sha = SHA256.Create();
        var key = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source))).Replace("-", string.Empty);
        var directory = Path.Combine(cacheRoot, "icons", key);
        var destination = Path.Combine(directory, "icon.png");
        if (File.Exists(destination))
            return destination;
        Directory.CreateDirectory(directory);
        if (GitHubReleaseAssetReference.TryParse(source, out var reference))
        {
            var downloaded = GitHubReleaseAssetSource.Download(reference, directory);
            if (!string.Equals(downloaded, destination, StringComparison.OrdinalIgnoreCase))
                File.Move(downloaded, destination);
        }
        else
        {
            using var stream = HttpClient.GetStreamAsync(source).GetAwaiter().GetResult();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            File.WriteAllBytes(destination, buffer.ToArray());
        }
        return destination;
    }

    public string PreparePackage(DevPayloadPackageInfo package, string cacheRoot)
    {
        if (package is null)
            throw new ArgumentNullException(nameof(package));
        if (string.IsNullOrWhiteSpace(cacheRoot))
            throw new ArgumentException("Cache root is required.", nameof(cacheRoot));

        var localPath = GetLocalPackagePath(package.PackagePath);
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            Verify(localPath, package);
            return localPath;
        }

        if (GitHubReleaseAssetReference.TryParse(package.PackagePath, out var githubAsset))
            return PrepareGitHubReleasePackage(package, githubAsset, cacheRoot);

        if (!package.IsRemotePackage)
            throw new DevManifestException($"Package path is not supported: {package.PackagePath}");

        var destination = GetCachePath(package, cacheRoot);
        if (File.Exists(destination))
        {
            Verify(destination, package);
            return destination;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var tempPath = destination + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        using (var stream = HttpClient.GetStreamAsync(package.PackagePath).GetAwaiter().GetResult())
        using (var file = File.Create(tempPath))
            stream.CopyTo(file);

        Verify(tempPath, package);
        if (File.Exists(destination))
            File.Delete(destination);
        File.Move(tempPath, destination);
        return destination;
    }

    private static string PrepareGitHubReleasePackage(DevPayloadPackageInfo package, GitHubReleaseAssetReference reference, string cacheRoot)
    {
        var destination = GetCachePath(package, cacheRoot);
        if (File.Exists(destination))
        {
            Verify(destination, package);
            return destination;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var tempFolder = Path.Combine(Path.GetDirectoryName(destination)!, ".github-" + Guid.NewGuid().ToString("N"));
        var tempPath = destination + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            var downloaded = GitHubReleaseAssetSource.Download(reference, tempFolder);
            File.Move(downloaded, tempPath);
            Verify(tempPath, package);
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(tempPath, destination);
            return destination;
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, recursive: true);
        }
    }

    public static void Verify(string packagePath, DevPayloadPackageInfo package)
    {
        if (!File.Exists(packagePath))
            throw new DevManifestException($"Package file not found: {packagePath}");

        if (package.SizeBytes > 0)
        {
            var actualSize = new FileInfo(packagePath).Length;
            if (actualSize != package.SizeBytes)
                throw new DevManifestException($"Package size mismatch for {package.PluginId}: expected {package.SizeBytes}, actual {actualSize}.");
        }

        if (!package.HasHash)
            return;

        var actualHash = ComputeSha256(packagePath);
        if (!string.Equals(actualHash, package.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new DevManifestException($"Package sha256 mismatch for {package.PluginId}: expected {package.Sha256}, actual {actualHash}.");
    }

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToUpperInvariant();
    }

    private static string GetCachePath(DevPayloadPackageInfo package, string cacheRoot)
    {
        var fileName = $"{SafeSegment(package.PluginId)}-DevPayload-{SafeSegment(package.ReleaseId)}.zip";
        var destination = Path.Combine(cacheRoot, SafeSegment(package.PluginId), fileName);
        EnsureChildPath(cacheRoot, destination);
        return destination;
    }

    private static string GetLocalPackagePath(string packagePath)
    {
        if (Uri.TryCreate(packagePath, UriKind.Absolute, out var uri))
            return uri.IsFile ? uri.LocalPath : string.Empty;

        return packagePath;
    }

    private static string SafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains("\\") ||
            value.Contains("/"))
            throw new DevManifestException($"Unsafe package cache path segment: {value}");

        return value.Trim();
    }

    private static void EnsureChildPath(string parent, string child)
    {
        var resolvedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedChild = Path.GetFullPath(child);
        if (!resolvedChild.StartsWith(resolvedParent, StringComparison.OrdinalIgnoreCase))
            throw new DevManifestException($"Path escapes package cache: {resolvedChild}");
    }
}
