using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VolturaWeekNumber.Features.Calendar;

internal enum CalendarExportScope { Year, Month, Week }

internal sealed record CalendarExportRequest(CalendarExportScope Scope, DateOnly Anchor)
{
    internal string FileName => $"VolturaWeekNumber-{Anchor.ToString(Scope switch
    {
        CalendarExportScope.Year => "yyyy",
        CalendarExportScope.Month => "yyyy-MM",
        _ => "yyyy-MM-dd",
    }, CultureInfo.InvariantCulture)}.ics";
}

internal static class CalendarExport
{
    internal static IReadOnlyList<WeekResult> Weeks(CalendarExportRequest request, CalendarOptions options, CultureInfo region)
    {
        var anchor = request.Anchor;

        if (request.Scope == CalendarExportScope.Week)
        {
            var start = WeekCalculator.Calculate(anchor, options, region).WeekStart;
            // A truncated week at DateOnly.MinValue has no representable first day.

            if (start.DayOfWeek != WeekCalculator.FirstWeekday(options, region))
            {
                throw new ArgumentOutOfRangeException(nameof(request));
            }

            return [WeekCalculator.Calculate(start, options, region)];
        }

        var first = request.Scope == CalendarExportScope.Year
            ? new DateOnly(anchor.Year, 1, 1)
            : new DateOnly(anchor.Year, anchor.Month, 1);
        var last = request.Scope == CalendarExportScope.Year
            ? new DateOnly(anchor.Year, 12, 31)
            : new DateOnly(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month));
        var results = new List<WeekResult>();
        // Validate the complete requested period, including partial weeks, against the calendar.

        for (var day = first.DayNumber; day <= last.DayNumber; day++)
        {
            var date = DateOnly.FromDayNumber(day);
            var week = WeekCalculator.Calculate(date, options, region);

            if (date.DayOfWeek == WeekCalculator.FirstWeekday(options, region))
            {
                results.Add(week);
            }
        }

        return results;
    }

    internal static string Generate(CalendarExportRequest request, CalendarOptions options, CultureInfo region,
        CultureInfo language, string weekFormat, string convention, DateTimeOffset timestamp)
    {
        var result = new StringBuilder();

        void Line(string value) => AppendLine(result, value);
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Voltura AB//Voltura WeekNumber//EN");
        Line("CALSCALE:GREGORIAN");

        var period = request.Scope switch
        {
            CalendarExportScope.Year => request.Anchor.Year.ToString(CultureInfo.InvariantCulture),
            CalendarExportScope.Month => request.Anchor.ToString("Y", language),
            _ => request.Anchor.ToString("d", language),
        };

        Line("X-WR-CALNAME:" + Escape($"Voltura WeekNumber · {period}"));

        var identity = WeekCalculator.ConventionIdentity(options, region);

        foreach (var week in Weeks(request, options, region))
        {
            var date = week.WeekStart;
            var end = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, date.DayNumber + 6));
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}|{identity}")));

            Line("BEGIN:VEVENT");
            Line($"UID:{id}@voltura-weeknumber");
            Line($"DTSTAMP:{timestamp.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}");
            Line($"DTSTART;VALUE=DATE:{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");
            Line("SUMMARY:" + Escape(string.Format(language, weekFormat, week.Number.ToString("D2", CultureInfo.InvariantCulture))));
            Line("DESCRIPTION:" + Escape($"{date.ToString("d", language)}–{end.ToString("d", language)}\n{convention}"));
            Line("TRANSP:TRANSPARENT");
            Line("END:VEVENT");
        }

        Line("END:VCALENDAR");

        return result.ToString();
    }

    internal static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
        .Replace("\n", "\\n", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal);

    private static void AppendLine(StringBuilder output, string line)
    {
        var bytes = 0;

        foreach (var rune in line.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > 75)
            {
                output.Append("\r\n ");
                bytes = 1;
            }

            output.Append(rune.ToString());
            bytes += rune.Utf8SequenceLength;
        }

        output.Append("\r\n");
    }

    internal static void Save(string path, string content)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $".voltura-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
