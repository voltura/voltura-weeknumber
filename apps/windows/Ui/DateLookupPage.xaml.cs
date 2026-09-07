using System.Windows;
using System.Windows.Controls;

namespace VolturaWeekNumber.Ui;

public partial class DateLookupPage : UserControl
{
    public DateLookupPage() => InitializeComponent();
    private void TodayClick(object sender, RoutedEventArgs args) => ((DateLookupViewModel)DataContext).Today();
    private void ConvertClick(object sender, RoutedEventArgs args) => ((DateLookupViewModel)DataContext).Convert();
}
