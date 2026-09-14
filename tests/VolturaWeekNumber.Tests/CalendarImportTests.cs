using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VolturaWeekNumber.Features.Calendar;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class CalendarImportTests
{
    private static readonly int[] DstHours = [7, 8, 8];
    [Fact]
    public void EventCrossingAutumnClockChangeKeepsItsUtcEndpoints()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:fold\nDTSTART:20261025T005000Z\nDTEND:20261025T011000Z\nSUMMARY:Clock change\nEND:VEVENT"), "fold.ics");
        var utc = Assert.Single(CalendarImport.Query([source], new(2026, 10, 25), new(2026, 10, 25), TimeZoneInfo.Utc, CancellationToken.None));

        Assert.Equal(TimeSpan.FromMinutes(20), utc.End - utc.Start);

        var local = Assert.Single(CalendarImport.Query([source], new(2026, 10, 25), new(2026, 10, 25), TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm"), CancellationToken.None));

        Assert.Equal(new DateTime(2026, 10, 25, 2, 50, 0), local.Start);
        Assert.Equal(new DateTime(2026, 10, 25, 2, 10, 0), local.End);
        Assert.True(local.Includes(new(2026, 10, 25)));
    }

    [Theory]
    [InlineData("RRULE:FREQ=SECONDLY")]
    [InlineData("RRULE:FREQ=MINUTELY;UNTIL=20000102T000000Z")]
    [InlineData("RRULE:FREQ=HOURLY;COUNT=1000000")]
    [InlineData("RRULE:FREQ=DAILY\nEXRULE:FREQ=SECONDLY")]
    public async Task ExcessiveHistoricalTraversalFailsBeforeEnumeration(string rule)
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:old\nDTSTART:20000101T000000Z\n" + rule + "\nSUMMARY:Old\nEND:VEVENT"), "old.ics");
        var query = Task.Run(() => CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 14), TimeZoneInfo.Utc, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => query.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Equal("CalendarImportLimit", error.Message);
    }

    [Fact]
    public void LongRunningWeeklyRecurrenceStillWorks()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:weekly\nDTSTART:20000103T090000Z\nRRULE:FREQ=WEEKLY\nEND:VEVENT"), "weekly.ics");

        Assert.Single(CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 14), TimeZoneInfo.Utc, CancellationToken.None));
    }

    [Theory]
    [InlineData("20261025T011000Z", "20261025T005000Z")]
    [InlineData("20261025T025000", "20261025T021000")]
    public void ReversedEndpointsAreStillRejected(string start, string end)
    {
        Assert.Throws<InvalidDataException>(() => CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:reversed\nDTSTART:" + start + "\nDTEND:" + end + "\nEND:VEVENT"), "reversed.ics"));
    }

    [Fact]
    public void CancelledHistoricalQueryDoesNotStartEvaluation()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:old\nDTSTART:20000101T000000Z\nRRULE:FREQ=SECONDLY\nEND:VEVENT"), "old.ics");
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 14), TimeZoneInfo.Utc, cancellation.Token));
    }

    [Fact]
    public void UnknownZonesAndTruncatedFilesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => CalendarImport.Parse(Calendar(Meeting.Replace("DTSTART:", "DTSTART;TZID=Made/Up:", StringComparison.Ordinal)), "bad.ics"));
        Assert.Throws<InvalidDataException>(() => CalendarImport.Parse(Calendar(Meeting).Replace("END:VCALENDAR", "", StringComparison.Ordinal), "bad.ics"));
    }

    [Fact]
    public void UtcMidnightAndZeroDurationHaveCorrectDateMembership()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:night\nDTSTART:20260914T220000Z\nDTEND:20260915T000000Z\nSUMMARY:Night\nEND:VEVENT\nBEGIN:VEVENT\nUID:instant\nDTSTART:20260915T000000Z\nSUMMARY:Instant\nEND:VEVENT"), "night.ics");
        var items = CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 15), TimeZoneInfo.Utc, CancellationToken.None);

        Assert.False(items.Single(item => item.Title == "Night").Includes(new(2026, 9, 15)));
        Assert.True(items.Single(item => item.Title == "Instant").Includes(new(2026, 9, 15)));
    }

    [Fact]
    public void OutlookZoneUnicodeFoldingAndLongAdditionalPeriodAreSupported()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:outlook\nDTSTART;TZID=W. Europe Standard Time:20260914T090000\nSUMMARY:Möte\\, 東京\n  planering\nEND:VEVENT"), "outlook.ics");
        var item = Assert.Single(CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 14), TimeZoneInfo.Utc, CancellationToken.None));

        Assert.Equal(7, item.Start.Hour);
        Assert.Contains("Möte, 東京 planering", item.Title, StringComparison.Ordinal);

        var period = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:period\nDTSTART:20260101T090000Z\nDURATION:PT1H\nRDATE;VALUE=PERIOD:20260901T090000Z/20260920T090000Z\nEND:VEVENT"), "period.ics");

        Assert.Single(CalendarImport.Query([period], new(2026, 9, 14), new(2026, 9, 14), TimeZoneInfo.Utc, CancellationToken.None));
    }
    internal static string Calendar(string events) => "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Tests//EN\r\nX-WR-CALNAME:Team\r\n" + events.Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\nEND:VCALENDAR\r\n";
    internal const string Meeting = "BEGIN:VEVENT\nUID:meeting\nDTSTART:20260914T090000\nDTEND:20260914T100000\nSUMMARY:Planning\nDESCRIPTION:First line\\nSecond line\nLOCATION:Office\nEND:VEVENT";

    [Fact]
    public void FloatingTimesAndEscapedDescriptionsArePreserved()
    {
        var source = CalendarImport.Parse(Calendar(Meeting), "team.ics");
        var item = Assert.Single(CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 14), TimeZoneInfo.Utc, CancellationToken.None));

        Assert.Equal("Team", source.Name);
        Assert.Equal(9, item.Start.Hour);
        Assert.Equal("First line\nSecond line", item.Description);
        Assert.True(item.Includes(new(2026, 9, 14)));
        Assert.False(item.Includes(new(2026, 9, 15)));
    }

    [Fact]
    public void AllDaySpanHasExclusiveEndAndOverlapsQuery()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:trip\nDTSTART;VALUE=DATE:20260820\nDTEND;VALUE=DATE:20260916\nSUMMARY:Trip\nEND:VEVENT"), "trip.ics");
        var item = Assert.Single(CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 16), TimeZoneInfo.Utc, CancellationToken.None));

        Assert.True(item.AllDay);
        Assert.True(item.Includes(new(2026, 9, 15)));
        Assert.False(item.Includes(new(2026, 9, 16)));
    }

    [Fact]
    public void RecurrenceExclusionsOverridesAndCancellationAreApplied()
    {
        var content = Calendar(Meeting.Replace("SUMMARY:Planning", "RRULE:FREQ=DAILY;COUNT=5\nEXDATE:20260915T090000\nSUMMARY:Planning", StringComparison.Ordinal)
            + "\nBEGIN:VEVENT\nUID:meeting\nRECURRENCE-ID:20260916T090000\nDTSTART:20260916T110000\nDTEND:20260916T120000\nSUMMARY:Moved\nEND:VEVENT"
            + "\nBEGIN:VEVENT\nUID:meeting\nRECURRENCE-ID:20260917T090000\nDTSTART:20260917T090000\nSTATUS:CANCELLED\nEND:VEVENT");
        var source = CalendarImport.Parse(content, "team.ics");
        var items = CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 18), TimeZoneInfo.Utc, CancellationToken.None);

        Assert.Equal(3, items.Count);
        Assert.Contains(items, item => item.Title == "Moved" && item.Start.Hour == 11);
        Assert.DoesNotContain(items, item => item.Start.Day is 15 or 17);
    }

    [Fact]
    public void ZonedRecurrenceRetainsWallTimeAcrossDst()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:dst\nDTSTART;TZID=Europe/Stockholm:20261024T090000\nDTEND;TZID=Europe/Stockholm:20261024T100000\nRRULE:FREQ=DAILY;COUNT=3\nSUMMARY:Daily\nEND:VEVENT"), "dst.ics");
        var items = CalendarImport.Query([source], new(2026, 10, 24), new(2026, 10, 26), TimeZoneInfo.Utc, CancellationToken.None);

        Assert.Equal(DstHours, items.Select(item => item.Start.Hour));
    }

    [Fact]
    public void DurationSpansAndAdditionalDatesAreIncluded()
    {
        var source = CalendarImport.Parse(Calendar("BEGIN:VEVENT\nUID:duration\nDTSTART:20260912T090000Z\nDURATION:P3D\nRDATE:20260920T090000Z\nEND:VEVENT"), "duration.ics");

        Assert.Single(CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 14), TimeZoneInfo.Utc, CancellationToken.None));
        Assert.Single(CalendarImport.Query([source], new(2026, 9, 20), new(2026, 9, 20), TimeZoneInfo.Utc, CancellationToken.None));
    }

    [Fact]
    public void MalformedAndExcessiveInputFailExplicitly()
    {
        Assert.Throws<InvalidDataException>(() => CalendarImport.Parse("not a calendar", "bad.ics"));
        Assert.Throws<InvalidDataException>(() => CalendarImport.Parse(new string('a', CalendarImport.MaximumBytes + 1), "large.ics"));

        var source = CalendarImport.Parse(Calendar(Meeting.Replace("SUMMARY:Planning", "RRULE:FREQ=SECONDLY\nSUMMARY:Planning", StringComparison.Ordinal)), "large.ics");

        Assert.Throws<InvalidDataException>(() => CalendarImport.Query([source], new(2026, 9, 14), new(2026, 9, 15), TimeZoneInfo.Utc, CancellationToken.None));
    }

    [Fact]
    public async Task ImportReplaceRemovePersistAndFailuresPreserveData()
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        try
        {
            var path = Path.Combine(root, "team.ics");

            await File.WriteAllTextAsync(path, Calendar(Meeting), TestContext.Current.CancellationToken);

            using var store = new ImportedCalendarStore(root);

            await store.ImportAsync(path);

            var id = Assert.Single(store.Sources).Id;

            await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAsync(path));
            await File.WriteAllTextAsync(path, "bad", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAsync(path, id));
            Assert.Equal(id, Assert.Single(store.Sources).Id);
            await File.WriteAllTextAsync(path, Calendar(Meeting.Replace("Planning", "Updated", StringComparison.Ordinal)), TestContext.Current.CancellationToken);
            await store.ImportAsync(path, id);

            using var reopened = new ImportedCalendarStore(root);

            await reopened.LoadAsync();
            Assert.Equal(id, Assert.Single(reopened.Sources).Id);
            Assert.Contains("Updated", reopened.Sources[0].Content, StringComparison.Ordinal);
            await reopened.RemoveAsync(id);

            using var empty = new ImportedCalendarStore(root);

            await empty.LoadAsync();
            Assert.Empty(empty.Sources);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task WriteFailureDoesNotPublishImport()
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "Calendars"), "blocked", TestContext.Current.CancellationToken);

            var path = Path.Combine(root, "team.ics");

            await File.WriteAllTextAsync(path, Calendar(Meeting), TestContext.Current.CancellationToken);

            using var store = new ImportedCalendarStore(root);

            await Assert.ThrowsAsync<IOException>(() => store.ImportAsync(path));
            Assert.Empty(store.Sources);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
