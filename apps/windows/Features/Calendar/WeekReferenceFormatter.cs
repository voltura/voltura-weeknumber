using System.Globalization;

namespace VolturaWeekNumber.Features.Calendar;

internal enum WeekReferenceFormat
{
    Iso,
    Short,
    Localized,
}

internal sealed record WeekReference(DateOnly Anchor, int Number, WeekRange Range);

internal static class WeekReferenceFormatter
{
    internal static WeekRange RangeFromStart(DateOnly start) => new(
        start,
        DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, start.DayNumber + 6))
    );

    internal static string Format(
        IEnumerable<WeekReference> references,
        WeekReferenceFormat format,
        CultureInfo culture,
        string weekNumberPattern
    ) => string.Join(Environment.NewLine, references.Select(reference =>
        FormatOne(reference, format, culture, weekNumberPattern)).Distinct(StringComparer.Ordinal));

    private static string FormatOne(
        WeekReference reference,
        WeekReferenceFormat format,
        CultureInfo culture,
        string weekNumberPattern
    )
    {
        if (format == WeekReferenceFormat.Iso)
        {
            var iso = WeekCalculator.Calculate(reference.Anchor, new(CalendarMode.Iso), CultureInfo.InvariantCulture);
            var range = RangeFromStart(iso.WeekStart);

            return string.Create(CultureInfo.InvariantCulture,
                $"{iso.IsoYear:D4}-W{iso.Number:D2} · {range.Start:yyyy-MM-dd}/{range.End:yyyy-MM-dd}");
        }

        var number = reference.Number.ToString("D2", CultureInfo.InvariantCulture);
        var start = reference.Range.Start;
        var end = reference.Range.End;

        return format switch
        {
            WeekReferenceFormat.Short => $"W{number} · {start.ToString("d", culture)}–{end.ToString("d", culture)}",
            WeekReferenceFormat.Localized =>
                $"{string.Format(culture, weekNumberPattern, number)} · {start.ToString("D", culture)} – {end.ToString("D", culture)}",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }
}
