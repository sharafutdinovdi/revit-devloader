using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class GitHubReleaseErrorClassifierTests
{
    [Theory]
    [InlineData("Run gh auth login to authenticate")]
    [InlineData("authentication required")]
    [InlineData("HTTP 401: Bad credentials")]
    [InlineData("HTTP 403: Resource not accessible")]
    [InlineData("release not found")]
    public void ClassifyRecognizesAuthenticationAndAccessErrors(string standardError)
    {
        var kind = GitHubReleaseErrorClassifier.Classify(standardError);

        Assert.Equal(GitHubReleaseErrorKind.AuthenticationOrAccess, kind);
    }

    [Theory]
    [InlineData("connection reset by peer")]
    [InlineData("release asset did not match any files")]
    [InlineData("")]
    public void ClassifyTreatsOtherFailuresAsOther(string standardError)
    {
        var kind = GitHubReleaseErrorClassifier.Classify(standardError);

        Assert.Equal(GitHubReleaseErrorKind.Other, kind);
    }
}
