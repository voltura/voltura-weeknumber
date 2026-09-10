using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Updates;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;

namespace VolturaWeekNumber;

public enum MainPage
{
    WeekNumber,
    Calendar,
    DayOfYear,
    JulianDay,
    Preferences,
    About,
}

public partial class MainWindow : Window
{
    public event Action<string>? ActionRequested;
    public event Action? HiddenToTray;
    private bool _exit;
    private readonly IClipboardWriter _clipboard;

    public MainWindow(CalendarViewModel model) : this(model, new ClipboardWriter()) { }

    internal MainWindow(CalendarViewModel model, IClipboardWriter clipboard)
    {
        _clipboard = clipboard;
        InitializeComponent();
        DataContext = model;
        CalendarPage.DataContext = model.CalendarBrowser;
        SelectedWeekCopy.CopyRequested += format => CopyWeek(model.CopyText(format));
        LookupWeekCopy.CopyRequested += format => CopyWeek(model.WeekLookup.CopyText(format));
        WindowWorkAreaPlacement.ConstrainAndCenterOnFirstLoad(this);
        WindowWorkAreaPlacement.KeepVisibleAfterDisplayChanges(this);
        Closing += OnClosing;
        Tabs.SelectionChanged += OnPageChanged;
        AddHandler(Expander.ExpandedEvent, new RoutedEventHandler(OnExpanded));
    }

    private void CopyWeek(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        ((CalendarViewModel)DataContext).Status = Strings.Current[
            _clipboard.TryWrite(text)
                ? "Copied"
                : "CopyFailed"];
    }

    public void Open(MainPage tab = MainPage.WeekNumber)
    {
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        WindowWorkAreaPlacement.EnsureVisibleOnCurrentMonitor(this);
        Activate();
        // Showing the window restores focus and can reselect the previously focused tab.
        Tabs.SelectedIndex = (int)tab;
        ((CalendarViewModel)DataContext).CalendarBrowser.SetActive(CalendarTab.IsSelected);
        FocusDatePageHeader();
    }

