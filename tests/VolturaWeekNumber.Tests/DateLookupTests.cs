using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class DateLookupTests
{
    [Theory]
    [InlineData(2026, 1, 1, 1)]
    [InlineData(2026, 2, 19, 50)]
    [InlineData(2024, 2, 29, 60)]
    [InlineData(2024, 3, 1, 61)]
    [InlineData(2026, 12, 31, 365)]
    [InlineData(2024, 12, 31, 366)]
    [InlineData(1900, 12, 31, 365)]
    [InlineData(2000, 12, 31, 366)]
    [InlineData(2100, 12, 31, 365)]
    public void OrdinalRoundTrips(int year, int month, int day, int ordinal)
    {
        var date = new DateOnly(year, month, day);
        Assert.Equal(ordinal, date.DayOfYear);
        Assert.Equal(date, DateLookup.FromDayOfYear(year, ordinal));
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(10000, 1)] [InlineData(2026, 366)]
    [InlineData(2024, 367)] [InlineData(2024, 0)]
    public void InvalidOrdinalsAreRejected(int year, int day) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DateLookup.FromDayOfYear(year, day));

    [Fact]
    public void JulianReferenceAndAllSupportedDatesRoundTrip()
    {
        Assert.Equal(2451545, DateLookup.ToJulianDay(new DateOnly(2000, 1, 1)));
        Assert.Equal(1721426, DateLookup.ToJulianDay(DateOnly.MinValue));
        Assert.Equal(5373484, DateLookup.ToJulianDay(DateOnly.MaxValue));
        for (var day = 0; day <= DateOnly.MaxValue.DayNumber; day++)
        {
            var date = DateOnly.FromDayNumber(day);
            Assert.Equal(1721426 + day, DateLookup.ToJulianDay(date));
            Assert.Equal(date, DateLookup.FromJulianDay(1721426 + day));
        }
    }

    [Theory]
    [InlineData(int.MinValue)] [InlineData(1721425)] [InlineData(5373485)] [InlineData(int.MaxValue)]
    public void InvalidJulianDaysAreRejected(int day) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DateLookup.FromJulianDay(day));

    [Fact]
    public void IndependentSelectionsFollowTodayOnlyUntilEdited()
    {
        var ordinal = new DateLookupViewModel(false);
        var julian = new DateLookupViewModel(true);
        var tomorrow = DateTime.Today.AddDays(1);
        ordinal.SelectedDate = new DateTime(2024, 1, 1);
        ordinal.Refresh(tomorrow); julian.Refresh(tomorrow);
        Assert.Equal("001", ordinal.Result);
        Assert.Equal(tomorrow, julian.SelectedDate);
        ordinal.Today();
        Assert.Equal(DateTime.Today, ordinal.SelectedDate);
    }

    [Theory]
    [InlineData(false, "", "2024")]
    [InlineData(false, "366", "2026")]
    [InlineData(false, "1", "")]
    [InlineData(true, "2451545.5", "")]
    [InlineData(true, "-1", "")]
    public void InvalidLookupPreservesSelection(bool julian, string number, string year)
    {
        var model = new DateLookupViewModel(julian) { SelectedDate = new DateTime(2000, 1, 1), NumberInput = number, YearInput = year };
        model.Convert();
        Assert.Equal(new DateTime(2000, 1, 1), model.SelectedDate);
        Assert.NotEmpty(model.Error);
    }

    [Fact]
    public void SuccessfulReverseLookupClearsError()
    {
        var model = new DateLookupViewModel(false) { YearInput = "2024", NumberInput = "bad" };
        model.Convert();
        model.NumberInput = "060"; model.Convert();
        Assert.Empty(model.Error);
        Assert.Equal(new DateTime(2024, 2, 29), model.SelectedDate);
        model.SelectedDate = null;
        Assert.Equal("—", model.Result);
    }
}
