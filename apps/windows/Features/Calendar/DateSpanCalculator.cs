namespace VolturaWeekNumber.Features.Calendar;

internal readonly record struct DateSpanResult(
    int CalendarDays,
    int Weekdays,
    int WeekendDays,
    int WholeWeeks
);

internal static class DateSpanCalculator
{
    internal static DateSpanResult Calculate(DateOnly first, DateOnly second)
    {
        var startDay = Math.Min(first.DayNumber, second.DayNumber);
        var calendarDays = Math.Abs(second.DayNumber - first.DayNumber);
        var wholeWeeks = calendarDays / 7;
        var weekdays = wholeWeeks * 5;
        var remainder = calendarDays % 7;
        var day = DateOnly.FromDayNumber(startDay).DayOfWeek;

        for (var index = 0; index < remainder; index++)
        {
            if (day is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                weekdays++;
            }

            day = (DayOfWeek)(((int)day + 1) % 7);
        }

        return new DateSpanResult(
            calendarDays,
            weekdays,
            calendarDays - weekdays,
            wholeWeeks
        );
    }
}
