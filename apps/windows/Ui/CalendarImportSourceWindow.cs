using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

internal sealed class CalendarImportSourceWindow : Window
{
    internal string? Source { get; private set; }

    internal CalendarImportSourceWindow()
    {
        Title = Strings.Current["ImportCalendar"].TrimEnd('…', '.');
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "WindowBrush");

        var panel = new StackPanel();

        panel.Children.Add(new TextBlock { Text = Title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });

        var file = ActionButton("CalendarChooseFile", "\uE8E5");

        file.HorizontalAlignment = HorizontalAlignment.Left;
        file.Margin = new Thickness(0, 0, 0, 20);
        file.Click += (_, _) =>
        {
            var picker = new OpenFileDialog { Filter = "iCalendar (*.ics)|*.ics", Multiselect = false, Title = Title };

            if (picker.ShowDialog(this) == true)
            {
                Source = picker.FileName;
                DialogResult = true;
            }
        };

        panel.Children.Add(file);

        var address = new TextBox { MinWidth = 0, MinHeight = 36, MaxLength = 8192, Padding = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Stretch };

        address.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        address.SetResourceReference(ForegroundProperty, "TextBrush");
        address.SetResourceReference(BorderBrushProperty, "BorderBrush");
        System.Windows.Automation.AutomationProperties.SetName(address, Strings.Current["CalendarWebAddress"]);
        panel.Children.Add(new Label { Content = new TextBlock { Text = Strings.Current["CalendarWebAddress"] }, Target = address, Padding = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(address);

        var actions = new SpacingStackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var close = ActionButton("Close", "\uE8BB");

        close.IsCancel = true;
        close.Click += (_, _) => Close();

        var import = ActionButton("CalendarImportUrl", "\uE8B5");

        import.IsDefault = true;
        import.IsEnabled = false;
        address.TextChanged += (_, _) => import.IsEnabled = CalendarUrlImport.TryGetUri(address.Text, out _);
        import.Click += (_, _) =>
        {
            if (CalendarUrlImport.TryGetUri(address.Text, out _))
            {
                Source = address.Text.Trim();
                DialogResult = true;
            }
        };

        actions.Children.Add(close);
        actions.Children.Add(import);
        panel.Children.Add(actions);

        var border = new Border { Child = panel, Padding = new Thickness(20), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };

        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Content = border;
        Loaded += (_, _) => address.Focus();
    }

    private static Button ActionButton(string key, string symbol)
    {
        var content = new SpacingStackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var glyph = new TextBlock { Text = symbol };

        glyph.SetResourceReference(StyleProperty, symbol == "\uE8BB"
            ? "XGlyph"
            : "ButtonGlyph");
        content.Children.Add(glyph);
        content.Children.Add(new TextBlock { Text = Strings.Current[key], VerticalAlignment = VerticalAlignment.Center });

        var button = new Button { Content = content, Width = double.NaN, MinWidth = 0 };

        System.Windows.Automation.AutomationProperties.SetName(button, Strings.Current[key]);

        return button;
    }
}
