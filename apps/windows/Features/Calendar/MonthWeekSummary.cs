using System.Globalization;

namespace VolturaWeekNumber.Features.Calendar;

internal static class MonthWeekSummary
{
    internal static string Format(DateOnly month, CalendarOptions options, CultureInfo region)
    {
        var first = new DateOnly(month.Year, month.Month, 1);
        var numbers = Enumerable.Range(0, DateTime.DaysInMonth(month.Year, month.Month))
            .Select(offset => WeekCalculator.Calculate(first.AddDays(offset), options, region).Number)
            .Distinct().ToArray();
        var ranges = new List<string>();
        var start = numbers[0];
        var end = start;

        foreach (var number in numbers.Skip(1))
        {
            if (number == end + 1)
            {
                end = number;
            }
            else
            {
                ranges.Add(Range(start, end));
                start = end = number;
            }
        }

        ranges.Add(Range(start, end));

        return string.Join(", ", ranges);
    }

    private static string Range(int start, int end) => start == end
        ? start.ToString(CultureInfo.InvariantCulture)
        : $"{start.ToString(CultureInfo.InvariantCulture)}–{end.ToString(CultureInfo.InvariantCulture)}";
}
