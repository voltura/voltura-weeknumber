using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using VolturaWeekNumber.Platform;

namespace VolturaWeekNumber.Ui;

public partial class TrayCalendarWindow : Window
{
    private bool _exit;
    private int _exportInteractions;
    internal IDisposable BeginExportInteraction()
    {
        _exportInteractions++;

        return new ExportInteraction(this);
    }

    private sealed class ExportInteraction(TrayCalendarWindow owner) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner._exportInteractions--;

            if (!owner.IsActive)
            {
                owner.DismissOnDeactivate();
            }
        }
    }
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateOnly?), typeof(TrayCalendarWindow));
    public DateOnly? SelectedDate
    {
        get => (DateOnly?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }
    public static IMultiValueConverter DatesMatch { get; } = new DateMatchConverter();
    internal bool IsPinned => CalendarPin.IsChecked == true;

    internal TrayCalendarViewModel Model { get; } = new();
    internal event Action? Dismissed;

    public TrayCalendarWindow()
    {
        InitializeComponent();
        DataContext = Model;
        Deactivated += (_, _) => DismissOnDeactivate();
    }

    internal void Exit()
    {
        _exit = true;
        Close();
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

    internal void DismissOnDeactivate()
    {
        if (IsVisible && !IsPinned && _exportInteractions == 0)
        {
            Dismissed?.Invoke();
            Hide();
        }
    }
    internal void Open(System.Drawing.Rectangle anchor)
    {
        SelectedDate = null;
        Model.Today();
        Language = XmlLanguage.GetLanguage(Strings.Current.Culture.Name);
        TrayCalendarPlacement.Place(this, anchor);
        Show();
        TrayCalendarPlacement.Place(this, anchor);
        Activate();
        HeadingButton.Focus();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (args.LeftButton == MouseButtonState.Pressed && CanDragFrom(args.GetPosition(this)))
        {
            args.Handled = true;
            DragMove();
        }
    }

    internal bool CanDragFrom(Point position)
    {
        var headerBottom = CalendarHeader.TranslatePoint(new Point(0, CalendarHeader.ActualHeight), this).Y;

        if (position.X < 0 || position.X >= ActualWidth || position.Y < 0 || position.Y > headerBottom)
        {
            return false;
        }

        for (var element = InputHitTest(position) as DependencyObject;
            element is not null && element != this;
            element = VisualTreeHelper.GetParent(element))
        {
            if (element is ButtonBase)
            {
                return false;
            }
        }

        return true;
    }

    private void OnKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            Hide();
            args.Handled = true;
        }
    }

    private void HeadingClick(object sender, RoutedEventArgs args)
    {
        Model.ZoomOut();

        if (!Model.CanZoomOut)
        {
            PreviousButton.Focus();
        }
    }
    private void PreviousClick(object sender, RoutedEventArgs args) => Model.Move(-1);
    private void NextClick(object sender, RoutedEventArgs args) => Model.Move(1);
    private void ExportClick(object sender, RoutedEventArgs args) =>
        CalendarExportActions.Export(this, Model.ExportRequest, Model.Month.Options);
    private void TodayClick(object sender, RoutedEventArgs args)
    {
        Model.Today();
        SelectedDate = Model.TodayDate;
        HeadingButton.Focus();
    }
    private void PickerClick(object sender, RoutedEventArgs args)
    {
        Model.Select((CalendarPickerItem)((Button)sender).DataContext);
        HeadingButton.Focus();
    }
    private void DayClick(object sender, RoutedEventArgs args) =>
        SelectedDate = ((CalendarDayItem)((RadioButton)sender).DataContext).Date;

    private sealed class DateMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values.Length == 2 && values[0] is DateOnly date && values[1] is DateOnly selected && date == selected;
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
