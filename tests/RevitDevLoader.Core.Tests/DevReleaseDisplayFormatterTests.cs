using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevReleaseDisplayFormatterTests
{
    [Fact]
    public void FormatsTimestampReleaseIdWithoutHash()
    {
        var text = DevReleaseDisplayFormatter.Format("20260624-112313-a8102084", "1.0.0.0");

        Assert.Equal("24.06.2026 11:23", text);
    }

    [Fact]
    public void KeepsMeaningfulAssemblyVersion()
    {
        var text = DevReleaseDisplayFormatter.Format("20260624-112313-a8102084", "2.3.0.0");

        Assert.Equal("v2.3 · 24.06.2026 11:23", text);
    }

    [Fact]
    public void FallsBackToAssemblyVersionWhenReleaseIdIsOpaque()
    {
        var text = DevReleaseDisplayFormatter.Format("release-a", "2026.624.13.43101");

        Assert.Equal("v2026.624.13.43101", text);
    }
}
