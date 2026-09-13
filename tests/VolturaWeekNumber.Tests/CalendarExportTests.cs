using System.Globalization;
using System.Text;
using VolturaWeekNumber.Features.Calendar;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class CalendarExportTests
{
    private static readonly CalendarOptions Iso = new(CalendarMode.Iso);
    private static readonly CultureInfo Region = CultureInfo.GetCultureInfo("en-GB");

    [Fact]
    public void MonthIncludesOnlyStartsInsideMonthAndWeekCanStartOutsideIt()
    {
        var request = new CalendarExportRequest(CalendarExportScope.Month, new(2026, 9, 13));
        var weeks = CalendarExport.Weeks(request, Iso, Region);

        Assert.Equal([7, 14, 21, 28], weeks.Select(week => week.WeekStart.Day));
        Assert.Equal([37, 38, 39, 40], weeks.Select(week => week.Number));

        var boundary = CalendarExport.Weeks(new(CalendarExportScope.Week, new(2026, 9, 1)), Iso, Region);

        Assert.Equal(new DateOnly(2026, 8, 31), Assert.Single(boundary).WeekStart);
    }

    [Fact]
    public void IsoYearExportsCalendarYearStartsIncludingWeek53()
    {
        var weeks = CalendarExport.Weeks(new(CalendarExportScope.Year, new(2020, 1, 1)), Iso, Region);

        Assert.Equal(52, weeks.Count);
        Assert.Equal(new DateOnly(2020, 1, 6), weeks[0].WeekStart);
        Assert.Equal(53, weeks[^1].Number);
        Assert.Equal(new DateOnly(2020, 12, 28), weeks[^1].WeekStart);

        var january = CalendarExport.Weeks(new(CalendarExportScope.Week, new(2021, 1, 1)), Iso, Region);

        Assert.Equal(2020, Assert.Single(january).IsoYear);
        Assert.Equal(53, january[0].Number);
    }

    [Theory]
    [InlineData("en-US", DayOfWeek.Sunday)]
    [InlineData("sv-SE", DayOfWeek.Monday)]
    public void RegionalStartsFollowRegion(string region, DayOfWeek first)
    {
        var weeks = CalendarExport.Weeks(new(CalendarExportScope.Year, new(2024, 1, 1)), new(), CultureInfo.GetCultureInfo(region));

        Assert.All(weeks, week => Assert.Equal(first, week.WeekStart.DayOfWeek));
        Assert.Contains(weeks, week => week.WeekStart.Month == 2 && week.WeekStart.Day >= 25);
    }

    [Theory]
    [InlineData(CalendarWeekRule.FirstDay)]
    [InlineData(CalendarWeekRule.FirstFullWeek)]
    [InlineData(CalendarWeekRule.FirstFourDayWeek)]
    public void CustomStartsAndNumbersFollowExistingCalculator(CalendarWeekRule rule)
    {
        var options = new CalendarOptions(CalendarMode.Custom, DayOfWeek.Wednesday, rule);
        var weeks = CalendarExport.Weeks(new(CalendarExportScope.Year, new(2025, 1, 1)), options, Region);

        Assert.Equal(53, weeks.Count);
        Assert.All(weeks, week =>
        {
            Assert.Equal(DayOfWeek.Wednesday, week.WeekStart.DayOfWeek);
            Assert.Equal(WeekCalculator.Calculate(week.WeekStart, options, Region), week);
        });
    }

    [Fact]
    public void BoundariesDoNotOverflowOrPretendTruncatedWeekHasAStart()
    {
        Assert.NotEmpty(CalendarExport.Weeks(new(CalendarExportScope.Year, DateOnly.MinValue), Iso, Region));
        Assert.NotEmpty(CalendarExport.Weeks(new(CalendarExportScope.Year, DateOnly.MaxValue), Iso, Region));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarExport.Weeks(new(CalendarExportScope.Week, DateOnly.MinValue),
            new(CalendarMode.Custom, DayOfWeek.Sunday), Region));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarExport.Weeks(new(CalendarExportScope.Year, new(1900, 1, 1)),
            new(), CultureInfo.GetCultureInfo("ar-SA")));
    }

    [Fact]
    public void IcalendarUsesTransparentSingleDayEventsAndStableCrossScopeIdentity()
    {
        var month = Generate(new(CalendarExportScope.Month, new(2026, 9, 1)));
        var week = Generate(new(CalendarExportScope.Week, new(2026, 9, 7)));

        Assert.Contains("X-WR-CALNAME:Voltura WeekNumber · September 2026", month, StringComparison.Ordinal);

        Assert.Contains("DTSTART;VALUE=DATE:20260907\r\n", week, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:Week 37\r\n", week, StringComparison.Ordinal);
        Assert.Contains("TRANSP:TRANSPARENT\r\n", week, StringComparison.Ordinal);
        Assert.DoesNotContain("DTEND", week, StringComparison.Ordinal);
        Assert.DoesNotContain("DURATION", week, StringComparison.Ordinal);
        Assert.DoesNotContain("RRULE", week, StringComparison.Ordinal);
        Assert.DoesNotContain("VALARM", week, StringComparison.Ordinal);
        Assert.Contains("DTSTAMP:20260913T100000Z\r\n", week, StringComparison.Ordinal);

        var uid = Unfold(week).Split("\r\n").Single(line => line.StartsWith("UID:", StringComparison.Ordinal));

        Assert.Contains(uid, Unfold(month), StringComparison.Ordinal);
        Assert.Equal(WeekCalculator.ConventionIdentity(Iso, Region), WeekCalculator.ConventionIdentity(new(), Region));

        var custom = CalendarExport.Generate(new(CalendarExportScope.Week, new(2026, 9, 7)),
            new(CalendarMode.Custom, DayOfWeek.Monday, CalendarWeekRule.FirstDay), Region, Region, "Week {0}", "Custom", DateTimeOffset.UtcNow);

        Assert.DoesNotContain(uid, Unfold(custom), StringComparison.Ordinal);

        var output = Environment.GetEnvironmentVariable("VOLTURA_CALENDAR_REVIEW");

        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "month.ics"), month);
            File.WriteAllText(Path.Combine(output, "week.ics"), week);
            File.WriteAllText(Path.Combine(output, "year.ics"), Generate(new(CalendarExportScope.Year, new(2020, 1, 1))));
        }
    }

    [Fact]
    public void UnicodeFoldingPreservesTextAndEscapesPropertyInjection()
    {
        var label = string.Concat(Enumerable.Repeat("週😀é", 30)) + ",;\\\r\nInjected: {0}";
        var text = CalendarExport.Generate(new(CalendarExportScope.Week, new(2026, 9, 7)), Iso, Region, Region,
            label, "ISO", DateTimeOffset.UtcNow);

        Assert.All(text.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75));
        Assert.DoesNotContain("\r\nInjected:", text, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:" + CalendarExport.Escape(string.Format(Region, label, "37")), Unfold(text), StringComparison.Ordinal);
        Assert.DoesNotContain('\n', text.Replace("\r\n", "", StringComparison.Ordinal));

        var output = Environment.GetEnvironmentVariable("VOLTURA_CALENDAR_REVIEW");

        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "unicode.ics"), text);
        }
    }

    [Fact]
    public void FailedReplacementPreservesExistingFileAndCleansTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "weeknumber-export-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "calendar.ics");

        try
        {
            File.WriteAllText(path, "original");

            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var error = Record.Exception(() => CalendarExport.Save(path, "replacement"));

                Assert.True(error is IOException or UnauthorizedAccessException);
            }

            Assert.Equal("original", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
            CalendarExport.Save(path, "replacement");
            Assert.Equal("replacement", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string Generate(CalendarExportRequest request) => CalendarExport.Generate(request, Iso, Region, Region,
        "Week {0}", "ISO 8601", new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.FromHours(2)));
    private static string Unfold(string value) => value.Replace("\r\n ", "", StringComparison.Ordinal);
}
