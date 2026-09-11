using RevitDevLoader.Core;
using RevitDevLoader.Views;

namespace RevitDevLoader.Core.Tests;

public sealed class PluginManagerRowStateTests
{
    [Theory]
    [InlineData(DevPluginStatusKind.UpdateAvailable, true, true, (int)PluginManagerPrimaryAction.Update, "Update")]
    [InlineData(DevPluginStatusKind.Latest, true, true, (int)PluginManagerPrimaryAction.Reinstall, "Reinstall")]
    [InlineData(DevPluginStatusKind.InstalledNoPackage, true, false, (int)PluginManagerPrimaryAction.None, "")]
    [InlineData(DevPluginStatusKind.NotInstalled, false, true, (int)PluginManagerPrimaryAction.Install, "Install")]
    [InlineData(DevPluginStatusKind.NotInstalled, false, false, (int)PluginManagerPrimaryAction.None, "")]
    [InlineData(DevPluginStatusKind.UnsupportedRevitVersion, false, false, (int)PluginManagerPrimaryAction.None, "")]
    public void SelectsSinglePrimaryAction(
        DevPluginStatusKind kind,
        bool installed,
        bool available,
        int expectedAction,
        string expectedText)
    {
        var state = PluginManagerRowState.Create(CreateStatus(kind, installed, available));

        Assert.Equal((PluginManagerPrimaryAction)expectedAction, state.PrimaryAction);
        Assert.Equal(expectedText, state.PrimaryActionText);
    }

    [Theory]
    [InlineData(DevPluginStatusKind.UpdateAvailable, true, true, (int)PluginManagerStatusIcon.UpdateAvailable, "Update available")]
    [InlineData(DevPluginStatusKind.Latest, true, true, (int)PluginManagerStatusIcon.Installed, "Installed")]
    [InlineData(DevPluginStatusKind.InstalledNoPackage, true, false, (int)PluginManagerStatusIcon.Installed, "Installed")]
    [InlineData(DevPluginStatusKind.NotInstalled, false, true, (int)PluginManagerStatusIcon.None, "Not installed")]
    [InlineData(DevPluginStatusKind.NotInstalled, false, false, (int)PluginManagerStatusIcon.None, "Not installed")]
    [InlineData(DevPluginStatusKind.UnsupportedRevitVersion, false, false, (int)PluginManagerStatusIcon.Error, "Unavailable")]
    [InlineData(DevPluginStatusKind.CommandSlotUnavailable, false, true, (int)PluginManagerStatusIcon.Error, "No free slot")]
    [InlineData(DevPluginStatusKind.PackageError, false, false, (int)PluginManagerStatusIcon.Error, "Package error")]
    public void SelectsStatusIconAndText(
        DevPluginStatusKind kind,
        bool installed,
        bool available,
        int expectedIcon,
        string expectedText)
    {
        var state = PluginManagerRowState.Create(CreateStatus(kind, installed, available));

        Assert.Equal((PluginManagerStatusIcon)expectedIcon, state.StatusIcon);
        Assert.Equal(expectedText, state.StatusText);
    }

    [Theory]
    [InlineData(DevPluginStatusKind.UpdateAvailable, true, true, "Version 1.0.0")]
    [InlineData(DevPluginStatusKind.Latest, true, true, "Version 1.0.0")]
    [InlineData(DevPluginStatusKind.NotInstalled, false, true, "Version 1.0.1")]
    public void FormatsVersionLine(
        DevPluginStatusKind kind,
        bool installed,
        bool available,
        string expectedText)
    {
        var state = PluginManagerRowState.Create(CreateStatus(kind, installed, available));

        Assert.Equal(expectedText, state.VersionText);
    }

    [Fact]
    public void UpdateAvailableUsesAccentAction()
    {
        var state = PluginManagerRowState.Create(
            CreateStatus(DevPluginStatusKind.UpdateAvailable, installed: true, available: true));

        Assert.Equal("Update", state.PrimaryActionText);
        Assert.True(state.UseAccentPrimaryAction);
    }

    [Fact]
    public void NotInstalledHasNoIcon()
    {
        var installed = PluginManagerRowState.Create(
            CreateStatus(DevPluginStatusKind.Latest, installed: true, available: true));
        var notInstalled = PluginManagerRowState.Create(
            CreateStatus(DevPluginStatusKind.NotInstalled, installed: false, available: true));
        var update = PluginManagerRowState.Create(
            CreateStatus(DevPluginStatusKind.UpdateAvailable, installed: true, available: true));

        Assert.Equal(PluginManagerStatusIcon.None, notInstalled.StatusIcon);
        Assert.NotEqual(PluginManagerStatusIcon.None, installed.StatusIcon);
        Assert.NotEqual(PluginManagerStatusIcon.None, update.StatusIcon);
    }

