using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;

namespace VolturaWeekNumber;

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
        AddHandler(Expander.ExpandedEvent, new RoutedEventHandler(OnExpanded));
    }
    public void Open(int tab = 0)
    {
        Tabs.SelectedIndex = tab;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        WindowWorkAreaPlacement.EnsureVisibleOnCurrentMonitor(this);
        Activate();
    }
    public void UpdateLanguage() => Language = XmlLanguage.GetLanguage(Strings.Current.Culture.Name);
    public void UpdateState(string status, bool ready, bool eligible)
    {
        UpdateStatus.Text = Strings.Current[status];
        AutomaticUpdateCheck.Visibility = eligible ? Visibility.Visible : Visibility.Collapsed;
        CheckUpdateButton.Content = Strings.Current[eligible ? "CheckUpdates" : "Downloads"];
        InstallButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
    }
    public void Exit() { _exit = true; Close(); }
    internal void PrepareReview() => PreferencesScroll.ScrollToHome();
    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (_exit) return;
        args.Cancel = true;
        Hide();
        HiddenToTray?.Invoke();
    }
    private void TodayClick(object sender, RoutedEventArgs args) => ((CalendarViewModel)DataContext).SelectedDate = DateTime.Today;
    private void ActionClick(object sender, RoutedEventArgs args) { if (sender is Button { Tag: string action }) ActionRequested?.Invoke(action); }
    private void AutomaticUpdatesChanged(object sender, RoutedEventArgs args)
    {
        if (IsLoaded) ActionRequested?.Invoke("auto-updates");
    }
    private void OnExpanded(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not Expander expanded || expanded.Parent is not Panel parent) return;
        foreach (var sibling in parent.Children.OfType<Expander>()) if (sibling != expanded) sibling.IsExpanded = false;
    }
}
