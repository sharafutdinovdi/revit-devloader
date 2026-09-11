using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RevitDevLoader.Core;

public sealed class DevUpdateFeedReader
{
    private static readonly HttpClient HttpClient = new();

    public DevUpdateFeed Read(string source)
    {
        return ReadWithStatus(source).Feed;
    }

    public DevUpdateFeedReadResult ReadWithStatus(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Feed source is required.", nameof(source));

        var textResult = ReadTextWithStatus(source);
        return new DevUpdateFeedReadResult(ReadJson(textResult.Text), textResult.UsedCache);
    }

    public DevUpdateFeed ReadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new DevManifestException("Feed json is empty.");

        var serializer = new DataContractJsonSerializer(typeof(DevUpdateFeed));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return (DevUpdateFeed?)serializer.ReadObject(stream) ?? throw new DevManifestException("Feed json cannot be parsed.");
    }

    public string ReadText(string source)
    {
        return ReadTextWithStatus(source).Text;
    }

    private static GitHubReleaseTextResult ReadTextWithStatus(string source)
    {
        if (GitHubReleaseAssetReference.TryParse(source, out _))
            return GitHubReleaseAssetSource.DownloadText(source, DevUpdateLocations.GetDefaultCacheFolder());

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
                return new GitHubReleaseTextResult(File.ReadAllText(uri.LocalPath, Encoding.UTF8), usedCache: false);

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return new GitHubReleaseTextResult(HttpClient.GetStringAsync(uri).GetAwaiter().GetResult(), usedCache: false);
            }
        }

        return new GitHubReleaseTextResult(File.ReadAllText(source, Encoding.UTF8), usedCache: false);
    }
}
