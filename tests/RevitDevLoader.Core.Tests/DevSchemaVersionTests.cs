using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevSchemaVersionTests
{
    [Fact]
    public void SupportedSchemaVersionsMatchPublishedFormats()
    {
        Assert.Equal(1, DevFeedSchema.SupportedVersion);
        Assert.Equal(2, DevPayloadSchema.SupportedVersion);
    }
}
