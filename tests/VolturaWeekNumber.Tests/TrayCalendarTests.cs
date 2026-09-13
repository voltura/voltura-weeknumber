using System.Globalization;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;
using Drawing = System.Drawing;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class TrayCalendarTests(WpfTestFixture fixture)
{
    [Fact]
    public void MutedMonthsOpenTheirActualYear() => fixture.Run(() =>
    {
        var model = Create();

        model.ZoomOut();
        Assert.Equal(16, model.Items.Count);

        var january = model.Items[12];

        Assert.True(january.IsMuted);
        Assert.True(january.IsEnabled);
        model.Select(january);
        Assert.True(model.IsMonth);
        Assert.Equal(new DateOnly(2027, 1, 1), model.Anchor);
        model.Today();
        Assert.Equal(new DateOnly(2026, 9, 1), model.Anchor);
    });

    [Theory]
    [InlineData(0, 2018)]
    [InlineData(15, 2033)]
    public void MutedYearsOpenTheirActualMonthPicker(int index, int year) => fixture.Run(() =>
    {
        var model = Create();

        model.ZoomOut();
        model.ZoomOut();
        Assert.Equal("2020–2029", model.Heading);
        Assert.False(model.CanZoomOut);

        var item = model.Items[index];

        Assert.True(item.IsMuted);
        Assert.True(item.IsEnabled);
        model.Select(item);
        Assert.Equal(TrayCalendarView.Year, model.View);
        Assert.Equal(year, model.Anchor.Year);
        Assert.Equal(new DateOnly(year, 1, 1), model.Items[0].Date);
    });

    [Fact]
    public void NavigationMovesMonthsYearsAndDecadesAndTodayResetsZoom() => fixture.Run(() =>
    {
        var model = Create();

        model.Move(1);
        Assert.Equal(10, model.Anchor.Month);
        model.ZoomOut();
        model.Move(-1);
        Assert.Equal(2025, model.Anchor.Year);
        model.ZoomOut();
        model.Move(1);
        Assert.Equal("2030–2039", model.Heading);
        Assert.Equal(2028, model.Items[0].Date!.Value.Year);
        model.Move(-1);
        Assert.Equal("2020–2029", model.Heading);
        model.Today();
        Assert.True(model.IsMonth);
        Assert.Equal(new DateOnly(2026, 9, 1), model.Anchor);
    });

    [Fact]
    public void CalendarPreferencesAndRefreshPreserveBrowsingAndSplitWeeks() => fixture.Run(() =>
    {
        var model = Create();

        model.Refresh(new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay), new(2000, 12, 15));
        model.Today();
        Assert.Equal([54, 1], model.Month.Weeks[^1].Week.Numbers);
        Assert.Equal(DayOfWeek.Sunday, model.Month.Weeks[0].Week.Range.Start.DayOfWeek);
        model.Move(-1);
        model.Refresh(new(CalendarMode.Iso), new(2001, 1, 1));
        Assert.Equal(new DateOnly(2000, 11, 1), model.Anchor);
        Assert.All(model.Month.Weeks, week => Assert.Equal(DayOfWeek.Monday, week.Week.Range.Start.DayOfWeek));
        model.ZoomOut();
        model.Refresh(new(CalendarMode.Iso), new(2001, 1, 2));
        Assert.Equal(TrayCalendarView.Year, model.View);
    });

    [Fact]
    public void BoundsRemainSafeAtBothEndsOfSupportedDates() => fixture.Run(() =>
    {
        foreach (var today in new[] { DateOnly.MinValue, DateOnly.MaxValue })
        {
            var model = Create();

            model.Refresh(new(CalendarMode.Iso), today);
            model.Today();
            Assert.Equal(today.Year > 1, model.CanPrevious);
            Assert.Equal(today.Year < 9999, model.CanNext);
            model.ZoomOut();

            if (today.Year == 9999)
            {
                Assert.All(model.Items.Skip(12), item => Assert.False(item.IsEnabled));
            }

            model.ZoomOut();
            Assert.Contains(model.Items, item => !item.IsEnabled);
            Assert.All(model.Items.Where(item => !item.IsEnabled), item => Assert.Null(item.Date));
        }
    });

    [Theory]
    [InlineData(0, 0, 1920, 1080, 1860, 1080)]
    [InlineData(-1920, 0, 1920, 1080, -60, 1080)]
    [InlineData(0, 40, 1920, 1040, 1800, 0)]
    [InlineData(0, 0, 320, 300, 310, 300)]
    public void PlacementStaysInsideTheTaskbarMonitor(int x, int y, int w, int h, int ax, int ay)
    {
        var work = new Drawing.Rectangle(x, y, w, h);
        var bounds = TrayCalendarPlacement.CalculateBounds(work, new(ax, ay, 20, 20), 440, 456, 8);

        Assert.True(work.Contains(bounds));
    }

    private static TrayCalendarViewModel Create()
    {
        Strings.Current.SetLanguage("en");

        var model = new TrayCalendarViewModel();

        model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
        model.Today();

        return model;
    }
}
