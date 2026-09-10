using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class DateSpanUiTests(WpfTestFixture fixture)
{
    [Fact]
    public void DateSpanPageIsLiveAccessibleAndUsesTheExpectedNavigationSlot()
    {
        fixture.Run(() =>
        {
            Strings.Current.SetLanguage("en");

            var model = new CalendarViewModel();
            var window = new MainWindow(model);

            try
            {
                window.Open(MainPage.DateSpan);
                Dispatcher.CurrentDispatcher.Invoke(
                    () => { },
                    DispatcherPriority.ApplicationIdle
                );

                var tabs = Assert.IsType<TabControl>(window.FindName("Tabs"));
                var page = Assert.IsType<DateSpanPage>(window.FindName("DateSpanPage"));

                Assert.Equal((int)MainPage.DateSpan, tabs.SelectedIndex);
                Assert.Equal(800, window.Width);
                Assert.Same(model.DateSpan, page.DataContext);

                model.DateSpan.FirstDate = new DateTime(2026, 9, 7);
                model.DateSpan.SecondDate = new DateTime(2026, 9, 14);
                Dispatcher.CurrentDispatcher.Invoke(
                    () => { },
                    DispatcherPriority.ApplicationIdle
                );

                Assert.Equal("7", Assert.IsType<TextBlock>(page.FindName("CalendarDaysResult")).Text);
                Assert.Equal("5", Assert.IsType<TextBlock>(page.FindName("WeekdaysResult")).Text);
                Assert.Equal("2", Assert.IsType<TextBlock>(page.FindName("WeekendDaysResult")).Text);
                Assert.Equal("1", Assert.IsType<TextBlock>(page.FindName("WholeWeeksResult")).Text);

                var firstDate = Assert.IsType<DatePicker>(page.FindName("FirstDateInput"));
                var secondDate = Assert.IsType<DatePicker>(page.FindName("SecondDateInput"));
                var firstToday = Assert.IsType<Button>(page.FindName("FirstTodayButton"));
                var secondToday = Assert.IsType<Button>(page.FindName("SecondTodayButton"));

                Assert.Equal(Strings.Current["FirstDate"], AutomationProperties.GetName(firstDate));
                Assert.Equal(Strings.Current["SecondDate"], AutomationProperties.GetName(secondDate));
                Assert.Equal(Strings.Current["FirstDateToday"], AutomationProperties.GetName(firstToday));
                Assert.Equal(Strings.Current["SecondDateToday"], AutomationProperties.GetName(secondToday));

                page.Measure(new Size(520, 600));
                page.Arrange(new Rect(0, 0, 520, 600));
                Assert.True(firstDate.ActualWidth > 0);
                Assert.True(secondDate.ActualWidth > 0);
                Assert.Equal(240, firstToday.ActualWidth);
                Assert.Equal(240, secondToday.ActualWidth);
            }
            finally
            {
                window.Exit();
            }
        });
    }
}
