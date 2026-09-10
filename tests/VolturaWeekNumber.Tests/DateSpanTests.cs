using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class DateSpanTests
{
    [Theory]
    [InlineData(2026, 9, 10, 2026, 9, 10, 0, 0, 0, 0)]
    [InlineData(2026, 9, 7, 2026, 9, 14, 7, 5, 2, 1)]
    [InlineData(2026, 9, 11, 2026, 9, 14, 3, 1, 2, 0)]
    [InlineData(2026, 9, 12, 2026, 9, 14, 2, 0, 2, 0)]
    [InlineData(2024, 2, 28, 2024, 3, 1, 2, 2, 0, 0)]
    public void CalculatesHalfOpenSpans(
        int firstYear,
        int firstMonth,
        int firstDay,
        int secondYear,
        int secondMonth,
        int secondDay,
        int calendarDays,
        int weekdays,
        int weekendDays,
        int wholeWeeks
    )
    {
        var result = DateSpanCalculator.Calculate(
            new DateOnly(firstYear, firstMonth, firstDay),
            new DateOnly(secondYear, secondMonth, secondDay)
        );

        Assert.Equal(
            new DateSpanResult(calendarDays, weekdays, weekendDays, wholeWeeks),
            result
        );
        Assert.Equal(result.CalendarDays, result.Weekdays + result.WeekendDays);
    }

    [Fact]
    public void ReversedInputsProduceTheSameSpan()
    {
        var first = new DateOnly(1999, 12, 31);
        var second = new DateOnly(2000, 1, 10);

        Assert.Equal(
            DateSpanCalculator.Calculate(first, second),
            DateSpanCalculator.Calculate(second, first)
        );
    }

    [Fact]
    public void EntireSupportedRangeDoesNotOverflow()
    {
        var result = DateSpanCalculator.Calculate(DateOnly.MinValue, DateOnly.MaxValue);

        Assert.Equal(DateOnly.MaxValue.DayNumber, result.CalendarDays);
        Assert.Equal(result.CalendarDays, result.Weekdays + result.WeekendDays);
        Assert.Equal(result.CalendarDays / 7, result.WholeWeeks);
    }

    [Fact]
    public void EndpointsFollowTodayIndependently()
    {
        var model = new DateSpanViewModel();
        var firstToday = new DateTime(2026, 9, 10);

        model.Refresh(firstToday);
        Assert.Equal(firstToday, model.FirstDate);
        Assert.Equal(firstToday, model.SecondDate);

        model.FirstDate = new DateTime(2026, 9, 1);
        model.Refresh(new DateTime(2026, 9, 11));
        Assert.Equal(new DateTime(2026, 9, 1), model.FirstDate);
        Assert.Equal(new DateTime(2026, 9, 11), model.SecondDate);
        Assert.Equal(10, model.Result?.CalendarDays);

        model.FirstToday(new DateTime(2026, 9, 11));
        model.Refresh(new DateTime(2026, 9, 12));
        Assert.Equal(new DateTime(2026, 9, 12), model.FirstDate);
        Assert.Equal(new DateTime(2026, 9, 12), model.SecondDate);
    }

    [Fact]
    public void MissingEndpointClearsEveryResult()
    {
        var model = new DateSpanViewModel { FirstDate = null };

        Assert.Null(model.Result);
        Assert.Equal("—", model.CalendarDays);
        Assert.Equal("—", model.Weekdays);
        Assert.Equal("—", model.WeekendDays);
        Assert.Equal("—", model.WholeWeeks);
    }
}
