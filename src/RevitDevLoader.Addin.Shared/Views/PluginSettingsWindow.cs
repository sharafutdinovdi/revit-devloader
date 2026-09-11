using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitDevLoader.Core;
using RevitDevLoader.Infrastructure;

namespace RevitDevLoader.Views;

internal sealed class PluginSettingsWindow : Window
{
    private readonly string _settingsPath;
    private readonly TextBox _repository = new();
    private readonly TextBox _tag = new();
    private readonly TextBox _asset = new();
    private readonly TextBox _updatesFolder = new();
    private readonly TextBox _retention = new();
    private readonly CheckBox _localFallback = new();
    private DevLoaderSettingsEditorModel _model = new();

    public PluginSettingsWindow(Window owner, string settingsPath)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));

        DSTheme.Apply(this);
        Title = "DevLoader settings";
        Width = 620;
        Height = 520;
        MinWidth = 560;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        SetResourceReference(BackgroundProperty, "DSBackgroundApp");

        Content = BuildLayout();
        LoadValues();
    }

    private UIElement BuildLayout()
    {
        var root = new DockPanel { LastChildFill = true, Margin = new Thickness(20) };
        var footer = BuildFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var fields = new StackPanel();
        fields.Children.Add(CreateHeading("Update source"));
        fields.Children.Add(CreateField("Repository", _repository, "Example: owner/repo"));
        fields.Children.Add(CreateField("Release tag", _tag, "Example: test-feed"));
        fields.Children.Add(CreateField("Asset name", _asset, "Example: feed.json"));
        fields.Children.Add(CreateHeading("Local data"));
        fields.Children.Add(CreateField("Local updates folder", _updatesFolder, string.Empty));
        fields.Children.Add(CreateField("Versions to retain", _retention, "Whole number, at least 1"));

        _localFallback.Content = "Use a local source when the network is unavailable";
        _localFallback.Margin = new Thickness(0, 12, 0, 0);
        _localFallback.SetResourceReference(ForegroundProperty, "DSTextPrimary");
        fields.Children.Add(_localFallback);
        root.Children.Add(fields);
        return root;
    }

    private UIElement BuildFooter()
    {
        var grid = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var openFile = PluginManagerWindow.CreateSecondaryButton("Open settings file", OpenSettingsFile, 188);
        openFile.Margin = new Thickness(0);
        grid.Children.Add(openFile);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = PluginManagerWindow.CreateSecondaryButton("Cancel", () => DialogResult = false, 92);
        cancel.Margin = new Thickness(0);
        actions.Children.Add(cancel);
        var ok = PluginManagerWindow.CreatePrimaryButton("OK", SaveAndClose, 80);
        actions.Children.Add(ok);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        return grid;
    }

    private static TextBlock CreateHeading(string text)
    {
        var heading = new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 8)
        };
        heading.SetResourceReference(ForegroundProperty, "DSTextPrimary");
        return heading;
    }

    private static UIElement CreateField(string label, TextBox textBox, string hint)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(212) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelText = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        labelText.SetResourceReference(ForegroundProperty, "DSTextPrimary");
        grid.Children.Add(labelText);

        textBox.Height = 36;
        textBox.Padding = new Thickness(12, 0, 12, 0);
        textBox.VerticalContentAlignment = VerticalAlignment.Center;
        textBox.ToolTip = hint;
        textBox.SetResourceReference(BackgroundProperty, "DSBackgroundInput");
        textBox.SetResourceReference(BorderBrushProperty, "DSBorderInput");
        textBox.SetResourceReference(ForegroundProperty, "DSTextPrimary");
        Grid.SetColumn(textBox, 1);
        grid.Children.Add(textBox);
        return grid;
    }

    private void LoadValues()
    {
        _model = DevLoaderSettingsEditorStore.Load(_settingsPath);
        _repository.Text = _model.FeedRepository;
        _tag.Text = _model.FeedTag;
        _asset.Text = _model.FeedAsset;
        _updatesFolder.Text = _model.UpdatesFolder;
        _retention.Text = _model.RunRetentionCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _localFallback.IsChecked = _model.UseLocalUpdatesFallback;
    }

    private void SaveAndClose()
    {
        if (!int.TryParse(_retention.Text.Trim(), out var retention) || retention < 1)
        {
            MessageBox.Show(this, "Enter a whole number of versions, at least 1.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            _retention.Focus();
            return;
        }

        try
        {
            DevLoaderSettingsEditorStore.Save(
                _settingsPath,
                new DevLoaderSettingsEditorModel
                {
                    FeedRepository = _repository.Text,
                    FeedTag = _tag.Text,
                    FeedAsset = _asset.Text,
                    UpdatesFolder = _updatesFolder.Text,
                    RunRetentionCount = retention,
                    UseLocalUpdatesFallback = _localFallback.IsChecked == true,
                    UnmanagedFeedUrl = _model.UnmanagedFeedUrl
                });
            DialogResult = true;
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "Failed to save DevLoader settings at {SettingsPath}.", _settingsPath);
            MessageBox.Show(
                this,
                "Could not save settings. Check the repository address, updates folder and write permissions.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenSettingsFile()
    {
        if (!System.IO.File.Exists(_settingsPath))
            DevLoaderSettingsEditorStore.Save(_settingsPath, DevLoaderSettingsEditorStore.Load(_settingsPath));

        using var process = Process.Start(new ProcessStartInfo { FileName = _settingsPath, UseShellExecute = true });
    }
}
