using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        summary.Children.Add(CreatePluginIcon(status));

        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var pluginName = new TextBlock
        {
            Text = status.Plugin.DisplayName,
            ToolTip = status.Available?.Description ?? status.Installed?.Description,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        pluginName.SetResourceReference(ForegroundProperty, "DSTextPrimary");
        identity.Children.Add(pluginName);

        var metadata = CreateStatusLine(status, rowState);
        identity.Children.Add(metadata);
        var operation = new TextBlock
        {
            Text = _operationStatuses.TryGetValue(status.Plugin.PluginId, out var message)
                ? message : rowState.ReasonText,
            FontSize = 11,
            MinHeight = 16,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        operation.ToolTip = operation.Text;
        operation.SetResourceReference(ForegroundProperty,
            message is null ? "DSTextSecondary" : "DSStatusSuccess");
        identity.Children.Add(operation);
        Grid.SetColumn(identity, 1);
        summary.Children.Add(identity);

        var statusIcon = CreateStatusIcon(rowState);
        if (statusIcon is not null)
        {
            Grid.SetColumn(statusIcon, 2);
            summary.Children.Add(statusIcon);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var action = CreatePrimaryAction(status, rowState);
        if (action is not null)
            actions.Children.Add(action);
        if (status.Installed is not null && !HasConventionalInstallation(status))
        {
            var uninstall = CreateSecondaryButton("Uninstall", () => Remove(status), 76);
            uninstall.Height = 32;
            actions.Children.Add(uninstall);
            var folderGlyph = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M1,4 L7,4 L9,6 L17,6 L17,16 L1,16 Z"),
                StrokeThickness = 1.5,
                Width = 18,
                Height = 18
            };
            folderGlyph.SetResourceReference(Shape.StrokeProperty, "DSTextSecondary");
            var folder = CreateHeaderIconButton(folderGlyph, "Open folder", () => OpenPluginFolder(status.Installed.RunRoot));
            System.Windows.Automation.AutomationProperties.SetName(folder, "Open folder");
            actions.Children.Add(folder);
        }
        Grid.SetColumn(actions, 3);
        summary.Children.Add(actions);

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

    private UIElement CreatePluginIcon(DevPluginStatus status)
    {
        var icon = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Clip = new RectangleGeometry(new Rect(0, 0, 40, 40), 8, 8),
            Background = (Brush)new BrushConverter().ConvertFromString(DevPluginIconFallback.GetColor(status.Plugin.PluginId))!,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = DevPluginIconFallback.GetLetter(status.Plugin.DisplayName),
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var source = status.Available?.IconPath ?? string.Empty;
        if (status.Available?.PackageManifest is not null)
            source = string.Empty;
        var installedIcon = status.Installed is null ? string.Empty : System.IO.Path.Combine(status.Installed.RunRoot, status.Installed.IconPath);
        if (string.IsNullOrEmpty(source))
            source = installedIcon;
        if (!string.IsNullOrEmpty(source))
            icon.Loaded += async (_, _) =>
            {
                var image = await LoadIconAsync(source);
                if (image is null && File.Exists(installedIcon))
                    image = await LoadIconAsync(installedIcon);
                if (image is not null && !_isClosed)
                    icon.Child = new Image { Source = image, Stretch = Stretch.UniformToFill };
            };
        return icon;
    }

    private Task<ImageSource?> LoadIconAsync(string source)
    {
        if (_iconTasks.TryGetValue(source, out var task))
            return task;
        task = Task.Run<ImageSource?>(() =>
        {
            try
            {
                var path = _packageCache.PrepareIcon(source, DevUpdateLocations.GetDefaultCacheFolder());
                using var stream = File.OpenRead(path);
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var bitmap = decoder.Frames[0];
                if (bitmap.PixelWidth < 32 || bitmap.PixelWidth != bitmap.PixelHeight)
                    throw new InvalidDataException("Catalog icons must be square PNGs of at least 32 pixels.");
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception exception)
            {
                _logger.Warn($"Catalog icon unavailable. Source='{source}'. Error='{exception.Message}'.");
                return null;
            }
        });
        _iconTasks[source] = task;
        return task;
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
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 4, 12, 0),
            SnapsToDevicePixels = true
        };
        TextOptions.SetTextFormattingMode(panel, TextFormattingMode.Display);

        var version = new TextBlock
        {
            Text = $"{rowState.VersionText} · {rowState.StatusText}",
            TextTrimming = TextTrimming.CharacterEllipsis,
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
            PluginManagerRowMenuAction.Remove => "Uninstall",
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
