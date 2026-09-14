using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Ui;
using Xunit;
using CalendarMode = VolturaWeekNumber.Features.Calendar.CalendarMode;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class CalendarExportUiTests(WpfTestFixture fixture)
{
    [Fact]
    public void ActiveViewsAndContextTargetsResolveWithoutNavigation() => fixture.Run(() =>
    {
        var tray = new TrayCalendarViewModel();

        tray.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
        tray.Today();
        Assert.Equal(CalendarExportScope.Month, tray.ExportRequest.Scope);
        tray.ZoomOut();
        Assert.Equal(CalendarExportScope.Year, tray.ExportRequest.Scope);

        var month = tray.Items[12];
        var requests = CalendarExportActions.Requests("Picker", month, tray.Anchor, false);

        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal(2027, request.Anchor.Year));
        tray.ZoomOut();
        Assert.False(tray.CanExport);
        Assert.Single(CalendarExportActions.Requests("Picker", tray.Items[3], tray.Anchor, true));

        var browser = tray.Month;

        browser.ShowMonth(new(2026, 9, 1));

        var row = browser.Weeks[0];
        var rowRequests = CalendarExportActions.Requests("Week", row, browser.Anchor, false);

        Assert.Equal(new DateOnly(2026, 9, 1), rowRequests[1].Anchor);
        Assert.Equal(new DateOnly(2026, 8, 31), rowRequests[2].Anchor);

        var dayRequests = CalendarExportActions.Requests("Day", row.Days[0], browser.Anchor, false);

        Assert.Equal(8, dayRequests[1].Anchor.Month);
        Assert.True(browser.IsMonth);
        browser.ShowWeek(row);
        Assert.Equal(CalendarExportScope.Week, browser.ExportRequest.Scope);
        Assert.Equal(new DateOnly(2026, 8, 31), browser.ExportRequest.Anchor);
        browser.ZoomOut(CalendarZoom.Year);
        Assert.Equal(CalendarExportScope.Year, browser.ExportRequest.Scope);
    });

    [Fact]
    public void FloatingTodayMatchesMainGlyphAndExportIsLeftOfToday() => fixture.Run(() =>
    {
        var tray = new TrayCalendarWindow();

        try
        {
            tray.Show();
            tray.UpdateLayout();

            var today = (Button)tray.FindName("TodayButton");
            var export = (Button)tray.FindName("ExportButton");

            Assert.Contains(Descendants(today).OfType<TextBlock>(), text => text.Text == "\uE8D1");
            Assert.Contains(Descendants(export).OfType<TextBlock>(), text => text.Text == "\uEDE1");
            Assert.True(export.TranslatePoint(new Point(), tray).X + export.ActualWidth <= today.TranslatePoint(new Point(), tray).X);
            tray.Model.ZoomOut();
            tray.Model.ZoomOut();
            tray.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, export.Visibility);
        }
        finally
        {
            tray.Exit();
        }
    });

    [Fact]
    public void UnpinnedContextMenuKeepsCalendarVisibleAndPreservesSelection() => fixture.Run(() =>
    {
        var tray = new TrayCalendarWindow();

        try
        {
            tray.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
            tray.Model.Today();
            tray.SelectedDate = new(2026, 9, 13);
            tray.Show();
            tray.UpdateLayout();

            var day = Descendants(tray).OfType<RadioButton>()
                .First(button => button.DataContext is CalendarDayItem { Date: { Year: 2026, Month: 8, Day: 31 } });

            Assert.True(day.Focus());
            CalendarExportActions.OpenContextMenu(day, true);

            var menu = day.ContextMenu;

            Assert.True(menu.IsOpen);
            Assert.Equal(System.Windows.Controls.Primitives.PlacementMode.Bottom, menu.Placement);
            Assert.Equal(3, menu.Items.Count);
            Assert.Contains("August", ((MenuItem)menu.Items[1]).Header.ToString(), StringComparison.Ordinal);
            tray.DismissOnDeactivate();
            Assert.True(tray.IsVisible);
            Assert.False(tray.IsPinned);
            Assert.Equal(new DateOnly(2026, 9, 13), tray.SelectedDate);
            Assert.Equal(new DateOnly(2026, 9, 1), tray.Model.Anchor);
            menu.IsOpen = false;
            tray.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            tray.DismissOnDeactivate();
            Assert.False(tray.IsVisible);
        }
        finally
        {
            tray.Exit();
        }
    });

    [Fact]
    public void CancelAndFailureKeepScopeAndPinStateAndUseInitiatingOwner() => fixture.Run(() =>
    {
        var tray = new TrayCalendarWindow();

        tray.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
        tray.Model.Today();

        var date = new DateOnly(2026, 9, 11);

        tray.SelectedDate = date;
        tray.Show();

        var pin = (System.Windows.Controls.Primitives.ToggleButton)tray.FindName("CalendarPin");

        pin.IsChecked = true;

        try
        {
            var dialog = new FakeDialog(owner =>
            {
                Assert.Same(tray, owner);
                tray.DismissOnDeactivate();
                Assert.True(tray.IsVisible);

                return null;
            });

            CalendarExportActions.Export(tray, tray.Model.ExportRequest, tray.Model.Month.Options, dialog);
            Assert.Null(dialog.Error);
            Assert.Equal(date, tray.SelectedDate);
            Assert.True(tray.IsPinned);

            var failure = new FakeDialog(_ => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "calendar.ics"));

            CalendarExportActions.Export(tray, tray.Model.ExportRequest, tray.Model.Month.Options, failure);
            Assert.Equal(Strings.Current["CalendarExportFailed"], failure.Error);
            Assert.True(tray.IsPinned);
            pin.IsChecked = false;

            using (tray.BeginCalendarInteraction())
            {
                tray.DismissOnDeactivate();
                Assert.True(tray.IsVisible);
            }

            tray.DismissOnDeactivate();
            Assert.False(tray.IsVisible);
        }
        finally
        {
            tray.Exit();
        }
    });

    private sealed class FakeDialog(Func<Window, string?> choose) : ICalendarExportDialog
    {
        internal string? Error { get; private set; }
        public string? ChoosePath(Window owner, CalendarExportRequest request) => choose(owner);
        public void ShowError(Window owner, string message) => Error = message;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
