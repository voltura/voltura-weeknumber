using System.Globalization;
using VolturaWeekNumber.Features.Calendar;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class CalendarTests
{
    [Fact]
    public void FirstDayRuleCanProduceWeek54()
    {
        var result = WeekCalculator.Calculate(
            new(2000, 12, 31),
            new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay),
            CultureInfo.GetCultureInfo("en-US")
        );

        Assert.Equal(54, result.Number);
        Assert.Equal(new DateOnly(2000, 12, 31), result.WeekStart);
    }

    [Fact]
    public void RegionalCalendarRejectsUnsupportedDateWhileIsoStillWorks()
    {
        var region = CultureInfo.GetCultureInfo("ar-SA");
        var date = new DateOnly(1800, 1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WeekCalculator.Calculate(date, new(), region)
        );
        Assert.Equal(1, WeekCalculator.Calculate(date, new(CalendarMode.Iso), region).Number);
        Assert.InRange(WeekCalculator.Calculate(new(2026, 9, 9), new(), region).Number, 1, 53);
    }

    [Fact]
    public void HebrewLeapYearCanProduceWeek56()
    {
        var region = new CultureInfo("he-IL");

        region.DateTimeFormat.Calendar = new HebrewCalendar();

        var result = WeekCalculator.Calculate(
            new(2022, 9, 25),
            new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay),
            region
        );

        Assert.Equal(56, result.Number);
        Assert.Null(result.IsoYear);
    }

    [Fact]
    public void RegionalAndCustomRulesUseTheSelectedOptionalCalendar()
    {
        var region = new CultureInfo("he-IL");

        region.DateTimeFormat.Calendar = new HebrewCalendar();
        region.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Sunday;
        region.DateTimeFormat.CalendarWeekRule = CalendarWeekRule.FirstDay;

        var date = new DateOnly(2022, 9, 25);

        Assert.Equal(56, WeekCalculator.Calculate(date, new(), region).Number);
        Assert.Equal(
            56,
            WeekCalculator.Calculate(
                date,
                new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay),
                region
            ).Number
        );
        Assert.Equal(38, WeekCalculator.Calculate(date, new(CalendarMode.Iso), region).Number);
    }

    [Theory]
    [InlineData(2024, 12, 31, 1, 2025)]
    [InlineData(2025, 12, 29, 1, 2026)]
    [InlineData(2021, 1, 1, 53, 2020)]
    [InlineData(2027, 1, 1, 53, 2026)]
    [InlineData(2024, 2, 29, 9, 2024)]
    public void IsoBoundaries(int year, int month, int day, int number, int isoYear)
    {
        var result = WeekCalculator.Calculate(
            new(year, month, day),
            new(CalendarMode.Iso),
            CultureInfo.GetCultureInfo("en-US")
        );

        Assert.Equal(number, result.Number);
        Assert.Equal(isoYear, result.IsoYear);
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("de-DE")]
    public void RegionalIsoUsesCorrectWeekYear(string region)
    {
        var result = WeekCalculator.Calculate(
            new(2024, 12, 31),
            new(),
            CultureInfo.GetCultureInfo(region)
        );

        Assert.Equal(1, result.Number);
        Assert.Equal(2025, result.IsoYear);
    }

    [Fact]
    public void EveryCustomRuleUsesItsOwnConvention()
    {
        var region = CultureInfo.GetCultureInfo("en-US");

        foreach (var first in Enum.GetValues<DayOfWeek>())
        {
            foreach (var rule in Enum.GetValues<CalendarWeekRule>())
            {
                foreach (
                    var date in new[]
                    {
                        new DateTime(2024, 12, 31),
                        new DateTime(2021, 1, 1),
                        new DateTime(2024, 2, 29),
                    }
                )
                {
                    var result = WeekCalculator.Calculate(
                        DateOnly.FromDateTime(date),
                        new(CalendarMode.Custom, first, rule),
                        region
                    );
                    var expected =
                        first == DayOfWeek.Monday && rule == CalendarWeekRule.FirstFourDayWeek
                            ? ISOWeek.GetWeekOfYear(date)
                            : region.Calendar.GetWeekOfYear(date, rule, first);

                    Assert.Equal(expected, result.Number);
                }
            }
        }
    }

    [Fact]
    public void ReverseLookupUsesTheActiveConventionAndWeekYear()
    {
        var region = CultureInfo.GetCultureInfo("en-US");
        var iso = WeekCalculator.FindRanges(2026, 1, new(CalendarMode.Iso), region);

        Assert.Equal(
            [new WeekRange(new(2025, 12, 29), new(2026, 1, 4))],
            iso
        );
        Assert.Empty(WeekCalculator.FindRanges(2021, 53, new(CalendarMode.Iso), region));

        var custom = WeekCalculator.FindRanges(
            2000,
            54,
            new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay),
            region
        );

        Assert.Equal(
            [new WeekRange(new(2000, 12, 31), new(2001, 1, 6))],
            custom
        );

        var hebrew = new CultureInfo("he-IL");

        hebrew.DateTimeFormat.Calendar = new HebrewCalendar();

        Assert.Contains(
            new WeekRange(new(2022, 9, 25), new(2022, 10, 1)),
            WeekCalculator.FindRanges(
                2022,
                56,
                new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay),
                hebrew
            )
        );
    }

    [Fact]
    public void WeekNotificationsDeduplicateAndSettingsRefreshResetsBaseline()
    {
        var tracker = new WeekTracker();
        var first = new WeekResult(1, 2025, new(2024, 12, 30));
        var next = new WeekResult(2, 2025, new(2025, 1, 6));

        Assert.False(tracker.Observe(first, true));
        Assert.False(tracker.Observe(first, true));
        Assert.True(tracker.Observe(next, true));
        Assert.False(tracker.Observe(next, true));
        Assert.False(tracker.Observe(first, false));
        Assert.False(tracker.Observe(first, true));
    }

    [Theory]
    [InlineData(2026, 3, 29, 23)]
    [InlineData(2026, 10, 25, 25)]
    public void MidnightScheduleRespectsDaylightSaving(int year, int month, int day, int hours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        var now = new DateTimeOffset(date, zone.GetUtcOffset(date));

        Assert.Equal(TimeSpan.FromHours(hours), WeekCalculator.UntilNextMidnight(now, zone));
    }
}
