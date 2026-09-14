using System.Security.Cryptography;
using System.Text;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;

namespace VolturaWeekNumber.Features.Calendar;

internal sealed record ImportedCalendar(int Version, Guid Id, string Name, string FileName,
    DateTimeOffset ImportedAt, int EventCount, string Hash, string Content);

internal sealed record ImportedOccurrence(Guid SourceId, string SourceName, string Title,
    string Location, string Description, DateTime Start, DateTime End, bool AllDay)
{
    internal bool Includes(DateOnly date) => Start.Date <= date.ToDateTime(TimeOnly.MinValue)
        && (End > Start
            ? End > date.ToDateTime(TimeOnly.MinValue)
            : DateOnly.FromDateTime(Start) == date);
}

internal static class CalendarImport
{
    internal const int MaximumBytes = 10 * 1024 * 1024;
    internal const int MaximumEvents = 20000;
    internal const int MaximumOccurrences = 10000;
    internal const int MaximumEvaluationIncrements = 10000;

    internal static ImportedCalendar Parse(string content, string fileName, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 32767)
        {
            throw new InvalidDataException("CalendarImportInvalid");
        }

        var calendar = Read(content);
        var name = calendar.Properties.Get<string>("X-WR-CALNAME") ?? calendar.Properties.Get<string>("NAME");

        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileNameWithoutExtension(fileName);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDataException("CalendarImportInvalid");
        }

        if (name.Length > 1024)
        {
            throw new InvalidDataException("CalendarImportLimit");
        }

        return new(1, id ?? Guid.NewGuid(), name, Path.GetFileName(fileName), DateTimeOffset.UtcNow,
            calendar.Events.Count, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))), content);
    }

    private static Ical.Net.Calendar Read(string content)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(content) > MaximumBytes)
            {
                throw new InvalidDataException("CalendarImportLimit");
            }

            if (!content.Trim().StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase)
                || !content.Trim().EndsWith("END:VCALENDAR", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("CalendarImportInvalid");
            }

            var calendars = Ical.Net.Calendar.Load<Ical.Net.Calendar>(content);

            if (calendars.Count != 1 || calendars[0].Events.Count == 0)
            {
                throw new InvalidDataException("CalendarImportInvalid");
            }

            var calendar = calendars[0];

            if (calendar.Events.Count > MaximumEvents)
            {
                throw new InvalidDataException("CalendarImportLimit");
            }

            foreach (var entry in calendar.Events)
            {
                if (entry.Status == "CANCELLED" && entry.RecurrenceIdentifier is { } cancelled)
                {
                    entry.DtStart ??= cancelled.StartTime;
                }

                if ((entry.Summary?.Length ?? 0) > 16384 || (entry.Description?.Length ?? 0) > 65536
                    || (entry.Location?.Length ?? 0) > 16384)
                {
                    throw new InvalidDataException("CalendarImportLimit");
                }

                if (entry.DtStart is null || string.IsNullOrWhiteSpace(entry.Uid)
                    || entry.RecurrenceIdentifier?.Range == RecurrenceRange.ThisAndFuture)
                {
                    throw new InvalidDataException("CalendarImportInvalid");
                }
                // Resolve time zones before saving; never silently reinterpret an unknown zone.

                _ = Local(entry.DtStart, TimeZoneInfo.Local);

                if (entry.EffectiveDuration.ToTimeSpanUnspecified() < TimeSpan.Zero)
                {
                    throw new InvalidDataException("CalendarImportInvalid");
                }

                if (entry.DtEnd is { } end && Local(end, TimeZoneInfo.Utc) < Local(entry.DtStart, TimeZoneInfo.Utc))
                {
                    throw new InvalidDataException("CalendarImportInvalid");
                }
            }

            return calendar;
        }
        catch (Exception error) when (error is not InvalidDataException && error is not OutOfMemoryException)
        {
            throw new InvalidDataException("CalendarImportInvalid", error);
        }
    }

    internal static DateTime Local(CalDateTime value, TimeZoneInfo zone) => !value.HasTime || value.IsFloating
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified)
        : TimeZoneInfo.ConvertTimeFromUtc(value.AsUtc, zone);

    internal static IReadOnlyList<ImportedOccurrence> Query(IReadOnlyList<ImportedCalendar> sources,
        DateOnly first, DateOnly last, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var result = new List<ImportedOccurrence>();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var calendar = Read(source.Content);
            // Include starts before the view whose duration overlaps it. Overrides may move a start.
            var lookback = calendar.Events.Max(entry => Math.Max(0,
                entry.EffectiveDuration.ToTimeSpanUnspecified().TotalDays));

            lookback = Math.Max(lookback, calendar.Events.SelectMany(entry => entry.RecurrenceDates.GetAllPeriods())
                .Select(period => period.EffectiveDuration?.ToTimeSpanUnspecified().TotalDays ?? 0).DefaultIfEmpty().Max());

            var start = first.ToDateTime(TimeOnly.MinValue).AddDays(-Math.Min(first.DayNumber, Math.Ceiling(lookback) + 2));
            var end = last == DateOnly.MaxValue
                ? DateTime.MaxValue
                : last.AddDays(1).ToDateTime(TimeOnly.MinValue);

            try
            {
                ValidateEvaluationRange(calendar, end, cancellationToken);

                var count = 0;
                var periodLookups = new Dictionary<CalendarEvent, Dictionary<CalDateTime, Period>?>(ReferenceEqualityComparer.Instance);

                foreach (var occurrence in calendar.GetOccurrences(new CalDateTime(start),
                    new EvaluationOptions { MaxUnmatchedIncrementsLimit = 10000 })
                    .TakeWhileBefore(new CalDateTime(end.Date == DateTime.MaxValue.Date
                        ? DateTime.MaxValue
                        : end.AddDays(1))))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (++count > MaximumOccurrences || result.Count >= MaximumOccurrences)
                    {
                        throw new InvalidDataException("CalendarImportLimit");
                    }

                    if (occurrence.Source is not CalendarEvent entry || entry.Status == "CANCELLED")
                    {
                        continue;
                    }

                    var from = Local(occurrence.Period.StartTime, zone);
                    // Ical.Net applies the master duration to RDATE periods; retain their explicit end.

                    if (!periodLookups.TryGetValue(entry, out var periods))
                    {
                        foreach (var period in entry.RecurrenceDates.GetAllPeriods())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (period.EndTime is not null || period.Duration is not null)
                            {
                                periods ??= new Dictionary<CalDateTime, Period>();
                                periods.TryAdd(period.StartTime, period);
                            }
                        }

                        periodLookups.Add(entry, periods);
                    }

                    Period? explicitPeriod = null;

                    periods?.TryGetValue(occurrence.Period.StartTime, out explicitPeriod);

                    var to = Local(explicitPeriod?.EffectiveEndTime ?? occurrence.Period.EffectiveEndTime ?? occurrence.Period.StartTime, zone);

                    if (from >= end || (to > from
                        ? to <= first.ToDateTime(TimeOnly.MinValue)
                        : from < first.ToDateTime(TimeOnly.MinValue)))
                    {
                        continue;
                    }

                    result.Add(new(source.Id, source.Name, entry.Summary ?? "", entry.Location ?? "",
                        entry.Description ?? "", from, to, !occurrence.Period.StartTime.HasTime));
                }
            }
            catch (Exception error) when (error is not OperationCanceledException && error is not InvalidDataException && error is not OutOfMemoryException)
            {
                throw new InvalidDataException("CalendarImportInvalid", error);
            }
        }

        return result.OrderByDescending(item => item.AllDay).ThenBy(item => item.Start).ThenBy(item => item.Title, StringComparer.CurrentCulture).ToArray();
    }

    private static void ValidateEvaluationRange(Ical.Net.Calendar calendar, DateTime end, CancellationToken token)
    {
        // Ical.Net advances from DTSTART one interval at a time before yielding. Its unmatched
        // limit does not cover that traversal, including for exception rules. Bound it upfront.
        var remaining = (double)MaximumEvaluationIncrements;
        var remainingCandidates = 1000000d;

        foreach (var entry in calendar.Events)
        {
            token.ThrowIfCancellationRequested();

            // Inspect every rule the current parser evaluates, including legacy RRULE/EXRULE lists.
#pragma warning disable CS0618

            var rules = entry.RecurrenceRules.Concat(entry.ExceptionRules);
#pragma warning restore CS0618

            foreach (var rule in rules)
            {
                var seconds = rule.Frequency switch
                {
                    FrequencyType.Secondly => 1d,
                    FrequencyType.Minutely => 60d,
                    FrequencyType.Hourly => 3600d,
                    FrequencyType.Daily => 86400d,
                    FrequencyType.Weekly => 7 * 86400d,
                    FrequencyType.Monthly => 28 * 86400d,
                    FrequencyType.Yearly => 365 * 86400d,
                    _ => throw new InvalidDataException("CalendarImportInvalid"),
                };

                if (rule.Interval < 1)
                {
                    throw new InvalidDataException("CalendarImportInvalid");
                }

                // Include the extra query day and time-zone offsets. Do not cap by COUNT or
                // UNTIL: the library's initial seek can traverse beyond those bounds.

                var span = Math.Max(0, (end - entry.DtStart!.Value).TotalSeconds) + 2 * 86400d;
                var increments = Math.Ceiling(span / (seconds * rule.Interval)) + 1;
                // Bound the intermediate BY* product, including the list materialized by
                // negative BYSETPOS. Use conservative day bounds for each frequency.
                var days = rule.Frequency switch
                {
                    FrequencyType.Yearly => 366d,
                    FrequencyType.Monthly => 31d,
                    FrequencyType.Weekly => 7d,
                    _ => 1d,
                };
                var candidates = days * Math.Max(1, rule.ByMonth.Count)
                    * Math.Max(1, rule.ByWeekNo.Count) * Math.Max(1, rule.ByYearDay.Count)
                    * Math.Max(1, rule.ByMonthDay.Count) * Math.Max(1, rule.ByDay.Count)
                    * Math.Max(1, rule.ByHour.Count) * Math.Max(1, rule.ByMinute.Count)
                    * Math.Max(1, rule.BySecond.Count);

                remaining -= increments;
                remainingCandidates -= increments * candidates;

                if (remaining < 0 || candidates > MaximumOccurrences || remainingCandidates < 0)
                {
                    throw new InvalidDataException("CalendarImportLimit");
                }
            }
        }
    }
}
