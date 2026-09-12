using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using RevitDevLoader.Core;
using RevitDevLoader.Infrastructure;

namespace RevitDevLoader.Views;

public sealed partial class PluginManagerWindow : Window
{
    private const double SurfaceCornerRadius = 8;
    private readonly Dictionary<string, string> _operationStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, System.Threading.Tasks.Task<ImageSource?>> _iconTasks = new(StringComparer.Ordinal);


    private readonly ExternalCommandData _commandData;
    private readonly FileLogger _logger;
    private readonly DevUpdateSourceService _updateSourceService;
    private readonly DevUpdatePackageCache _packageCache = new();
    private readonly DevPayloadInstaller _installer = new();
    private readonly DevPluginRegistry _registry = new();
    private readonly StackPanel _rows = new()
    {
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly System.Windows.Controls.TextBox _searchBox = new();
    private readonly TextBlock _searchPlaceholder = new() { Text = "Search plugins", IsHitTestVisible = false };
    private readonly Button _clearSearchButton = new();
    private readonly TextBlock _feedUpdateIndicator = new()
    {
        Text = "Refreshing feed...",
        Visibility = Visibility.Visible,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly HashSet<string> _shownConventionalWarnings = new(StringComparer.OrdinalIgnoreCase);
    private Button? _checkUpdatesButton;
    private bool _isBusy;
    private bool _isCheckingValue;
    private bool _isClosed;
    private bool _revitApiContextAvailable = true;
    private IReadOnlyList<DevPluginStatus> _displayStatuses = Array.Empty<DevPluginStatus>();
    private IReadOnlyList<DevPayloadPackageInfo> _availablePackages = Array.Empty<DevPayloadPackageInfo>();
    private DevLoaderSettings _settings;
    private readonly string _revitVersion;
    private string _updatesFolder;

    private bool _isChecking
    {
        get => _isCheckingValue;
        set
        {
            var wasChecking = _isCheckingValue;
            _isCheckingValue = value;
            _feedUpdateIndicator.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (wasChecking && !value && IsLoaded && !_isClosed)
                ShowConventionalInstallWarnings();
        }
    }

    private IReadOnlyList<DevPluginStatus> _allStatuses
    {
        get => _displayStatuses;
        set => _displayStatuses = AllowConventionalInstallActions(value);
    }

    public PluginManagerWindow(ExternalCommandData commandData, FileLogger logger)
    {
        _commandData = commandData ?? throw new ArgumentNullException(nameof(commandData));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _updateSourceService = new DevUpdateSourceService(logger.CreateLogger<DevUpdateSourceService>());
        _revitVersion = GetRevitMajorVersion(commandData);
        _settings = DevLoaderSettings.Load();
        _updatesFolder = _settings.UpdatesFolder;

        DSTheme.Apply(this);
        Title = "DevLoader";
        Width = 760;
        MinWidth = 680;
        MaxHeight = 760;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "DSBackgroundApp");
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        Content = BuildLayout();

        Closing += OnClosing;
        Closed += OnClosed;
        Loaded += OnLoaded;
        ApplySnapshot(Array.Empty<DevPayloadPackageInfo>());
    }

    private UIElement BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(BuildHeader());

        var pluginList = new Border { Padding = new Thickness(24), Child = BuildMiddleZone() };
        pluginList.SetResourceReference(BackgroundProperty, "DSBackgroundApp");
        Grid.SetRow(pluginList, 1);
        root.Children.Add(pluginList);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private UIElement BuildHeader()
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.Children.Add(BuildSearchControl());

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        tools.Children.Add(CreateHeaderIconButton(CreateSettingsIcon(), "Settings", OpenSettings));
        tools.Children.Add(CreateHeaderIconButton(CreateJournalIcon(), "Logs", OpenLogsFolder));
        Grid.SetColumn(tools, 1);
        panel.Children.Add(tools);

        var surface = new Border
        {
            Height = 56,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 12, 24, 12),
            Child = panel
        };
        surface.SetResourceReference(BackgroundProperty, "DSBackgroundHeader");
        surface.SetResourceReference(BorderBrushProperty, "DSBorder");
        return surface;
    }

    private UIElement BuildSearchControl()
    {
        _searchBox.Width = 240;
        _searchBox.MaxWidth = 240;
        _searchBox.Height = 32;
        _searchBox.HorizontalAlignment = HorizontalAlignment.Left;
        _searchBox.Padding = new Thickness(32, 0, 32, 0);
        _searchBox.HorizontalContentAlignment = HorizontalAlignment.Left;
        _searchBox.TextAlignment = TextAlignment.Left;
        _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
        _searchBox.ToolTip = "Enter a plugin name or status";
        _searchBox.Template = CreateSearchBoxTemplate();
        _searchBox.BorderThickness = new Thickness(1);
        _searchBox.SetResourceReference(BackgroundProperty, "DSBackgroundPanel");
        _searchBox.SetResourceReference(BorderBrushProperty, "DSBackgroundHeader");
        _searchBox.SetResourceReference(ForegroundProperty, "DSTextPrimary");
        _searchPlaceholder.Margin = new Thickness(32, 0, 32, 0);
        _searchPlaceholder.VerticalAlignment = VerticalAlignment.Center;
        _searchPlaceholder.SetResourceReference(ForegroundProperty, "DSTextPlaceholder");
        _clearSearchButton.Content = CreateClearSearchIcon();
        _clearSearchButton.Width = 32;
        _clearSearchButton.Height = 32;
        _clearSearchButton.HorizontalAlignment = HorizontalAlignment.Right;
        _clearSearchButton.VerticalAlignment = VerticalAlignment.Center;
        _clearSearchButton.ToolTip = "Clear search";
        _clearSearchButton.BorderThickness = new Thickness(0);
        _clearSearchButton.SetResourceReference(BackgroundProperty, "DSBackgroundPanel");
        _clearSearchButton.SetResourceReference(ForegroundProperty, "DSTextSecondary");
        _clearSearchButton.Click += (_, _) => _searchBox.Clear();

        var search = new Grid();
        search.Children.Add(_searchBox);
        search.Children.Add(CreateSearchIcon());
        search.Children.Add(_searchPlaceholder);
        search.Children.Add(_clearSearchButton);
        search.Width = 240;
        search.MaxWidth = 240;
        search.Height = 32;
        search.HorizontalAlignment = HorizontalAlignment.Left;
        search.VerticalAlignment = VerticalAlignment.Center;
        _searchBox.TextChanged += (_, _) =>
        {
            var hasQuery = _searchBox.Text.Length > 0;
            UpdateSearchPlaceholder();
            _clearSearchButton.Visibility = hasQuery ? Visibility.Visible : Visibility.Collapsed;
            ApplySearchFilter();
        };
        _searchBox.GotKeyboardFocus += (_, _) => UpdateSearchPlaceholder();
        _searchBox.LostKeyboardFocus += (_, _) => UpdateSearchPlaceholder();
        _clearSearchButton.Visibility = Visibility.Collapsed;
        return search;
    }

    private UIElement BuildMiddleZone()
    {
        return BuildPluginList();
    }

    private void UpdateSearchPlaceholder()
    {
        _searchPlaceholder.Visibility = PluginManagerSearchState.ShouldShowPlaceholder(
            _searchBox.Text,
            _searchBox.IsKeyboardFocusWithin)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private UIElement BuildPluginList()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 580,
            Content = _rows
        };
        var listSurface = new Border
        {
            CornerRadius = new CornerRadius(SurfaceCornerRadius),
            BorderThickness = new Thickness(1),
            Child = scroll
        };
        listSurface.SetResourceReference(BackgroundProperty, "DSBackgroundApp");
        listSurface.SetResourceReference(BorderBrushProperty, "DSBorder");
        return listSurface;
    }

    private UIElement BuildFooter()
    {
        _checkUpdatesButton = CreatePrimaryButton("Check for updates", CheckUpdates, 152);
        _checkUpdatesButton.Margin = new Thickness(0);
        _checkUpdatesButton.HorizontalAlignment = HorizontalAlignment.Right;

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _feedUpdateIndicator.Margin = new Thickness(0, 0, 12, 0);
        _feedUpdateIndicator.SetResourceReference(ForegroundProperty, "DSTextSecondary");
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(_feedUpdateIndicator);
        actions.Children.Add(_checkUpdatesButton);
        Grid.SetColumn(actions, 1);
        content.Children.Add(actions);

        var refreshSurface = new Border
        {
            Height = 72,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 0, 24, 0),
            Child = content
        };
        refreshSurface.SetResourceReference(BackgroundProperty, "DSBackgroundHeader");
        refreshSurface.SetResourceReference(BorderBrushProperty, "DSBorder");
        return refreshSurface;
    }

    private static string GetRevitMajorVersion(ExternalCommandData commandData)
    {
        var version = commandData?.Application?.Application?.VersionNumber ?? string.Empty;
        return version.Length >= 4 ? version.Substring(0, 4) : string.Empty;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        Loaded -= OnLoaded;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_isClosed)
                return;

            LoadCachedRows();
            if (_availablePackages.Count == 0)
                CheckUpdates();
            else
                _isChecking = false;
            ShowConventionalInstallWarnings();
        }), DispatcherPriority.Background);
    }

    private void ShowConventionalInstallWarnings()
    {
        var addinsDirectories = PluginAssemblyPathLoader.GetConventionalAddinsDirectories(_revitVersion);
        var warnings = new List<string>();
        foreach (var plugin in _allStatuses
                     .Select(status => status.Plugin)
                     .GroupBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            foreach (var match in PluginAssemblyPathLoader.FindConventionalInstalls(
                         addinsDirectories,
                         plugin.MainAssembly))
            {
                if (IsCurrentDevLoaderAssembly(plugin.PluginId, match.AssemblyPath))
                    continue;
                if (!_shownConventionalWarnings.Add(plugin.PluginId + "|" + match.AddinPath))
                    continue;

                _logger.Warn($"Conventional add-in detected. PluginId='{plugin.PluginId}'. AddinPath='{match.AddinPath}'. AssemblyPath='{match.AssemblyPath}'. DevLoader actions remain available.");
                warnings.Add($"{plugin.DisplayName}: {match.AddinPath}");
            }
        }

        if (warnings.Count == 0)
            return;

        MessageBox.Show(
            this,
            "A conventional plugin installation was found:\n\n" +
            string.Join("\n", warnings.Distinct(StringComparer.OrdinalIgnoreCase)) +
            "\n\nDevLoader will continue. Disable one .addin manifest and restart Revit to avoid loading another copy of the assembly.",
            "DevLoader",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private bool IsCurrentDevLoaderAssembly(string pluginId, string assemblyPath)
    {
        if (!_registry.Exists(pluginId))
            return false;

        try
        {
            var resolvedAssemblyPath = Path.GetFullPath(assemblyPath);
            return _registry.Load(pluginId).Versions.Any(version => string.Equals(
                Path.GetFullPath(version.AssemblyPath),
                resolvedAssemblyPath,
                StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception)
        {
            _logger.Error($"Failed to inspect installed assembly for plugin '{pluginId}'.", exception);
            return false;
        }
    }

    private static IReadOnlyList<DevPluginStatus> AllowConventionalInstallActions(
        IReadOnlyList<DevPluginStatus>? statuses)
    {
        if (statuses is null)
            return Array.Empty<DevPluginStatus>();

        return statuses.Select(status =>
        {
            var warningIndex = status.Details.IndexOf(
                DevPluginStatusService.ConventionalInstallWarningPrefix,
                StringComparison.Ordinal);
            if (warningIndex < 0)
                return status;

            return new DevPluginStatus(
                status.Plugin,
                status.RevitVersion,
                status.Installed,
                status.Available,
                status.Kind,
                status.Details.Substring(0, warningIndex).Trim());
        }).ToList();
    }
}
