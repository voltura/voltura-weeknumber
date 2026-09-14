using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class CalendarImportUiTests(WpfTestFixture fixture)
{
    [Fact]
    public void EventDetailsPageLargeDaysAndReleaseHiddenContent() => fixture.Run(() =>
    {
        var tray = new TrayCalendarWindow();
        CalendarEventsWindow? events = null;

        try
        {
            ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
            tray.Open(new(700, 800, 20, 20));
            events = new CalendarEventsWindow(tray);

            var date = new DateOnly(2026, 9, 14);
            var items = Enumerable.Range(0, 25).Select(index => new ImportedOccurrence(Guid.NewGuid(),
                "Source", "Event " + index, "", "", date.ToDateTime(TimeOnly.MinValue),
                date.ToDateTime(TimeOnly.MinValue), false)).ToArray();

            events.Display(date, items);
            events.ShowDetails();
            events.UpdateLayout();

            var scroll = Descendants(events).OfType<System.Windows.Controls.ScrollViewer>()
                .Single(item => item.Content is System.Windows.Controls.StackPanel);
            var list = (System.Windows.Controls.StackPanel)scroll.Content;
            var next = Descendants(events).OfType<System.Windows.Controls.Button>()
                .Single(button => System.Windows.Automation.AutomationProperties.GetName(button) == Strings.Current["NextPage"]);

            Assert.Equal(10, list.Children.Count);
            next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(10, list.Children.Count);
            next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(5, list.Children.Count);
            Assert.False(next.IsEnabled);
            events.Hide();
            Assert.Empty(list.Children);
        }
        finally
        {
            events?.Exit();
            tray.Exit();
        }
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ImportInformationUsesTheAppThemeAndClosesWithoutDismissingCalendar(bool dark, bool contrast) => fixture.Run(() =>
    {
        using var store = new ImportedCalendarStore(Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N")));
        var tray = new TrayCalendarWindow();
        ImportedCalendarsWindow? manager = null;

        try
        {
            ThemeManager.ApplyPalette(dark, contrast);
            ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
            tray.Open(new(700, 800, 20, 20));
            manager = new ImportedCalendarsWindow(store, null) { Owner = tray, Topmost = true };

            manager.Show();

            Exception? failure = null;
            var shown = false;

            _ = manager.Dispatcher.InvokeAsync(() =>
            {
                var message = manager.OwnedWindows.OfType<CalendarMessageWindow>().SingleOrDefault();

                try
                {
                    Assert.NotNull(message);
                    shown = true;
                    Assert.True(message.Topmost);
                    Assert.Equal(WindowStyle.None, message.WindowStyle);
                    Assert.Same(tray.FindResource("WindowBrush"), message.Background);
                    Assert.True(message.DismissButton.IsDefault);
                    Assert.True(message.DismissButton.IsCancel);
                    Capture(message, "import-message-" + (contrast
                        ? "contrast"
                        : dark
                            ? "dark"
                            : "light"));
                    message.DismissButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
                catch (Exception error)
                {
                    failure = error;
                }
                finally
                {
                    if (message?.IsVisible == true)
                    {
                        message.Close();
                    }
                }
            });
            new CalendarImportDialog().ShowError(manager, Strings.Current["CalendarAlreadyImported"]);
            Assert.Null(failure);
            Assert.True(shown);
            Assert.True(tray.IsVisible);
            Assert.True(manager.IsVisible);
        }
        finally
        {
            manager?.Close();
            tray.Exit();
            ThemeManager.ApplyPalette(false, false);
        }
    });

    [Fact]
    public void EventDotHasVisibleClearanceFromSelectedDateCircle() => fixture.Run(() =>
    {
        var tray = new TrayCalendarWindow();

        try
        {
            ThemeManager.ApplyPalette(true, false);
            ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
            tray.Model.Refresh(new(), new(2026, 10, 14));
            tray.Open(new(700, 800, 20, 20));

            var date = new DateOnly(2026, 10, 31);
            var day = tray.Model.Month.Weeks.SelectMany(week => week.Days).Single(day => day.Date == date);

            day.SetEventCount(1);
            tray.SelectedDate = date;
            tray.UpdateLayout();

            var button = Descendants(tray).OfType<System.Windows.Controls.RadioButton>()
                .Single(button => ReferenceEquals(button.DataContext, day));
            var dot = (FrameworkElement)button.Template.FindName("EventDot", button);
            var center = dot.TranslatePoint(new Point(dot.ActualWidth / 2, dot.ActualHeight / 2), button);
            var offset = center - new Point(button.ActualWidth / 2, button.ActualHeight / 2);

            Assert.True(offset.Length - button.ActualWidth / 2 - dot.ActualWidth / 2 >= 2);
            Capture(tray, "tray-events-dot-clearance-dark");
        }
        finally
        {
            tray.Exit();
            ThemeManager.ApplyPalette(false, false);
        }
    });

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void DetailsAndManagerRenderLongLocalizedContent(bool dark, bool contrast) => fixture.Run(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        var path = Path.Combine(root, "arbetskalender.ics");

        File.WriteAllText(path, CalendarImportTests.Calendar(CalendarImportTests.Meeting).Replace("X-WR-CALNAME:Team", "X-WR-CALNAME:Arbetskalender med ett längre namn", StringComparison.Ordinal));

        using var store = new ImportedCalendarStore(root);

        Task.Run(() => store.ImportAsync(path), TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        var tray = new TrayCalendarWindow();
        CalendarEventsWindow? events = null;
        ImportedCalendarsWindow? manager = null;

        try
        {
            Strings.Current.SetLanguage("sv");
            ThemeManager.ApplyPalette(dark, contrast);
            ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
            tray.Open(new(700, 800, 20, 20));
            events = new CalendarEventsWindow(tray);
            events.Display(new(2026, 9, 14), Enumerable.Range(0, 8).Select(index => new ImportedOccurrence(Guid.NewGuid(),
                "Arbetskalender med ett längre namn", "Planering och genomgång av kommande aktiviteter", "Stockholm", string.Join("\n", Enumerable.Repeat("Längre beskrivning med detaljer för mötet.", 5)),
                new DateTime(2026, 9, 14, index + 8, 0, 0), new DateTime(2026, 9, 14, index + 9, 0, 0), false)).ToArray());
            events.Show();
            events.UpdateLayout();
            Assert.Equal(tray.FindResource("WindowBrush"), events.Background);

            var suffix = contrast
                ? "contrast"
                : dark
                    ? "dark"
                    : "light";

            Capture(events, "tray-events-long-" + suffix);
            manager = new ImportedCalendarsWindow(store, null) { Owner = tray };

            manager.Show();
            Capture(manager, "import-manager-" + suffix);
        }
        finally
        {
            manager?.Close();
            events?.Exit();
            tray.Exit();
            Strings.Current.SetLanguage("en");
            ThemeManager.ApplyPalette(false, false);
            Directory.Delete(root, true);
        }
    });

    [Fact]
    public async Task DateToggleNavigationPinMoveAndRemovalUpdateBothWindows()
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        using var store = new ImportedCalendarStore(root);
        TrayCalendarWindow? tray = null;
        Task pending = Task.CompletedTask;

        try
        {
            var path = Path.Combine(root, "team.ics");

            await File.WriteAllTextAsync(path, CalendarImportTests.Calendar(CalendarImportTests.Meeting.Replace("SUMMARY:Planning", "RRULE:FREQ=DAILY;COUNT=3\nSUMMARY:Planning", StringComparison.Ordinal)), TestContext.Current.CancellationToken);
            await store.ImportAsync(path);
            fixture.Run(() =>
            {
                tray = new TrayCalendarWindow();
                tray.SetImportStore(store);
                tray.Model.Refresh(new(), new(2026, 9, 14));
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
                tray.Open(new(700, 800, 20, 20));
                pending = tray.EventRefresh;
            });
            await pending;
            fixture.Run(() =>
            {
                Assert.True(tray!.Model.Month.Weeks.SelectMany(week => week.Days).Single(day => day.Date == new DateOnly(2026, 9, 14)).HasEvents);
                tray.SelectEventDate(new(2026, 9, 14));

                var events = Assert.Single(tray.OwnedWindows.OfType<CalendarEventsWindow>());

                Assert.True(events.IsVisible);
                Assert.Equal(new DateOnly(2026, 9, 14), events.Date);
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = false;
                events.Activate();
                Assert.Equal("Close", System.Windows.Automation.AutomationProperties.GetName(events.CloseButton));
                Assert.Equal("Close", events.CloseButton.ToolTip);
                events.CloseButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                tray.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.False(events.IsVisible);
                Assert.True(tray.IsVisible);
                tray.SelectEventDate(new(2026, 9, 14));
                Assert.True(events.IsVisible);
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
                tray.SelectEventDate(new(2026, 9, 15));
                Assert.Equal(new DateOnly(2026, 9, 15), events.Date);
                tray.SelectEventDate(new(2026, 9, 15));
                Assert.False(events.IsVisible);
                tray.SelectEventDate(new(2026, 9, 14));
                tray.SelectEventDate(new(2026, 9, 20));
                Assert.False(events.IsVisible);
                tray.SelectEventDate(new(2026, 9, 14));
                tray.Left += 10;
                Assert.False(events.IsVisible);
                tray.SelectEventDate(new(2026, 9, 14));
                tray.DismissOnDeactivate();
                Assert.True(events.IsVisible);
                Capture(tray, "tray-events-calendar");
                Capture(events, "tray-events-details");
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = false;
                events.Activate();
                tray.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.True(tray.IsVisible);
                Assert.True(events.IsVisible);
                tray.Activate();
                tray.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.True(events.IsVisible);
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
                events.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(events), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert.False(tray.IsVisible);
                Assert.False(events.IsVisible);
                tray.Open(new(700, 800, 20, 20));
                pending = tray.EventRefresh;
            });
            await pending;
            fixture.Run(() =>
            {
                tray!.SelectEventDate(new(2026, 9, 14));
                Assert.True(Assert.Single(tray.OwnedWindows.OfType<CalendarEventsWindow>()).IsVisible);
            });
            await store.RemoveAsync(store.Sources[0].Id);
            fixture.Run(() =>
            {
                tray!.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                pending = tray.EventRefresh;
            });
            await pending;
            fixture.Run(() =>
            {
                Assert.False(Assert.Single(tray!.OwnedWindows.OfType<CalendarEventsWindow>()).IsVisible);
                Assert.All(tray.Model.Month.Weeks.SelectMany(week => week.Days), day => Assert.False(day.HasEvents));
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = false;
                tray.DismissOnDeactivate();
                Assert.False(tray.IsVisible);
            });
        }
        finally
        {
            fixture.Run(() => tray?.Exit());
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EventPlacementUsesEitherSideAndClampsSmallWorkAreas()
    {
        var work = new System.Drawing.Rectangle(0, 0, 1920, 1080);

        Assert.Equal(548, TrayCalendarPlacement.CalculateEventBounds(work, new(100, 100, 440, 456), 360, 456, 8).X);
        Assert.Equal(1112, TrayCalendarPlacement.CalculateEventBounds(work, new(1480, 624, 440, 456), 360, 456, 8).X);

        var small = new System.Drawing.Rectangle(-400, 0, 400, 300);
        var bounds = TrayCalendarPlacement.CalculateEventBounds(small, new(-400, 0, 400, 300), 540, 684, 12);

        Assert.Equal(small, bounds);
    }

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("VOLTURA_CALENDAR_REVIEW") is not { Length: > 0 } root)
        {
            return;
        }

        Directory.CreateDirectory(root);
        window.UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        bitmap.Render(window);

        var encoder = new PngBitmapEncoder();

        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var file = File.Create(Path.Combine(root, name + ".png"));

        encoder.Save(file);
    }
}
