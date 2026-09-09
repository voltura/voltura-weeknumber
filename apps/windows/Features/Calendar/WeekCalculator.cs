using System.Globalization;

namespace VolturaWeekNumber.Features.Calendar;

public enum CalendarMode
{
    Regional,
    Iso,
    Custom,
}

public sealed record CalendarOptions(
    CalendarMode Mode = CalendarMode.Regional,
    DayOfWeek FirstDay = DayOfWeek.Monday,
    CalendarWeekRule Rule = CalendarWeekRule.FirstFourDayWeek
);

public sealed record WeekResult(int Number, int? IsoYear, DateOnly WeekStart);

public sealed record WeekRange(DateOnly Start, DateOnly End);

public static class WeekCalculator
{
    public static WeekResult Calculate(DateOnly date, CalendarOptions options, CultureInfo region)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(region);

        var calendar = region.DateTimeFormat.Calendar;
        var first =
            options.Mode == CalendarMode.Regional
                ? region.DateTimeFormat.FirstDayOfWeek
                : options.FirstDay;
        var rule =
            options.Mode == CalendarMode.Regional
                ? region.DateTimeFormat.CalendarWeekRule
                : options.Rule;
        var iso =
            options.Mode == CalendarMode.Iso
            || (
                calendar is GregorianCalendar
                && first == DayOfWeek.Monday
                && rule == CalendarWeekRule.FirstFourDayWeek
            );

        if (iso)
        {
            first = DayOfWeek.Monday;
        }

        var value = date.ToDateTime(TimeOnly.MinValue);
        var number = iso
            ? ISOWeek.GetWeekOfYear(value)
            : calendar.GetWeekOfYear(value, rule, first);
        var offset = ((int)date.DayOfWeek - (int)first + 7) % 7;

        return new(
            number,
            iso
                ? ISOWeek.GetYear(value)
                : null,
            DateOnly.FromDayNumber(Math.Max(0, date.DayNumber - offset))
        );
    }

    public static IReadOnlyList<WeekRange> FindRanges(
        int year,
        int week,
        CalendarOptions options,
        CultureInfo region
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(region);

        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (week is < 1 or > 56)
        {
            throw new ArgumentOutOfRangeException(nameof(week));
        }

        var ranges = new List<WeekRange>();
        var date = new DateOnly(year, 1, 1);
        var last = new DateOnly(year, 12, 31);

        while (true)
        {
            try
            {
                var result = Calculate(date, options, region);

                if (
                    result.Number == week
                    && (result.IsoYear is null || result.IsoYear == year)
                    && (ranges.Count == 0 || ranges[^1].Start != result.WeekStart)
                )
                {
                    ranges.Add(
                        new(
                            result.WeekStart,
                            DateOnly.FromDayNumber(
                                Math.Min(DateOnly.MaxValue.DayNumber, result.WeekStart.DayNumber + 6)
                            )
                        )
                    );
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // The active regional calendar may not support the entire Gregorian year.
            }

            if (date == last)
            {
                return ranges;
            }

            date = date.AddDays(1);
        }
    }

    public static TimeSpan UntilNextMidnight(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var next = DateTime.SpecifyKind(local.Date.AddDays(1), DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(next))
        {
            next = next.AddMinutes(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(next, zone) - now.UtcDateTime;
    }
}
