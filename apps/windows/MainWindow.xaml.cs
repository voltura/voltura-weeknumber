using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Updates;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;

namespace VolturaWeekNumber;

public enum MainPage
{
    WeekNumber,
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

    public MainWindow(CalendarViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        WindowWorkAreaPlacement.ConstrainAndCenterOnFirstLoad(this);
        WindowWorkAreaPlacement.KeepVisibleAfterDisplayChanges(this);
        Closing += OnClosing;
        Tabs.SelectionChanged += OnPageChanged;
        AddHandler(Expander.ExpandedEvent, new RoutedEventHandler(OnExpanded));
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
        FocusDatePageHeader();
    }

    private void OnPageChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.OriginalSource == Tabs)
        {
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

    // This checkbox saves immediately; do not accept edits while its save action is gated.
    internal void SetActionsBusy(bool busy) => AutomaticUpdateCheck.IsEnabled = !busy;

    internal void UpdateState(UpdateState state, bool eligible)
    {
        UpdateStatus.Text = Strings.Current[state.Status.ToString()];
        AutomaticUpdateCheck.Visibility = eligible
            ? Visibility.Visible
            : Visibility.Collapsed;
        CheckUpdateButton.Content = Strings.Current[eligible
            ? "CheckUpdates"
            : "Downloads"];
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

    internal void PrepareReview() => PreferencesScroll.ScrollToHome();

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (_exit)
        {
            return;
        }

        args.Cancel = true;
        Hide();
        HiddenToTray?.Invoke();
    }

    private void TodayClick(object sender, RoutedEventArgs args) =>
        ((CalendarViewModel)DataContext).Today();

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
}
