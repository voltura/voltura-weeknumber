using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace VolturaWeekNumber.Ui;

public partial class CalendarBrowserPage : System.Windows.Controls.UserControl
{
    public static IValueConverter HiddenWhenTrue { get; } = new InvertedVisibilityConverter();
    public static readonly DependencyProperty MonthColumnsProperty = DependencyProperty.Register(
        nameof(MonthColumns), typeof(int), typeof(CalendarBrowserPage), new PropertyMetadata(3));
    public int MonthColumns
    {
        get => (int)GetValue(MonthColumnsProperty);
        set => SetValue(MonthColumnsProperty, value);
    }
    private CalendarBrowserViewModel Model => (CalendarBrowserViewModel)DataContext;

    public CalendarBrowserPage() => InitializeComponent();

    private void PageSizeChanged(object sender, SizeChangedEventArgs args) =>
        MonthColumns = ActualWidth >= 620
            ? 3
            : 2;

    private void OpenMonthClick(object sender, RoutedEventArgs args)
    {
        Model.ShowMonth(((CalendarMonthItem)((Button)sender).DataContext).Date);
        FocusNavigation();
    }
    private void OpenWeekClick(object sender, RoutedEventArgs args)
    {
        Model.ShowWeek((CalendarWeekItem)((Button)sender).DataContext);
        FocusNavigation();
    }
    private void YearClick(object sender, RoutedEventArgs args)
    {
        Model.ZoomOut(CalendarZoom.Year);
        FocusNavigation();
    }
    private void MonthClick(object sender, RoutedEventArgs args)
    {
        Model.ZoomOut(CalendarZoom.Month);
        FocusNavigation();
    }
    private void PreviousClick(object sender, RoutedEventArgs args) => Model.Move(-1);
    private void NextClick(object sender, RoutedEventArgs args) => Model.Move(1);
    private void TodayClick(object sender, RoutedEventArgs args) => Model.Today();

    private void FocusNavigation()
    {
        CalendarScroll.ScrollToTop();
        _ = Dispatcher.InvokeAsync(() => TodayButton.Focus(), DispatcherPriority.Loaded);
    }

    private sealed class InvertedVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true
                ? Visibility.Collapsed
                : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
