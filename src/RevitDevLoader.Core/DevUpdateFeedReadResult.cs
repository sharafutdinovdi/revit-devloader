using System;

namespace RevitDevLoader.Core;

public sealed class DevUpdateFeedReadResult
{
    public DevUpdateFeedReadResult(DevUpdateFeed feed, bool usedCache)
    {
        Feed = feed ?? throw new ArgumentNullException(nameof(feed));
        UsedCache = usedCache;
    }

    public DevUpdateFeed Feed { get; }

    public bool UsedCache { get; }
}
