namespace VolturaWeekNumber.Features.Calendar;

public static class DateLookup
{
    // DateOnly day zero is Gregorian 0001-01-01, whose noon is JDN 1721426.
    private const int JulianEpoch = 1721426;

    public static int ToJulianDay(DateOnly date) => date.DayNumber + JulianEpoch;

    public static DateOnly FromJulianDay(int day)
    {
        if (day < JulianEpoch || day > JulianEpoch + DateOnly.MaxValue.DayNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        return DateOnly.FromDayNumber(day - JulianEpoch);
    }

    public static DateOnly FromDayOfYear(int year, int day)
    {
        var start = new DateOnly(year, 1, 1);

        if (day < 1 || day > (DateTime.IsLeapYear(year)
            ? 366
            : 365))
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        return start.AddDays(day - 1);
    }
}
