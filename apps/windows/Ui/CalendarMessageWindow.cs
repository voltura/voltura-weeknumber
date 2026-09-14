using System.Windows;
using System.Windows.Controls;

namespace VolturaWeekNumber.Ui;

internal sealed class CalendarMessageWindow : Window
{
    internal Button DismissButton { get; } = new()
    {
        Width = double.NaN,
        MinWidth = 90,
        IsDefault = true,
        IsCancel = true,
        HorizontalAlignment = HorizontalAlignment.Right,
    };

    internal CalendarMessageWindow(string message, bool confirmation = false)
    {
        Title = confirmation
            ? Strings.Current["RemoveCalendar"]
            : "Voltura WeekNumber";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "WindowBrush");

        var content = new StackPanel();

        content.Children.Add(new TextBlock { Text = Title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        SetButtonContent(DismissButton, "Close", "\uE8BB");
        DismissButton.Click += (_, _) => Close();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
        };

        actions.Children.Add(DismissButton);

        if (confirmation)
        {
            var remove = new Button
            {
                Width = double.NaN,
                MinWidth = 90,
                Margin = new Thickness(8, 0, 0, 0),
            };

            SetButtonContent(remove, "RemoveCalendar", "\uE74D");
            remove.Click += (_, _) => DialogResult = true;
            actions.Children.Add(remove);
        }

        content.Children.Add(actions);

        var border = new Border { Child = content, Padding = new Thickness(20), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };

        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Content = border;
    }

    private static void SetButtonContent(Button button, string key, string glyphText)
    {
        var content = new SpacingStackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var glyph = new TextBlock { Text = glyphText };
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

        glyph.SetResourceReference(StyleProperty, glyphText == "\uE8BB"
            ? "XGlyph"
            : "ButtonGlyph");

        if (glyphText != "\uE8BB")
        {
            glyph.FontSize = 14;
        }

        label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding($"[{key}]") { Source = Strings.Current });
        button.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty, new System.Windows.Data.Binding($"[{key}]") { Source = Strings.Current });
        content.Children.Add(glyph);
        content.Children.Add(label);
        button.Content = content;
    }

    internal static void ShowMessage(Window owner, string message)
    {
        using var interaction = CalendarImportActions.Group(owner)?.BeginCalendarInteraction();
        var dialog = new CalendarMessageWindow(message) { Owner = owner, Topmost = owner.Topmost };

        dialog.ShowDialog();
    }

    internal static bool ConfirmRemoval(Window owner, string message)
    {
        using var interaction = CalendarImportActions.Group(owner)?.BeginCalendarInteraction();
        var dialog = new CalendarMessageWindow(message, confirmation: true) { Owner = owner, Topmost = owner.Topmost };

        return dialog.ShowDialog() == true;
    }
}
