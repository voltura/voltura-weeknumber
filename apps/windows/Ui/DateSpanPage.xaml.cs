using System.Windows;
using System.Windows.Controls;

namespace VolturaWeekNumber.Ui;

public partial class DateSpanPage : UserControl
{
    public DateSpanPage() => InitializeComponent();

    private void FirstTodayClick(object sender, RoutedEventArgs args) =>
        ((DateSpanViewModel)DataContext).FirstToday();

    private void SecondTodayClick(object sender, RoutedEventArgs args) =>
        ((DateSpanViewModel)DataContext).SecondToday();
}
