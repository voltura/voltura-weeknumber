using System.Globalization;
using VolturaWeekNumber.Features.Calendar;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class MonthWeekSummaryTests
{
    [Theory]
    [InlineData(2026, 11, "44–49")]
    [InlineData(2021, 1, "53, 1–4")]
    [InlineData(2025, 12, "49–52, 1")]
    [InlineData(2024, 2, "5–9")]
    public void IsoRangesKeepChronologicalOrderAcrossWeekYearBoundaries(int year, int month, string expected)
    {
        Assert.Equal(expected, MonthWeekSummary.Format(new(year, month, 1), new(CalendarMode.Iso), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void RegionalRangeIncludesOnlyDatesInTheMonth()
    {
        Assert.Equal("49–54", MonthWeekSummary.Format(new(2000, 12, 1),
            new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay), CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void UnsupportedRegionalDatesAreNotAssignedAnInventedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthWeekSummary.Format(new(1800, 1, 1), new(), CultureInfo.GetCultureInfo("ar-SA")));
    }
}
