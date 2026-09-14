using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolturaWeekNumber.Features.Calendar;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class CalendarImportSafetyTests
{
    [Fact]
    public void ExpandedYearlySetPositionIsRejectedBeforeCandidateAllocation()
    {
        var rule = "RRULE:FREQ=YEARLY;BYMONTH=" + string.Join(',', Enumerable.Range(1, 12))
            + ";BYMONTHDAY=" + string.Join(',', Enumerable.Range(1, 31))
            + ";BYHOUR=" + string.Join(',', Enumerable.Range(0, 24))
            + ";BYMINUTE=" + string.Join(',', Enumerable.Range(0, 60))
            + ";BYSECOND=" + string.Join(',', Enumerable.Range(0, 60)) + ";BYSETPOS=-1";
        var source = CalendarImport.Parse(CalendarImportTests.Calendar(
            "BEGIN:VEVENT\nUID:expanded\nDTSTART:20260101T000000Z\n" + rule + "\nEND:VEVENT"), "expanded.ics");
        var error = Assert.Throws<InvalidDataException>(() => CalendarImport.Query([source],
            new(2026, 9, 1), new(2026, 9, 30), TimeZoneInfo.Utc, CancellationToken.None));

        Assert.Equal("CalendarImportLimit", error.Message);
    }

    [Fact]
    public void MissingFileNameFailsAsInvalidData()
    {
        var content = CalendarImportTests.Calendar(CalendarImportTests.Meeting)
            .Replace("X-WR-CALNAME:Team\r\n", "", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => CalendarImport.Parse(content, null!));
    }

    [Fact]
    public async Task DamagedSourcesDoNotBlockHealthySourcesOrRemoval()
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "Calendars");

        Directory.CreateDirectory(directory);

        try
        {
            var healthy = CalendarImport.Parse(CalendarImportTests.Calendar(CalendarImportTests.Meeting), "healthy.ics");

            await File.WriteAllTextAsync(Path.Combine(directory, healthy.Id + ".json"), JsonSerializer.Serialize(healthy), TestContext.Current.CancellationToken);

            var missingName = healthy with { Id = Guid.NewGuid(), FileName = null! };

            await File.WriteAllTextAsync(Path.Combine(directory, missingName.Id + ".json"), JsonSerializer.Serialize(missingName), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "broken.json"), "{", TestContext.Current.CancellationToken);

            await using var store = new ImportedCalendarStore(root);

            await store.LoadAsync();
            Assert.Equal(healthy.Id, Assert.Single(store.Sources).Id);
            Assert.Equal(2, store.FailedSources.Count);
            Assert.Single(await store.QueryAsync(new(2026, 9, 14), new(2026, 9, 14), TestContext.Current.CancellationToken));
            await store.RemoveFailedAsync("broken.json");
            await store.RemoveFailedAsync(missingName.Id + ".json");
            await store.RemoveAsync(healthy.Id);
            Assert.Empty(store.FailedSources);
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ShutdownCancelsAndWaitsForOutstandingOperations()
    {
        var store = new ImportedCalendarStore(Path.GetTempPath());
        using var operation = store.BeginOperation();
        var cancellation = store.ShutdownToken;
        var disposal = store.DisposeAsync().AsTask();

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(disposal.IsCompleted);
        Assert.Throws<OperationCanceledException>(() => store.BeginOperation());
        operation.Dispose();
        await disposal;
        await store.DisposeAsync();
    }
}
