using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Ui;
using Xunit;
using CalendarMode = VolturaWeekNumber.Features.Calendar.CalendarMode;
using DatePicker = System.Windows.Controls.DatePicker;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class CalendarUiRegressionTests(WpfTestFixture fixture)
{
    [Fact]
    public void DefaultWeekSelectionAdvancesWithTodayAndExplicitSelectionStaysFixed()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();

            model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });

            var tomorrow = DateTime.Today.AddDays(1);

            model.Refresh(tomorrow);
            Assert.Equal(tomorrow, model.SelectedDate);

            model.SelectedDate = new DateTime(2024, 12, 31);
            model.Refresh(tomorrow.AddDays(1));
            Assert.Equal(new DateTime(2024, 12, 31), model.SelectedDate);
            model.Today();
            model.Refresh(tomorrow);
            Assert.Equal(tomorrow, model.SelectedDate);
        });
    }

    [Fact]
    public void BoundDatePickersKeepFollowingTodayAcrossMultipleRefreshes()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();

            model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });

            var window = new MainWindow(model);

            try
            {
                window.Open();

                var tomorrow = DateTime.Today.AddDays(1);

                for (var offset = 0; offset < 2; offset++)
                {
                    var date = tomorrow.AddDays(offset);

                    model.Refresh(date);
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(date, model.SelectedDate);
                    Assert.Equal(
                        date,
                        Assert.IsType<DatePicker>(window.FindName("DateInput")).SelectedDate
                    );
                    window.Open(MainPage.DayOfYear);
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(date, model.DayOfYear.SelectedDate);
                    Assert.Equal(model.DayOfYear.Result, model.DayOfYear.NumberInput);
                }
            }
            finally
            {
                window.Exit();
            }
        });
    }

    [Fact]
    public void ClearedOrUnsupportedDateDoesNotKeepThePreviousIsoYear()
    {
        fixture.Run(() =>
        {
            var previous = CultureInfo.CurrentCulture;

            try
            {
                var model = new CalendarViewModel();

                model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });
                model.SelectedDate = new DateTime(2021, 1, 1);
                Assert.NotEmpty(model.WeekYearText);
                model.SelectedDate = null;
                Assert.Empty(model.WeekYearText);

                model.SelectedDate = new DateTime(2021, 1, 1);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                model.Apply(new AppSettings());
                model.SelectedDate = new DateTime(1800, 1, 1);
                Assert.Empty(model.WeekYearText);
                Assert.Null(model.Preview);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        });
    }

    [Fact]
    public void BoundOrdinalInputsStayConsistentWhenLeavingALeapYear()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();

            model.DayOfYear.SelectedDate = new DateTime(2024, 12, 31);

            var window = new MainWindow(model);

            try
            {
                window.Open(MainPage.DayOfYear);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal("366", model.DayOfYear.NumberInput);

                var tabs = Assert.IsType<TabControl>(window.FindName("Tabs"));
                var page = Assert.IsType<DateLookupPage>(
                    Assert.IsType<TabItem>(tabs.SelectedItem).Content
                );
                var year = Assert.IsType<TextBox>(page.FindName("YearInput"));
                var number = Assert.IsType<TextBox>(page.FindName("NumberInput"));

                foreach (var date in new[]
                {
                    new DateTime(2025, 1, 1),
                    new DateTime(2024, 12, 31),
                    new DateTime(2025, 12, 31),
                    new DateTime(2028, 12, 31),
                    new DateTime(2029, 1, 1),
                })
                {
                    model.DayOfYear.SelectedDate = date;
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                    var expected = date.DayOfYear.ToString("D3", CultureInfo.InvariantCulture);

                    Assert.Equal(expected, model.DayOfYear.Result);
                    Assert.Equal(expected, model.DayOfYear.NumberInput);
                    Assert.Equal(expected, number.Text);
                    Assert.Equal(date.Year.ToString(CultureInfo.InvariantCulture), year.Text);
                    model.DayOfYear.Convert();
                    Assert.Equal(date, model.DayOfYear.SelectedDate);
                }
            }
            finally
            {
                window.Exit();
            }
        });
    }

    [Fact]
    public void WeekLookupIsQuietUntilOpenedAndDoesNotChangeTheSelectedDate()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();

            model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });
            model.SelectedDate = new DateTime(2024, 6, 12);

            var window = new MainWindow(model);

            try
            {
                window.Open();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                var expander = Assert.IsType<Expander>(window.FindName("WeekLookupExpander"));

                Assert.False(expander.IsExpanded);
                expander.IsExpanded = true;
                model.WeekLookup.YearInput = "2026";
                model.WeekLookup.WeekInput = "1";
                Assert.IsType<Button>(window.FindName("FindWeekButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal(new DateTime(2024, 6, 12), model.SelectedDate);
                Assert.Contains("29", model.WeekLookup.ResultText, StringComparison.Ordinal);
                Assert.Contains("2025", model.WeekLookup.ResultText, StringComparison.Ordinal);
                Assert.Contains("2026", model.WeekLookup.ResultText, StringComparison.Ordinal);
                Assert.Empty(model.WeekLookup.ErrorText);
            }
            finally
            {
                window.Exit();
            }
        });
    }
}