    [Fact]
    public void RowMenuAlwaysContainsExactlyThreeActionsInRequiredOrder()
    {
        var status = CreateStatus(DevPluginStatusKind.NotInstalled, installed: false, available: true);

        var items = PluginManagerRowMenuState.Create(status);

        Assert.Collection(
            items,
            item => Assert.Equal(PluginManagerRowMenuAction.OpenFolder, item.Action),
            item => Assert.Equal(PluginManagerRowMenuAction.ShowVersions, item.Action),
            item => Assert.Equal(PluginManagerRowMenuAction.Remove, item.Action));
        Assert.False(items[0].IsEnabled);
        Assert.True(items[1].IsEnabled);
        Assert.False(items[2].IsEnabled);
    }

    [Theory]
    [InlineData(DevPluginStatusKind.UnsupportedRevitVersion, false, false, "No version for Revit 2023")]
    [InlineData(DevPluginStatusKind.CommandSlotUnavailable, false, true, "20 DevLoader slots")]
    [InlineData(DevPluginStatusKind.NotInstalled, false, false, "missing from the update source")]
    [InlineData(DevPluginStatusKind.PackageError, false, false, "Open the logs")]
    public void ExplainsWhyPrimaryActionIsUnavailable(
        DevPluginStatusKind kind,
        bool installed,
        bool available,
        string expectedReason)
    {
        var state = PluginManagerRowState.Create(CreateStatus(kind, installed, available));

        Assert.Equal(PluginManagerPrimaryAction.None, state.PrimaryAction);
        Assert.Contains(expectedReason, state.ReasonText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConventionalInstallationOverridesDevLoaderAction()
    {
        var status = CreateStatus(
            DevPluginStatusKind.UpdateAvailable,
            installed: true,
            available: true,
            details: DevPluginStatusService.ConventionalInstallWarningPrefix + " test");

        var state = PluginManagerRowState.Create(status);

        Assert.Equal(PluginManagerPrimaryAction.None, state.PrimaryAction);
        Assert.Equal(PluginManagerStatusIcon.ConventionalInstall, state.StatusIcon);
        Assert.Empty(state.ReasonText);
        Assert.Equal("Managed outside DevLoader", state.StatusText);
        Assert.Equal("The plugin is installed manually and managed outside DevLoader", state.StatusToolTipText);
        Assert.All(PluginManagerRowMenuState.Create(status), item => Assert.False(item.IsEnabled));
    }

    [Theory]
    [InlineData("", false, true)]
    [InlineData("", true, false)]
    [InlineData("Sample", false, false)]
    [InlineData("Sample", true, false)]
    public void SearchPlaceholderIsVisibleOnlyWhenEmptyAndUnfocused(
        string text,
        bool hasKeyboardFocus,
        bool expected)
    {
        Assert.Equal(expected, PluginManagerSearchState.ShouldShowPlaceholder(text, hasKeyboardFocus));
    }

    [Fact]
    public void SearchFiltersLoadedStatusesWithoutReloadingSources()
    {
        var update = CreateStatus(DevPluginStatusKind.UpdateAvailable, installed: true, available: true);
        var unavailable = CreateStatus(DevPluginStatusKind.UnsupportedRevitVersion, installed: false, available: false);

        Assert.True(PluginManagerRowState.MatchesSearch(update, "test"));
        Assert.True(PluginManagerRowState.MatchesSearch(update, "update"));
        Assert.True(PluginManagerRowState.MatchesSearch(unavailable, "Revit 2023"));
        Assert.False(PluginManagerRowState.MatchesSearch(update, "families"));
        Assert.True(PluginManagerRowState.MatchesSearch(update, "   "));
    }

    private static DevPluginStatus CreateStatus(
        DevPluginStatusKind kind,
        bool installed,
        bool available,
        string details = "")
    {
        if (kind == DevPluginStatusKind.CommandSlotUnavailable && details.Length == 0)
            details = "All 20 DevLoader slots are occupied.";
        var plugin = new DevPluginCatalogItem(
            "TestPlugin",
            "Test plugin",
            "TestPlugin.Command",
            "TestPlugin.dll",
            new[] { "2023" },
            "TP");
        var manifest = installed
            ? new DevPluginManifest(
                "TestPlugin",
                "Test plugin",
                "TestPlugin.Command",
                "1.0.0",
                "1.0.0",
                Path.GetTempPath(),
                string.Empty,
                DateTime.UtcNow,
                new[] { new DevPluginVersionEntry("2023", Path.Combine(Path.GetTempPath(), "TestPlugin.dll")) })
            : null;
        var package = available
            ? new DevPayloadPackageInfo(
                Path.Combine(Path.GetTempPath(), "TestPlugin-DevPayload.zip"),
                "TestPlugin",
                "Test plugin",
                "1.0.1",
                "1.0.1",
                "TestPlugin.Command",
                "TestPlugin.dll",
                DateTime.UtcNow,
                new[] { "2023" },
                schemaVersion: 2)
            : null;
        return new DevPluginStatus(plugin, "2023", manifest, package, kind, details);
    }
}
