using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitDevLoader.Core;

public sealed class DevPluginCatalogItem
{
    public DevPluginCatalogItem(
        string pluginId,
        string displayName,
        string commandType,
        string mainAssembly,
        IEnumerable<string> supportedVersions,
        string iconText,
        DevPluginType pluginType = DevPluginType.Command,
        string applicationClass = "")
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin id is required.", nameof(pluginId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        if (pluginType == DevPluginType.Command && string.IsNullOrWhiteSpace(commandType))
            throw new ArgumentException("Command type is required.", nameof(commandType));
        if (pluginType == DevPluginType.Application && string.IsNullOrWhiteSpace(applicationClass))
            throw new ArgumentException("Application class is required.", nameof(applicationClass));
        if (string.IsNullOrWhiteSpace(mainAssembly))
            throw new ArgumentException("Main assembly is required.", nameof(mainAssembly));
        if (supportedVersions is null)
            throw new ArgumentNullException(nameof(supportedVersions));

        var versions = supportedVersions
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (versions.Count == 0)
            throw new ArgumentException("At least one supported version is required.", nameof(supportedVersions));

        PluginId = pluginId.Trim();
        DisplayName = displayName.Trim();
        PluginType = pluginType;
        CommandType = (commandType ?? string.Empty).Trim();
        ApplicationClass = (applicationClass ?? string.Empty).Trim();
        MainAssembly = mainAssembly.Trim();
        SupportedVersions = versions;
        IconText = string.IsNullOrWhiteSpace(iconText) ? pluginId.Trim().Substring(0, Math.Min(2, pluginId.Trim().Length)).ToUpperInvariant() : iconText.Trim();
    }

    public string PluginId { get; }

    public string DisplayName { get; }

    public DevPluginType PluginType { get; }

    public string CommandType { get; }

    public string ApplicationClass { get; }

    public string MainAssembly { get; }

    public IReadOnlyList<string> SupportedVersions { get; }

    public string IconText { get; }

    public string RibbonText => FormatRibbonText(DisplayName);

    public bool SupportsVersion(string revitVersion)
    {
        if (string.IsNullOrWhiteSpace(revitVersion))
            return false;

        return SupportedVersions.Contains(revitVersion.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string FormatRibbonText(string displayName)
    {
        const int maxLineLength = 14;
        var words = displayName
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return string.Empty;

        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (candidate.Length <= maxLineLength || current.Length == 0)
            {
                current = candidate;
                continue;
            }

            lines.Add(current);
            current = word;
            if (lines.Count == 2)
                break;
        }

        if (lines.Count < 2 && current.Length > 0)
            lines.Add(current);

        var consumedWords = string.Join(" ", lines).Split(' ').Length;
        if (consumedWords < words.Length || lines[lines.Count - 1].Length > maxLineLength)
        {
            var last = lines[lines.Count - 1];
            lines[lines.Count - 1] = last.Substring(0, Math.Min(last.Length, maxLineLength - 1)).TrimEnd() + "…";
        }

        return string.Join("\n", lines);
    }
}
