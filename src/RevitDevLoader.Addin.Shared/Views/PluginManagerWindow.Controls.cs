using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using RevitDevLoader.Infrastructure;

namespace RevitDevLoader.Views;

public sealed partial class PluginManagerWindow
{
    private enum ButtonPalette
    {
        Primary,
        Secondary,
        HeaderIcon
    }

    internal static Button CreatePrimaryButton(string text, Action action, double minWidth = 104)
    {
        return CreateButton(text, action, "DSActionPrimaryBg", "DSTextOnBrand", "DSActionPrimaryBg", new Thickness(8, 0, 0, 0), minWidth, palette: ButtonPalette.Primary);
    }

    internal static Button CreateSecondaryButton(string text, Action action, double minWidth = 104)
    {
        return CreateButton(text, action, "DSActionSecondaryBg", "DSTextPrimary", "DSBorder", new Thickness(8, 0, 0, 0), minWidth);
    }

    private static Button CreateHeaderIconButton(UIElement icon, string toolTip, Action action)
    {
        var button = CreateButton(
            string.Empty,
            action,
            "DSBackgroundHeader",
            "DSTextSecondary",
            "DSBackgroundHeader",
            new Thickness(8, 0, 0, 0),
            minWidth: 32,
            height: 32,
            palette: ButtonPalette.HeaderIcon);
        button.Width = 32;
        button.Padding = new Thickness(0);
        button.Background = Brushes.Transparent;
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Content = icon;
        button.ToolTip = toolTip;
        return button;
    }

    private static UIElement CreateSettingsIcon()
    {
        return new Image { Width = 32, Height = 32, Source = RibbonIconFactory.CreateSettingsIcon() };
    }

    private static UIElement CreateJournalIcon()
    {
        return new Image { Width = 32, Height = 32, Source = RibbonIconFactory.CreateJournalIcon() };
    }

    private static UIElement CreateSearchIcon()
    {
        var path = new Path
        {
            Data = Geometry.Parse("M7,2.5 A4.5,4.5 0 1 0 7,11.5 A4.5,4.5 0 1 0 7,2.5 M10.2,10.2 L14,14"),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        path.SetResourceReference(Shape.StrokeProperty, "DSTextSecondary");
        return new Viewbox
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            IsHitTestVisible = false,
            Child = path
        };
    }

    private static UIElement CreateClearSearchIcon()
    {
        var path = new Path
        {
            Data = Geometry.Parse("M3,3 L13,13 M13,3 L3,13"),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        path.SetBinding(Shape.StrokeProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
        });
        return new Viewbox { Width = 12, Height = 12, Child = path };
    }

    private static Button CreateRowActionButton(string text, Action action, bool isEnabled, bool primary)
    {
        var button = primary
            ? CreateButton(text, action, "DSActionPrimaryBg", "DSTextOnBrand", "DSActionPrimaryBg", new Thickness(8, 0, 0, 0), 108, 32, ButtonPalette.Primary)
            : CreateButton(text, action, "DSActionSecondaryBg", "DSTextPrimary", "DSBorder", new Thickness(8, 0, 0, 0), 108, 32);
        button.IsEnabled = isEnabled;
        return button;
    }

    private static Button CreateButton(
        string text,
        Action action,
        string backgroundResourceKey,
        string foregroundResourceKey,
        string borderResourceKey,
        Thickness margin,
        double minWidth = 104,
        double height = 36,
        ButtonPalette palette = ButtonPalette.Secondary)
    {
        var button = new Button
        {
            Content = text,
            Height = height,
            MinWidth = minWidth,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = margin,
            FontSize = 13,
            FontWeight = FontWeights.Normal,
            Template = CreateButtonTemplate(palette)
        };
        button.SetResourceReference(Control.BackgroundProperty, backgroundResourceKey);
        button.SetResourceReference(Control.ForegroundProperty, foregroundResourceKey);
        button.SetResourceReference(Control.BorderBrushProperty, borderResourceKey);
        button.Click += (_, _) => action();
        return button;
    }

