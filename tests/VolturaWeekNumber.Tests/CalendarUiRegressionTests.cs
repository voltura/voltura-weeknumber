using System.Globalization;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Ui;
using Xunit;
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
                model.DayOfYear.SelectedDate = new DateTime(2025, 1, 1);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal("001", model.DayOfYear.Result);
                Assert.Equal("001", model.DayOfYear.NumberInput);
            }
            finally
            {
                window.Exit();
            }
        });
    }
}
