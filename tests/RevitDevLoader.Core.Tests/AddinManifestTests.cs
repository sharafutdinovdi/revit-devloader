using System.Xml.Linq;

namespace RevitDevLoader.Core.Tests;

public sealed class AddinManifestTests
{
    [Theory]
    [InlineData("Legacy")]
    [InlineData("Modern")]
    public void ManifestDeclaresApplicationWithValidIdentity(string kind)
    {
        var root = DevLoaderReleaseManifestTests.FindRepoRoot();
        var project = $"RevitDevLoader.Addin.{kind}";
        var document = XDocument.Load(Path.Combine(root, "src", project, project + ".addin"));
        Assert.Empty(document.Descendants("ManifestSettings"));
        var addin = Assert.Single(document.Root!.Elements("AddIn"));
        Assert.Equal("Application", addin.Attribute("Type")?.Value);
        Assert.Equal("RevitDevLoader", addin.Element("Name")?.Value);
        Assert.Equal($"RevitDevLoader\\{project}.dll", addin.Element("Assembly")?.Value);
        Assert.Equal("RevitDevLoader.App", addin.Element("FullClassName")?.Value);
        Assert.True(Guid.TryParse(addin.Element("AddInId")?.Value, out _));
        Assert.Equal("DSHA", addin.Element("VendorId")?.Value);
        Assert.Equal("Dinar Sharafutdinov, https://github.com/sharafutdinovdi/revit-devloader", addin.Element("VendorDescription")?.Value);
    }
}
