using System.Globalization;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class WeekReferenceTests
{
    [Theory]
    [InlineData("Iso", "2026-W37 · 2026-09-07/2026-09-13")]
    [InlineData("Short", "W37 · 07/09/2026–13/09/2026")]
    [InlineData("Localized", "Week 37 · Monday, 7 September 2026 – Sunday, 13 September 2026")]
    public void AgreedEnglishFormats(string format, string expected) => Assert.Equal(expected,
        Format(new(2026, 9, 9), Enum.Parse<WeekReferenceFormat>(format)));

    [Theory]
    [InlineData(2021, 1, 1, "2020-W53 · 2020-12-28/2021-01-03")]
    [InlineData(2025, 12, 31, "2026-W01 · 2025-12-29/2026-01-04")]
    [InlineData(2024, 2, 29, "2024-W09 · 2024-02-26/2024-03-03")]
    public void IsoUsesWeekYearAndFullRange(int year, int month, int day, string expected) =>
        Assert.Equal(expected, Format(new(year, month, day), WeekReferenceFormat.Iso));

    [Fact]
    public void DisplayLanguageChangesTextWithoutChangingTheWeek()
    {
        Assert.Equal("Vecka 37 · måndag 7 september 2026 – söndag 13 september 2026",
            Format(new(2026, 9, 9), WeekReferenceFormat.Localized, "sv"));
        Assert.Equal("W37 · 2026-09-07–2026-09-13",
            Format(new(2026, 9, 9), WeekReferenceFormat.Short, "sv"));
        Assert.StartsWith("第37週 · 2026年9月7日", Format(new(2026, 9, 9), WeekReferenceFormat.Localized, "ja"));
        Assert.Equal("2026-W37 · 2026-09-07/2026-09-13",
            Format(new(2026, 9, 9), WeekReferenceFormat.Iso, "ja"));
    }

    [Fact]
    public void IsoConversionUsesAnchorAndDeduplicatesWhileOtherFormatsKeepDistinctRanges()
    {
        WeekReference[] references =
        [
            new(new(2026, 9, 6), 37, new(new(2026, 9, 6), new(2026, 9, 12))),
            new(new(2026, 9, 5), 36, new(new(2026, 9, 5), new(2026, 9, 11))),
        ];
        var culture = CultureInfo.GetCultureInfo("en-GB");

        Assert.Equal("2026-W36 · 2026-08-31/2026-09-06",
            WeekReferenceFormatter.Format(references, WeekReferenceFormat.Iso, culture, "Week {0}"));
        Assert.Equal("W37 · 06/09/2026–12/09/2026" + Environment.NewLine + "W36 · 05/09/2026–11/09/2026",
            WeekReferenceFormatter.Format(references, WeekReferenceFormat.Short, culture, "Week {0}"));
    }

    [Fact]
    public void EveryDisplayLanguageCanFormatRepresentableDateLimits()
    {
        foreach (var language in VolturaWeekNumber.Features.Localization.LanguageCatalog.All)
        {
            foreach (var date in new[] { DateOnly.MinValue, DateOnly.MaxValue })
            {
                foreach (var format in Enum.GetValues<WeekReferenceFormat>())
                {
                    Assert.NotEmpty(Format(date, format, language.Id));
                }
            }
        }
    }

    private static string Format(DateOnly date, WeekReferenceFormat format, string language = "en")
    {
        var strings = new Strings();

        strings.SetLanguage(language);

        var result = WeekCalculator.Calculate(date, new(CalendarMode.Iso), CultureInfo.InvariantCulture);

        return WeekReferenceFormatter.Format(
            [new(date, result.Number, WeekReferenceFormatter.RangeFromStart(result.WeekStart))],
            format, strings.Culture, strings["WeekNumberFormat"]);
    }
}
