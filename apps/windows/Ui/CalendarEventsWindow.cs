using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Platform;

namespace VolturaWeekNumber.Ui;

internal sealed class CalendarEventsWindow : Window
{
    private bool _exit;
    private readonly StackPanel _events = new();
    private const int PageSize = 10;
    private IReadOnlyList<ImportedOccurrence> _occurrences = [];
    private int _page;
    private readonly StackPanel _paging = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _pageLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private readonly Button _previous = new() { Content = "‹", Width = 40, MinWidth = 0 };
    private readonly Button _next = new() { Content = "›", Width = 40, MinWidth = 0 };
    private readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _heading = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Focusable = true, FocusVisualStyle = null };
    internal DateOnly? Date { get; private set; }
    internal Button CloseButton { get; } = new() { Width = 28, Height = 28, MinHeight = 28, MinWidth = 0, Background = System.Windows.Media.Brushes.Transparent, BorderBrush = System.Windows.Media.Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };

    internal CalendarEventsWindow(TrayCalendarWindow calendar)
    {
        Owner = calendar;
        Width = 360;
        Height = 456;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SetResourceReference(BackgroundProperty, "WindowBrush");

        var panel = new DockPanel();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };

        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        KeyboardNavigation.SetIsTabStop(_heading, false);
        header.Children.Add(_heading);

        var closeGlyph = new TextBlock { Text = "\uE8BB" };

        closeGlyph.SetResourceReference(StyleProperty, "XGlyph");
        CloseButton.Content = closeGlyph;
        CloseButton.SetBinding(ToolTipProperty, new System.Windows.Data.Binding("[Close]") { Source = Strings.Current });
        CloseButton.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty, new System.Windows.Data.Binding("[Close]") { Source = Strings.Current });
        CloseButton.Click += (_, _) =>
        {
            using var interaction = calendar.BeginCalendarInteraction();

            Hide();
            calendar.Activate();
        };

        Grid.SetColumn(CloseButton, 1);
        header.Children.Add(CloseButton);
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        var manageContent = new SpacingStackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var manageGlyph = new TextBlock { Text = "\uE787", FontSize = 14 };

        manageGlyph.SetResourceReference(StyleProperty, "ButtonGlyph");
        manageContent.Children.Add(manageGlyph);
        manageContent.Children.Add(new TextBlock { Text = Strings.Current["ImportedCalendars"], VerticalAlignment = VerticalAlignment.Center });

        var manage = new Button { Content = manageContent, Width = double.NaN, MinWidth = 0, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };

        System.Windows.Automation.AutomationProperties.SetName(manage, Strings.Current["ImportedCalendars"]);

        manage.Click += async (_, _) => await CalendarImportActions.ManageAsync(this);
        DockPanel.SetDock(manage, Dock.Bottom);
        panel.Children.Add(manage);
        _previous.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty, new System.Windows.Data.Binding("[PreviousPage]") { Source = Strings.Current });
        _next.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty, new System.Windows.Data.Binding("[NextPage]") { Source = Strings.Current });
        _previous.Click += (_, _) =>
        {
            _page--;
            RenderPage();
        };

        _next.Click += (_, _) =>
        {
            _page++;
            RenderPage();
        };

        _paging.Children.Add(_previous);
        _paging.Children.Add(_pageLabel);
        _paging.Children.Add(_next);
        DockPanel.SetDock(_paging, Dock.Bottom);
        panel.Children.Add(_paging);
        _scroll.Content = _events;
        panel.Children.Add(_scroll);

        var border = new Border { Child = panel, BorderThickness = new Thickness(1), Padding = new Thickness(16) };

        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Content = border;
        WindowCorners.Apply(this, border);
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                _events.Children.Clear();
                _occurrences = [];
            }
        };

        Deactivated += (_, _) => calendar.QueueGroupDismissal();
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                calendar.Hide();
                args.Handled = true;
            }
        };
    }

    internal void ShowDetails()
    {
        var reopening = !IsVisible;

        if (reopening)
        {
            FocusManager.SetFocusedElement(this, _heading);
        }

        Show();

        if (reopening)
        {
            _heading.Focus();
        }
    }

    internal void Display(DateOnly date, IReadOnlyList<ImportedOccurrence> occurrences)
    {
        _page = Date == date
            ? Math.Min(_page, Math.Max(0, (occurrences.Count - 1) / PageSize))
            : 0;
        _occurrences = occurrences;
        Date = date;
        _heading.Text = date.ToString("D", Strings.Current.Culture);
        Title = _heading.Text;
        RenderPage();
    }

    private void RenderPage()
    {
        _events.Children.Clear();
        _scroll.ScrollToTop();
        _paging.Visibility = _occurrences.Count > PageSize
            ? Visibility.Visible
            : Visibility.Collapsed;
        _previous.IsEnabled = _page > 0;
        _next.IsEnabled = (_page + 1) * PageSize < _occurrences.Count;
        _pageLabel.Text = $"{_page + 1} / {Math.Max(1, (_occurrences.Count + PageSize - 1) / PageSize)}";

        foreach (var occurrence in _occurrences.Skip(_page * PageSize).Take(PageSize))
        {
            var entry = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };

            void Text(string value, bool strong = false)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                entry.Children.Add(new TextBlock
                {
                    Text = value,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = strong
                        ? FontWeights.SemiBold
                        : FontWeights.Normal,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }

            Text(string.IsNullOrWhiteSpace(occurrence.Title)
                ? Strings.Current["UntitledEvent"]
                : occurrence.Title, true);

            var end = occurrence.AllDay && occurrence.End > occurrence.Start
                ? occurrence.End.AddDays(-1)
                : occurrence.End;

            Text(occurrence.AllDay
                ? Strings.Current["AllDayEvent"] + " · " + occurrence.Start.ToString("d", Strings.Current.Culture)
                + (end.Date > occurrence.Start.Date
                    ? " – " + end.ToString("d", Strings.Current.Culture)
                    : "")
                : occurrence.Start.ToString(occurrence.Start.Date == occurrence.End.Date
                    ? "t"
                    : "g", Strings.Current.Culture)
                    + " – " + occurrence.End.ToString(occurrence.Start.Date == occurrence.End.Date
                    ? "t"
                    : "g", Strings.Current.Culture));
            Text(occurrence.Location);
            Text(occurrence.Description);

            var source = new Button { Content = new TextBlock { Text = occurrence.SourceName, TextWrapping = TextWrapping.Wrap }, Width = double.NaN, Height = double.NaN, MinHeight = 40, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 280 };

            source.ToolTip = Strings.Current["ImportedCalendars"];
            source.Click += async (_, _) => await CalendarImportActions.ManageAsync(this, occurrence.SourceId);
            entry.Children.Add(source);
            _events.Children.Add(entry);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exit)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
    internal void Exit()
    {
        _exit = true;
        Close();
    }
}
