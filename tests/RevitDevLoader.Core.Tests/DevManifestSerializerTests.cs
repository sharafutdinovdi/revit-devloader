using RevitDevLoader.Core;

namespace RevitDevLoader.Core.Tests;

public sealed class DevManifestSerializerTests
{
    [Fact]
    public void ParseManifestReadsRequiredFieldsAndVersionEntries()
    {
        var text = string.Join(
            Environment.NewLine,
            "pluginName=SampleCommand",
            "displayName=SampleCommand Latest",
            "commandType=SampleCommand.Commands.OpenSampleCommandCommand",
            "updatedUtc=2026-06-04T10:00:00.0000000Z",
            @"version.2024.assemblyPath=C:\dev\runs\001\2024\SampleCommand.dll",
            @"version.2026.assemblyPath=C:\dev\runs\001\2026\SampleCommand.dll");

        var manifest = DevManifestSerializer.Parse(text);

        Assert.Equal("SampleCommand", manifest.PluginName);
        Assert.Equal("SampleCommand Latest", manifest.DisplayName);
        Assert.Equal(DevPluginType.Command, manifest.PluginType);
        Assert.Equal("SampleCommand.Commands.OpenSampleCommandCommand", manifest.CommandType);
        Assert.Equal(DateTimeKind.Utc, manifest.UpdatedUtc.Kind);
        Assert.Equal(2, manifest.Versions.Count);
        Assert.Equal(@"C:\dev\runs\001\2024\SampleCommand.dll", manifest.GetAssemblyPath("2024"));
        Assert.Equal(@"C:\dev\runs\001\2026\SampleCommand.dll", manifest.GetAssemblyPath("2026"));
    }

    [Fact]
    public void SerializeRoundTripsManifest()
    {
        var manifest = new DevPluginManifest(
            "SampleCommand",
            "SampleCommand Latest",
            "SampleCommand.Commands.OpenSampleCommandCommand",
            new DateTime(2026, 6, 4, 10, 0, 0, DateTimeKind.Utc),
            new[]
            {
                new DevPluginVersionEntry("2024", @"C:\dev\runs\002\2024\SampleCommand.dll"),
                new DevPluginVersionEntry("2026", @"C:\dev\runs\002\2026\SampleCommand.dll")
            });

        var text = DevManifestSerializer.Serialize(manifest);
        var parsed = DevManifestSerializer.Parse(text);

        Assert.Equal(manifest.PluginName, parsed.PluginName);
        Assert.Equal(manifest.DisplayName, parsed.DisplayName);
        Assert.Equal(manifest.CommandType, parsed.CommandType);
        Assert.Equal(@"C:\dev\runs\002\2024\SampleCommand.dll", parsed.GetAssemblyPath("2024"));
        Assert.Equal(@"C:\dev\runs\002\2026\SampleCommand.dll", parsed.GetAssemblyPath("2026"));
    }

    [Fact]
    public void SerializeRoundTripsCommandSlot()
    {
        var manifest = new DevPluginManifest(
            "ExternalChecker",
            "External model check",
            "ExternalChecker.Commands.RunCommand",
            "release-a",
            "1.0.0",
            @"C:\dev\runs\external",
            @"C:\updates\ExternalChecker.zip",
            7,
            new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc),
            new[] { new DevPluginVersionEntry("2026", @"C:\dev\runs\external\2026\ExternalChecker.dll") });

        var parsed = DevManifestSerializer.Parse(DevManifestSerializer.Serialize(manifest));

        Assert.Equal(7, parsed.CommandSlot);
    }

    [Fact]
    public void ParseManifestRejectsMissingCommandType()
    {
        var text = string.Join(
            Environment.NewLine,
            "pluginName=SampleCommand",
            "displayName=SampleCommand Latest",
            "updatedUtc=2026-06-04T10:00:00.0000000Z",
            @"version.2024.assemblyPath=C:\dev\runs\001\2024\SampleCommand.dll");

        var exception = Assert.Throws<DevManifestException>(() => DevManifestSerializer.Parse(text));

        Assert.Contains("commandType", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
