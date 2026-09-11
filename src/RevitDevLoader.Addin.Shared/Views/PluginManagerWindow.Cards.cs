using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RevitDevLoader.Core;
using RevitDevLoader.Infrastructure;

namespace RevitDevLoader.Views;

public sealed partial class PluginManagerWindow
{
    private UIElement CreatePluginRow(DevPluginStatus status)
    {
        var rowState = PluginManagerRowState.Create(status);
        var summary = new Grid();
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        summary.Children.Add(CreatePluginIcon(status.Plugin.PluginId));

        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var pluginName = new TextBlock
        {
            Text = status.Plugin.DisplayName,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        pluginName.SetResourceReference(ForegroundProperty, "DSTextPrimary");
        identity.Children.Add(pluginName);

        var metadata = CreateStatusLine(status, rowState);
        identity.Children.Add(metadata);
        Grid.SetColumn(identity, 1);
        summary.Children.Add(identity);

        var statusIcon = CreateStatusIcon(rowState);
        if (statusIcon is not null)
        {
            Grid.SetColumn(statusIcon, 2);
            summary.Children.Add(statusIcon);
        }

        var action = CreatePrimaryAction(status, rowState);
        if (action is not null)
        {
            Grid.SetColumn(action, 3);
            summary.Children.Add(action);
        }

        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 0.5),
            Padding = new Thickness(12),
            Cursor = Cursors.Hand,
            Child = summary
        };
        border.SetResourceReference(BackgroundProperty, "DSBackgroundApp");
        border.SetResourceReference(BorderBrushProperty, "DSBackgroundHeader");
        border.MouseLeftButtonUp += (_, args) =>
        {
            if (IsActionClick(args.OriginalSource))
                return;

            OpenRowMenu(border, status);
            args.Handled = true;
        };
        return border;
    }

    private static bool IsActionClick(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is Button)
                return true;

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private static UIElement CreatePluginIcon(string pluginId)
    {
        return new Image
        {
            Width = 32,
            Height = 32,
            Source = RibbonIconFactory.CreateCatalogIcon(pluginId),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static UIElement? CreateStatusIcon(PluginManagerRowState rowState)
    {
        if (rowState.StatusIcon == PluginManagerStatusIcon.None)
            return null;

        if (rowState.StatusIcon == PluginManagerStatusIcon.ConventionalInstall)
        {
            var lockGlyph = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M6,9 L6,7 A4,4 0 0 1 14,7 L14,9 M5,9 L15,9 L15,17 L5,17 Z"),
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                ToolTip = rowState.StatusToolTipText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            lockGlyph.SetResourceReference(Shape.StrokeProperty, "DSTextSecondary");
            return new Viewbox { Width = 16, Height = 16, Child = lockGlyph };
        }

        var canvas = new Canvas { Width = 20, Height = 20 };
        var colorKey = rowState.StatusIcon switch
        {
            PluginManagerStatusIcon.Installed => "DSStatusSuccess",
            PluginManagerStatusIcon.UpdateAvailable => "DSActionPrimaryBg",
            PluginManagerStatusIcon.Error => "DSStatusError",
            _ => throw new InvalidOperationException($"Unsupported status icon '{rowState.StatusIcon}'.")
        };
        var circle = new Ellipse { Width = 18, Height = 18, StrokeThickness = 1.5 };
        circle.SetResourceReference(Shape.StrokeProperty, colorKey);
        Canvas.SetLeft(circle, 1);
        Canvas.SetTop(circle, 1);
        canvas.Children.Add(circle);

        var data = rowState.StatusIcon switch
        {
            PluginManagerStatusIcon.Installed => "M 5,10 L 8.5,13.5 L 15,6.5",
            PluginManagerStatusIcon.UpdateAvailable => "M 10,15 L 10,5 M 6,9 L 10,5 L 14,9",
            PluginManagerStatusIcon.Error => "M 6,6 L 14,14 M 14,6 L 6,14",
            _ => throw new InvalidOperationException($"Unsupported status icon '{rowState.StatusIcon}'.")
        };
        var mark = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        mark.SetResourceReference(Shape.StrokeProperty, colorKey);
        canvas.Children.Add(mark);

        var container = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(SurfaceCornerRadius),
            ToolTip = rowState.StatusToolTipText,
            VerticalAlignment = VerticalAlignment.Center,
            Child = canvas
        };
        return container;
    }

    private UIElement CreateStatusLine(DevPluginStatus status, PluginManagerRowState rowState)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 12, 0),
            SnapsToDevicePixels = true
        };
        TextOptions.SetTextFormattingMode(panel, TextFormattingMode.Display);

        var version = new TextBlock
        {
            Text = rowState.VersionText,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"{BuildInstalledText(status)}\n{BuildAvailableText(status)}"
        };
        version.SetResourceReference(ForegroundProperty, "DSTextSecondary");
        panel.Children.Add(version);

        return panel;
    }

    private UIElement? CreatePrimaryAction(DevPluginStatus status, PluginManagerRowState rowState)
    {
        if (rowState.PrimaryAction == PluginManagerPrimaryAction.None)
            return null;

        var button = CreateRowActionButton(
                rowState.PrimaryActionText,
                () => Install(status),
                isEnabled: true,
                primary: rowState.UseAccentPrimaryAction);
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.Margin = new Thickness(16, 0, 0, 0);
        return button;
    }

    private void OpenRowMenu(FrameworkElement row, DevPluginStatus status)
    {
        var menu = new ContextMenu
        {
            FontSize = 13,
            Placement = PlacementMode.MousePoint,
            PlacementTarget = row
        };
        menu.SetResourceReference(Control.BackgroundProperty, "DSBackgroundPanel");
        menu.SetResourceReference(Control.ForegroundProperty, "DSTextPrimary");
        menu.SetResourceReference(Control.BorderBrushProperty, "DSBorder");

        foreach (var itemState in PluginManagerRowMenuState.Create(status))
        {
            var item = new MenuItem
            {
                Header = GetMenuHeader(itemState.Action),
                IsEnabled = itemState.IsEnabled
            };
            item.Click += (_, _) => ExecuteMenuAction(itemState.Action, status);
            menu.Items.Add(item);
        }

        row.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private static string GetMenuHeader(PluginManagerRowMenuAction action)
    {
        return action switch
        {
            PluginManagerRowMenuAction.OpenFolder => "Open plugin folder",
            PluginManagerRowMenuAction.ShowVersions => "Show versions",
            PluginManagerRowMenuAction.Remove => "Remove",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    private void ExecuteMenuAction(PluginManagerRowMenuAction action, DevPluginStatus status)
    {
        switch (action)
        {
            case PluginManagerRowMenuAction.OpenFolder:
                OpenPluginFolder(status.Installed?.RunRoot ?? string.Empty);
                break;
            case PluginManagerRowMenuAction.ShowVersions:
                ShowVersions(status);
                break;
            case PluginManagerRowMenuAction.Remove:
                Remove(status);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private UIElement CreateEmptyState(bool hasPlugins)
    {
        var message = new TextBlock
        {
            Text = hasPlugins
                ? "No matches. Change the search query."
                : "No plugins found.\nCheck the update source in Settings and check for updates again.",
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        message.SetResourceReference(ForegroundProperty, "DSTextSecondary");
        return new Border
        {
            MinHeight = 144,
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = message
        };
    }

    private UIElement CreateErrorState(string text)
    {
        var message = new TextBlock
        {
            Text = text,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };
        message.SetResourceReference(ForegroundProperty, "DSStatusError");
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(SurfaceCornerRadius),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 16, 0, 0),
            MaxWidth = 520,
            Child = message
        };
        border.SetResourceReference(BackgroundProperty, "DSBackgroundPanel");
        border.SetResourceReference(BorderBrushProperty, "DSStatusError");
        return border;
    }

    private void ShowVersions(DevPluginStatus status)
    {
        var versions = _availablePackages
            .Where(item => string.Equals(item.PluginId, status.Plugin.PluginId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedUtc)
            .Take(12)
            .Select(item => $"{DevReleaseDisplayFormatter.Format(item.ReleaseId, item.AssemblyVersion)} · Revit {string.Join(", ", item.Versions)}")
            .ToList();
        var available = versions.Count == 0
            ? "The plugin is missing from the update source."
            : "Available versions:\n" + string.Join("\n", versions);
        var installed = status.Installed is null
            ? "Not currently installed."
            : $"Installed: {DevReleaseDisplayFormatter.Format(status.Installed.ReleaseId, status.Installed.AssemblyVersion)}";

        MessageBox.Show(
            this,
            $"{status.Plugin.DisplayName}\n\n{installed}\n\n{available}",
            "Plugin versions",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void OpenPluginFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            MessageBox.Show("Installed plugin folder not found.", "DevLoader", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var process = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
