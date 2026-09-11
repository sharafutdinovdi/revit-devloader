using System;

namespace RevitDevLoader.Core;

public enum GitHubReleaseErrorKind
{
    CliNotFound,
    AuthenticationOrAccess,
    Other
}

public sealed class GitHubReleaseAssetException : Exception
{
    public GitHubReleaseAssetException(GitHubReleaseErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public GitHubReleaseAssetException(GitHubReleaseErrorKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public GitHubReleaseErrorKind Kind { get; }
}

public static class GitHubReleaseErrorClassifier
{
    private static readonly string[] AuthenticationMarkers =
    {
        "gh auth login",
        "authentication",
        "not authenticated",
        "bad credentials",
        "resource not accessible",
        "oauth",
        "http 401",
        "http 403",
        "status code 401",
        "status code 403",
        "not found"
    };

    public static GitHubReleaseErrorKind Classify(string? standardError)
    {
        var text = standardError ?? string.Empty;
        return Array.Exists(
            AuthenticationMarkers,
            marker => text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            ? GitHubReleaseErrorKind.AuthenticationOrAccess
            : GitHubReleaseErrorKind.Other;
    }
}