    private void OnPageChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.OriginalSource == Tabs)
        {
            ((CalendarViewModel)DataContext).CalendarBrowser.SetActive(CalendarTab.IsSelected && IsVisible);
            FocusDatePageHeader();

            if (Tabs.SelectedIndex == (int)MainPage.About)
            {
                ActionRequested?.Invoke("about-update");
            }
        }
    }

    private void FocusDatePageHeader()
    {
        if (Tabs.SelectedIndex is < 0 or > (int)MainPage.JulianDay)
        {
            return;
        }

        var selectedPage = Tabs.SelectedItem as TabItem;
        // Let TabControl finish its automatic content focus before keeping focus on navigation.

        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (IsActive && selectedPage is not null && Tabs.SelectedItem == selectedPage)
                {
                    selectedPage.Focus();
                }
            },
            DispatcherPriority.Loaded
        );
    }

    public void UpdateLanguage()
    {
        Language = XmlLanguage.GetLanguage(Strings.Current.Culture.Name);
        RefreshDateText(this);
    }

    private static void RefreshDateText(DependencyObject parent)
    {
        if (parent is DatePicker picker)
        {
            // WPF updates calendar language, but leaves unchanged dates formatted in the old language.
            picker.SetCurrentValue(DatePicker.TextProperty,
                picker.SelectedDate?.ToString("d", Strings.Current.Culture) ?? string.Empty);

            return;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            RefreshDateText(child);
        }
    }

    // These toggles save immediately; do not accept edits while their save action is gated.
    internal void SetActionsBusy(bool busy)
    {
        AutomaticUpdateCheck.IsEnabled = !busy;
        AlwaysOnTopCheck.IsEnabled = !busy;

        if (Tabs.Template.FindName("HeaderPinToggle", Tabs) is ToggleButton pin)
        {
            pin.IsEnabled = !busy;
        }
    }

    internal void ApplyAlwaysOnTop(bool enabled) => Topmost = enabled;

    internal void UpdateState(UpdateState state, bool eligible)
    {
        UpdateStatus.Text = Strings.Current[state.Status.ToString()];
        AutomaticUpdateCheck.Visibility = eligible
            ? Visibility.Visible
            : Visibility.Collapsed;

        var checkUpdateLabel = Strings.Current[eligible
            ? "CheckUpdates"
            : "Downloads"];

        CheckUpdateLabel.Text = checkUpdateLabel;
        CheckUpdateGlyph.Text = eligible
            ? "\uE72C"
            : "\uE896";
        System.Windows.Automation.AutomationProperties.SetName(
            CheckUpdateButton,
            checkUpdateLabel
        );
        CheckUpdateButton.IsEnabled = !state.Busy;
        InstallButton.Visibility = state.Ready
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void Exit()
    {
        _exit = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (_exit)
        {
            return;
        }

        args.Cancel = true;
        ((CalendarViewModel)DataContext).CalendarBrowser.SetActive(false);
        Hide();
        HiddenToTray?.Invoke();
    }

    private void TodayClick(object sender, RoutedEventArgs args) =>
        ((CalendarViewModel)DataContext).Today();

    private void PreviousWeekClick(object sender, RoutedEventArgs args) =>
        ((CalendarViewModel)DataContext).PreviousWeek();

    private void NextWeekClick(object sender, RoutedEventArgs args) =>
        ((CalendarViewModel)DataContext).NextWeek();

    private void ShowOffsetDateClick(object sender, RoutedEventArgs args) =>
        ((CalendarViewModel)DataContext).ApplyWeekOffset();

    private static bool ValidWeekOffsetInput(TextBox input, string text)
    {
        var candidate = input
            .Text.Remove(input.SelectionStart, input.SelectionLength)
            .Insert(input.SelectionStart, text);

        if (candidate.Length == 0 || candidate == "-")
        {
            return true;
        }

        return candidate.Length <= 7
            && (candidate[0] == '-'
                ? candidate.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0
                : candidate.AsSpan().IndexOfAnyExceptInRange('0', '9') < 0);
    }

    private void WeekOffsetTextInput(object sender, TextCompositionEventArgs args) =>
        args.Handled = !ValidWeekOffsetInput((TextBox)sender, args.Text);

    private void WeekOffsetKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            ((CalendarViewModel)DataContext).ApplyWeekOffset();
            args.Handled = true;
        }
        else if (args.Key == Key.Space)
        {
            args.Handled = true;
        }
    }

    private void WeekOffsetPasting(object sender, DataObjectPastingEventArgs args)
    {
        if (
            args.DataObject.GetData(DataFormats.UnicodeText) is not string text
            || !ValidWeekOffsetInput((TextBox)sender, text)
        )
        {
            args.CancelCommand();
        }
    }

    private void FindWeekClick(object sender, RoutedEventArgs args) =>
        ((CalendarViewModel)DataContext).WeekLookup.Find();

    private void WeekLookupExpanded(object sender, RoutedEventArgs args) =>
        ((CalendarViewModel)DataContext).WeekLookup.Refresh(DateTime.Today);

    private bool ValidWeekLookupInput(TextBox input, string text)
    {
        var candidate = input
            .Text.Remove(input.SelectionStart, input.SelectionLength)
            .Insert(input.SelectionStart, text);

        if (candidate.Length == 0)
        {
            return true;
        }

        var maximum = input == WeekYearInput
            ? 9999
            : 56;

        return candidate.All(character => character is >= '0' and <= '9')
            && int.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value is >= 1
            && value <= maximum;
    }

    private void WeekLookupTextInput(object sender, TextCompositionEventArgs args) =>
        args.Handled = !ValidWeekLookupInput((TextBox)sender, args.Text);

    private void WeekLookupKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Space)
        {
            args.Handled = true;
        }
    }

    private void WeekLookupPasting(object sender, DataObjectPastingEventArgs args)
    {
        if (
            args.DataObject.GetData(DataFormats.UnicodeText) is not string text
            || !ValidWeekLookupInput((TextBox)sender, text)
        )
        {
            args.CancelCommand();
        }
    }

    private void ActionClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string action })
        {
            ActionRequested?.Invoke(action);
        }
    }

    private void AutomaticUpdatesChanged(object sender, RoutedEventArgs args)
    {
        if (IsLoaded)
        {
            ActionRequested?.Invoke("auto-updates");
        }
    }

    private void AlwaysOnTopClick(object sender, RoutedEventArgs args)
    {
        ApplyAlwaysOnTop(((CalendarViewModel)DataContext).Editor.AlwaysOnTop);
        ActionRequested?.Invoke("always-on-top");
    }

    private void OnExpanded(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not Expander expanded || expanded.Parent is not Panel parent)
        {
            return;
        }

        foreach (var sibling in parent.Children.OfType<Expander>())
        {
            if (sibling != expanded)
            {
                sibling.IsExpanded = false;
            }
        }
    }

    private void ExpanderHeaderMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs args
    )
    {
        if (
            sender is not Expander expander
            || expander.Template.FindName("ToggleButtonBorder", expander)
                is not FrameworkElement headerBackground
            || expander.Template.FindName("HeaderSite", expander) is not ToggleButton headerToggle
        )
        {
            return;
        }

        var position = args.GetPosition(headerBackground);

        if (
            position.X < 0
            || position.X > headerBackground.ActualWidth
            || position.Y < 0
            || position.Y > headerBackground.ActualHeight
        )
        {
            return;
        }

        _ = headerToggle.Focus();
        expander.IsExpanded = !expander.IsExpanded;
        args.Handled = true;
    }
}
