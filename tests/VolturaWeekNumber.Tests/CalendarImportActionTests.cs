using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class CalendarImportActionTests(WpfTestFixture fixture)
{
    [Fact]
    public async Task SuccessfulImportAndReplacementReportSuccessAfterSaving()
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        await using var store = new ImportedCalendarStore(root);
        Window? owner = null;

        try
        {
            var path = Path.Combine(root, "team.ics");

            await File.WriteAllTextAsync(path, CalendarImportTests.Calendar(CalendarImportTests.Meeting), TestContext.Current.CancellationToken);

            var dialog = new ImportDialog(path);
            Task action = Task.CompletedTask;

            fixture.Run(() =>
            {
                owner = new Window();
                owner.SetValue(CalendarImportActions.StoreProperty, store);
                action = CalendarImportActions.ImportAsync(owner, dialog: dialog);
            });
            await action;
            Assert.Equal(Strings.Current["CalendarImportSuccessful"], dialog.Success);
            Assert.Null(dialog.Error);

            var id = Assert.Single(store.Sources).Id;

            Assert.True(File.Exists(Path.Combine(root, "Calendars", id + ".json")));
            await File.WriteAllTextAsync(path, CalendarImportTests.Calendar(CalendarImportTests.Meeting.Replace("Planning", "Updated", StringComparison.Ordinal)), TestContext.Current.CancellationToken);
            dialog.Success = null;
            fixture.Run(() => action = CalendarImportActions.ImportAsync(owner!, id, dialog));
            await action;
            Assert.Equal(Strings.Current["CalendarImportSuccessful"], dialog.Success);
            Assert.Equal(id, Assert.Single(store.Sources).Id);
        }
        finally
        {
            fixture.Run(() => owner?.Close());
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("same")]
    [InlineData("invalid")]
    public async Task RejectedReplacementIsReportedByUiWithoutChangingEitherCalendar(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        using var store = new ImportedCalendarStore(root);
        Window? owner = null;

        try
        {
            var first = Path.Combine(root, "first.ics");
            var second = Path.Combine(root, "second.ics");
            var invalid = Path.Combine(root, "invalid.ics");

            await File.WriteAllTextAsync(first, CalendarImportTests.Calendar(CalendarImportTests.Meeting), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(second, CalendarImportTests.Calendar(CalendarImportTests.Meeting.Replace("Planning", "Other calendar", StringComparison.Ordinal)), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(invalid, "invalid calendar", TestContext.Current.CancellationToken);
            await store.ImportAsync(first);
            await store.ImportAsync(second);

            var before = store.Sources.ToArray();
            var dialog = new ImportDialog(scenario == "duplicate"
                ? second
                : scenario == "same"
                    ? first
                    : invalid);
            Task action = Task.CompletedTask;

            fixture.Run(() =>
            {
                owner = new Window();
                owner.SetValue(CalendarImportActions.StoreProperty, store);
                action = CalendarImportActions.ImportAsync(owner, before[0].Id, dialog);
            });
            await action;
            Assert.Equal(Strings.Current[scenario == "invalid"
                ? "CalendarImportInvalid"
                : "CalendarAlreadyImported"], dialog.Error);
            Assert.Equal(before, store.Sources);
            Assert.Null(dialog.Success);

            using var reopened = new ImportedCalendarStore(root);

            await reopened.LoadAsync();
            Assert.Equal(before.OrderBy(source => source.Id), reopened.Sources.OrderBy(source => source.Id));
        }
        finally
        {
            fixture.Run(() => owner?.Close());
            Directory.Delete(root, true);
        }
    }

    private sealed class ImportDialog(string path) : ICalendarImportDialog
    {
        internal string? Error { get; private set; }
        internal string? Success { get; set; }
        public string? ChoosePath(Window owner) => path;
        public void ShowError(Window owner, string message) => Error = message;
        public void ShowSuccess(Window owner, string message) => Success = message;
    }
}
