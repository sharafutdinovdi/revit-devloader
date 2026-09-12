using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitDevLoader.Core;

namespace RevitDevLoader.Infrastructure;

public static class RibbonRuntimeService
{
    public const string PanelName = "DevLoader";

    private const string ManagerButtonName = "RevitDevLoader_Manager";
    private const string ManagerButtonText = "Dev";

    public static void EnsureStartupButtons(UIControlledApplication application, FileLogger logger)
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var panel = GetOrCreatePanel(application, PanelName);
        AddOrShowItem(panel, CreateManagerButton(assemblyPath));

        var revitVersion = GetRevitMajorVersion(application.ControlledApplication.VersionNumber);
        var registry = new DevPluginRegistry();
        var statuses = new DevPluginStatusService(registry, logger.CreateLogger<DevPluginStatusService>()).BuildStatuses(
            DevPluginCatalog.CreateDefault(),
            Array.Empty<DevPayloadPackageInfo>(),
            revitVersion);

        foreach (var status in statuses.Where(item => item.CanRun && item.Installed is not null))
            RefreshPluginButtons(panel, status.Installed!, assemblyPath);

        logger.Info($"Ribbon startup refreshed. RevitVersion='{revitVersion}'. Buttons='{statuses.Count(item => item.CanRun)}'.");
    }

    public static void EnsurePluginButton(UIApplication application, DevPluginCatalogItem plugin, FileLogger logger)
    {
        var revitVersion = GetRevitMajorVersion(application.Application.VersionNumber);
        if (!plugin.SupportsVersion(revitVersion))
            return;

        var registry = new DevPluginRegistry();
        DevPluginManifest manifest;
        try
        {
            manifest = registry.Load(plugin.PluginId);
        }
        catch (DevManifestException exception)
        {
            logger.Warn($"Ribbon plugin button skipped. PluginId='{plugin.PluginId}'. Error='{exception.Message}'.");
            return;
        }

        if (manifest.PluginType == DevPluginType.Application)
        {
            logger.Info($"Ribbon plugin button skipped. PluginId='{plugin.PluginId}'. Reason='Application plugin'.");
            return;
        }

        if (!manifest.CommandSlot.HasValue)
        {
            logger.Warn($"Ribbon plugin button skipped. PluginId='{plugin.PluginId}'. Reason='Command slot is not assigned'.");
            return;
        }

        var panel = GetOrCreatePanel(application, PanelName);
        RefreshPluginButtons(panel, manifest, Assembly.GetExecutingAssembly().Location);
        logger.Info($"Ribbon plugin buttons visible. PluginId='{plugin.PluginId}'. Commands='{manifest.Commands.Count}'.");
    }

    public static void HidePluginButton(UIApplication application, string pluginId, FileLogger logger)
    {
        foreach (var panel in application.GetRibbonPanels())
        {
            foreach (var item in panel.GetItems().Where(item => IsPluginButton(item.Name, pluginId)))
            {
                item.Visible = false;
                item.Enabled = false;
                logger.Info($"Ribbon plugin button hidden. PluginId='{pluginId}'. Item='{item.Name}'.");
            }
        }
    }

    private static bool IsPluginButton(string name, string pluginId) =>
        name == GetPluginButtonName(pluginId) || name.StartsWith(GetPluginButtonName(pluginId) + ":", StringComparison.Ordinal);

    private static void RefreshPluginButtons(RibbonPanel panel, DevPluginManifest manifest, string assemblyPath)
    {
        foreach (var item in panel.GetItems().Where(item => IsPluginButton(item.Name, manifest.PluginName)))
        {
            item.Visible = false;
            item.Enabled = false;
        }
        foreach (var command in manifest.Commands.Where(command => command.Slot.HasValue))
            AddOrShowItem(panel, CreatePluginButton(manifest, command, assemblyPath));
    }

    private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string panelName)
    {
        return application.GetRibbonPanels().FirstOrDefault(item => item.Name == panelName)
            ?? application.CreateRibbonPanel(panelName);
    }

    private static RibbonPanel GetOrCreatePanel(UIApplication application, string panelName)
    {
        return application.GetRibbonPanels().FirstOrDefault(item => item.Name == panelName)
            ?? application.CreateRibbonPanel(panelName);
    }

    private static RibbonItem AddOrShowItem(RibbonPanel panel, PushButtonData buttonData)
    {
        var item = panel.GetItems().FirstOrDefault(candidate => candidate.Name == buttonData.Name)
            ?? panel.AddItem(buttonData);
        if (item is PushButton button)
        {
            button.ClassName = buttonData.ClassName;
            button.AssemblyName = buttonData.AssemblyName;
            button.ItemText = buttonData.Text;
            button.ToolTip = buttonData.ToolTip;
            button.Image = buttonData.Image;
            button.LargeImage = buttonData.LargeImage;
        }
        item.Visible = true;
        item.Enabled = true;
        return item;
    }

    private static PushButtonData CreateManagerButton(string assemblyPath)
    {
        return new PushButtonData(
            ManagerButtonName,
            ManagerButtonText,
            assemblyPath,
            "RevitDevLoader.Commands.OpenPluginManagerCommand")
        {
            ToolTip = "Open the DevLoader plugin manager.",
            LongDescription = "Shows installed plugins and available updates.",
            Image = RibbonIconFactory.CreateDevIcon(),
            LargeImage = RibbonIconFactory.CreateDevIcon()
        };
    }

    private static PushButtonData CreatePluginButton(DevPluginManifest manifest, DevPackageCommand command, string assemblyPath)
    {
        var iconPath = Path.Combine(manifest.RunRoot, string.IsNullOrEmpty(command.Icon) ? manifest.IconPath : command.Icon);
        return new PushButtonData(
            GetPluginButtonName(manifest.PluginName) + ":" + command.Id,
            command.Text,
            assemblyPath,
            GetRunnerCommandType(command.Slot!.Value))
        {
            ToolTip = string.IsNullOrEmpty(command.Tooltip) ? manifest.DisplayName : command.Tooltip,
            LongDescription = manifest.Description,
            Image = RibbonIconFactory.LoadPackageIcon(iconPath, 16) ?? RibbonIconFactory.CreateCatalogIcon(manifest.PluginName),
            LargeImage = RibbonIconFactory.LoadPackageIcon(iconPath, 32) ?? RibbonIconFactory.CreateCatalogIcon(manifest.PluginName)
        };
    }

    private static string GetPluginButtonName(string pluginId) => $"RevitDevLoader_Run_{pluginId}";

    private static string GetRunnerCommandType(int commandSlot) =>
        $"RevitDevLoader.Commands.RunPluginSlot{commandSlot:00}Command";

    private static string GetRevitMajorVersion(string version)
    {
        return version.Length >= 4 ? version.Substring(0, 4) : string.Empty;
    }
}
