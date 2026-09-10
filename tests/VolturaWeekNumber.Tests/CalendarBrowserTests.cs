using System.Globalization;
using VolturaWeekNumber.Features.Calendar;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class CalendarBrowserTests
{
    [Theory]
    [InlineData(2021, 52)]
    [InlineData(2026, 53)]
    public void IsoYearIncludesEveryNumberedWeekWithCompleteRanges(int year, int count)
    {
        var options = new CalendarOptions(CalendarMode.Iso);
        var weeks = CalendarPeriod.Weeks(new(year, 1, 1), new(year, 12, 31), options, CultureInfo.InvariantCulture);
        var belonging = weeks.Where(week => ISOWeek.GetYear(week.Range.Start.AddDays(3).ToDateTime(TimeOnly.MinValue)) == year).ToArray();

        Assert.Equal(count, belonging.Length);
        Assert.Equal(Enumerable.Range(1, count), belonging.Select(week => Assert.Single(week.Numbers)));
        Assert.All(weeks, week =>
        {
            Assert.Equal(DayOfWeek.Monday, week.Range.Start.DayOfWeek);
            Assert.Equal(6, week.Range.End.DayNumber - week.Range.Start.DayNumber);
            Assert.Equal(7, week.Days.Count);
        });
    }

    [Fact]
    public void AdjacentMonthsShareWholeWeeksAndLeapDayAppearsExactlyOnceInMonth()
    {
        var options = new CalendarOptions(CalendarMode.Iso);
        var february = CalendarPeriod.Weeks(new(2024, 2, 1), new(2024, 2, 29), options, CultureInfo.InvariantCulture);
        var march = CalendarPeriod.Weeks(new(2024, 3, 1), new(2024, 3, 31), options, CultureInfo.InvariantCulture);

        Assert.Equal(february[^1].Range, march[0].Range);
        Assert.Equal(new DateOnly(2024, 2, 26), march[0].Range.Start);
        Assert.Equal(new DateOnly(2024, 3, 3), march[0].Range.End);
        Assert.Single(february.SelectMany(week => week.Days), date => date == new DateOnly(2024, 2, 29));
    }

    [Fact]
    public void CalendarYearBoundaryRetainsBothRegionalWeekNumbersInOnePhysicalWeek()
    {
        var weeks = CalendarPeriod.Weeks(new(2000, 12, 1), new(2000, 12, 31),
            new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay), CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(new DateOnly(2000, 12, 31), weeks[^1].Range.Start);
        Assert.Equal([54, 1], weeks[^1].Numbers);
    }

    [Fact]
    public void OptionalHebrewCalendarUsesItsActualWeekNumbers()
    {
        var region = new CultureInfo("he-IL");

        region.DateTimeFormat.Calendar = new HebrewCalendar();

        var weeks = CalendarPeriod.Weeks(new(2022, 9, 1), new(2022, 9, 30),
            new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay), region);

        Assert.Contains(weeks, week => week.Numbers.Contains(55));
        Assert.Contains(weeks, week => week.Numbers.Contains(56));
        Assert.All(weeks, week => Assert.Equal(DayOfWeek.Sunday, week.Range.Start.DayOfWeek));
    }

    [Fact]
    public void DateBoundariesKeepWeekdayColumnsWithoutFabricatingDates()
    {
        var options = new CalendarOptions(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay);
        var first = Assert.Single(CalendarPeriod.Weeks(DateOnly.MinValue, DateOnly.MinValue, options, CultureInfo.InvariantCulture));
        var last = Assert.Single(CalendarPeriod.Weeks(DateOnly.MaxValue, DateOnly.MaxValue, options, CultureInfo.InvariantCulture));

        Assert.Null(first.Days[0]);
        Assert.Equal(DateOnly.MinValue, first.Days[1]);
        Assert.Equal(new DateOnly(1, 1, 6), first.Range.End);
        Assert.Equal(DateOnly.MaxValue, last.Days[5]);
        Assert.Null(last.Days[6]);
    }

    [Fact]
    public void UnsupportedRegionalPeriodFailsAsAWholeWhileIsoWorks()
    {
        var region = CultureInfo.GetCultureInfo("ar-SA");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalendarPeriod.Weeks(new(1800, 1, 1), new(1800, 12, 31), new(), region));
        Assert.NotEmpty(CalendarPeriod.Weeks(new(1800, 1, 1), new(1800, 12, 31), new(CalendarMode.Iso), region));
    }
}
