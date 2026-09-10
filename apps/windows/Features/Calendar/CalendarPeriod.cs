using System.Globalization;

namespace VolturaWeekNumber.Features.Calendar;

internal sealed record CalendarWeek(WeekRange Range, IReadOnlyList<DateOnly?> Days, IReadOnlyList<int> Numbers);

internal static class CalendarPeriod
{
    // Build complete visual weeks, including adjacent-month dates. Never silently omit unsupported dates.
    internal static IReadOnlyList<CalendarWeek> Weeks(
        DateOnly start, DateOnly end, CalendarOptions options, CultureInfo region)
    {
        var first = WeekCalculator.FirstWeekday(options, region);
        var offset = ((int)start.DayOfWeek - (int)first + 7) % 7;
        var weeks = new List<CalendarWeek>();

        for (var weekStart = start.DayNumber - offset; weekStart <= end.DayNumber; weekStart += 7)
        {
            var days = Enumerable.Range(weekStart, 7)
                .Select(day => day < 0 || day > DateOnly.MaxValue.DayNumber
                    ? (DateOnly?)null
                    : DateOnly.FromDayNumber(day))
                .ToArray();
            var dates = days.OfType<DateOnly>().ToArray();
            var numbers = dates.Select(date => WeekCalculator.Calculate(date, options, region).Number)
                .Distinct().ToArray();

            weeks.Add(new(new(dates[0], dates[^1]), days, numbers));
        }

        return weeks;
    }
}