    private static ControlTemplate CreateButtonTemplate(ButtonPalette palette)
    {
        var template = new ControlTemplate(typeof(Button));
        var root = new FrameworkElementFactory(typeof(Border));
        root.Name = "Root";
        root.SetValue(
            Border.CornerRadiusProperty,
            palette == ButtonPalette.HeaderIcon ? new CornerRadius(7) : new CornerRadius(SurfaceCornerRadius));
        root.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        root.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
        root.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
        root.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(presenter);
        template.VisualTree = root;

        switch (palette)
        {
            case ButtonPalette.Primary:
                template.Triggers.Add(CreateTemplateTrigger(UIElement.IsMouseOverProperty, true, "DSFooterSecondaryHover", "DSFooterSecondaryBorder"));
                template.Triggers.Add(CreateTemplateTrigger(ButtonBase.IsPressedProperty, true, "DSFooterSecondaryPressed", "DSBrandFooterBorder"));
                break;
            case ButtonPalette.HeaderIcon:
                template.Triggers.Add(CreateTemplateTrigger(UIElement.IsKeyboardFocusedProperty, true, "DSBackgroundHeader", "DSBackgroundHeader"));
                template.Triggers.Add(CreateTemplateTrigger(UIElement.IsMouseOverProperty, true, "DSBackgroundHeader", "DSBackgroundHeader"));
                template.Triggers.Add(CreateTemplateTrigger(ButtonBase.IsPressedProperty, true, "DSBorder", "DSBorder"));
                break;
            default:
                template.Triggers.Add(CreateTemplateTrigger(UIElement.IsMouseOverProperty, true, "DSActionSecondaryHover", "DSBorder", "DSTextPrimary"));
                template.Triggers.Add(CreateTemplateTrigger(ButtonBase.IsPressedProperty, true, "DSActionSecondaryPressed", "DSBorder", "DSTextPrimary"));
                break;
        }

        if (palette != ButtonPalette.HeaderIcon)
        {
            var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(CreateResourceSetter(Border.BorderBrushProperty, "DSBorderFocus", "Root"));
            template.Triggers.Add(focused);
        }

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        template.Triggers.Add(disabled);
        return template;
    }

    private static ControlTemplate CreateSearchBoxTemplate()
    {
        var template = new ControlTemplate(typeof(TextBox));
        var root = new FrameworkElementFactory(typeof(Border));
        root.Name = "Root";
        root.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        root.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        root.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
        root.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
        root.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

        var contentHost = new FrameworkElementFactory(typeof(ScrollViewer));
        contentHost.Name = "PART_ContentHost";
        root.AppendChild(contentHost);
        template.VisualTree = root;

        var focused = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focused.Setters.Add(CreateResourceSetter(Border.BorderBrushProperty, "DSBorderFocus", "Root"));
        template.Triggers.Add(focused);
        return template;
    }

    private static Trigger CreateTemplateTrigger(
        DependencyProperty property,
        object value,
        string backgroundResourceKey,
        string borderResourceKey,
        string? foregroundResourceKey = null)
    {
        var trigger = new Trigger { Property = property, Value = value };
        trigger.Setters.Add(CreateResourceSetter(Border.BackgroundProperty, backgroundResourceKey, "Root"));
        trigger.Setters.Add(CreateResourceSetter(Border.BorderBrushProperty, borderResourceKey, "Root"));
        if (foregroundResourceKey is not null)
            trigger.Setters.Add(CreateResourceSetter(Control.ForegroundProperty, foregroundResourceKey));
        return trigger;
    }

    private static Setter CreateResourceSetter(DependencyProperty property, string resourceKey, string? targetName = null)
    {
        var setter = new Setter
        {
            Property = property,
            Value = new DynamicResourceExtension(resourceKey)
        };
        if (targetName is not null)
            setter.TargetName = targetName;
        return setter;
    }

}
