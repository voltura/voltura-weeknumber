using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed partial class CalendarManagerInteractionTests(WpfTestFixture fixture)
{
    [Fact]
    public async Task ManagerKeepsNativeCalendarEnabledAndOutsideActivationDismissesGroup()
    {
        using var store = new ImportedCalendarStore(Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N")));

        await store.LoadAsync();

        TrayCalendarWindow? tray = null;
        Window? outside = null;

        try
        {
            fixture.Run(() =>
            {
                tray = new TrayCalendarWindow();
                tray.SetImportStore(store);
                tray.Model.Refresh(new(), new(2026, 9, 14));
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
                tray.Open(new(700, 800, 20, 20));

                var returned = false;
                var enteredModalLoop = false;

                _ = tray.Dispatcher.InvokeAsync(() =>
                {
                    if (!returned)
                    {
                        enteredModalLoop = true;

                        foreach (var blockedManager in tray.OwnedWindows.OfType<ImportedCalendarsWindow>().ToArray())
                        {
                            blockedManager.Close();
                        }
                    }
                });

                var opening = CalendarImportActions.ManageAsync(tray);

                returned = true;
                Assert.False(enteredModalLoop);

                Assert.True(opening.IsCompletedSuccessfully);

                var manager = Assert.Single(tray.OwnedWindows.OfType<ImportedCalendarsWindow>());

                Assert.Equal("Calendars", manager.Title);
                Assert.True(manager.Topmost);
                Assert.True(IsWindowEnabled(new WindowInteropHelper(tray).Handle));
                Assert.True(IsWindowEnabled(new WindowInteropHelper(manager).Handle));

                tray.Activate();
                ((Button)tray.FindName("NextButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(10, tray.Model.Anchor.Month);
                ((Button)tray.FindName("PreviousButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(9, tray.Model.Anchor.Month);
                Assert.True(CalendarImportActions.ManageAsync(tray).IsCompletedSuccessfully);
                Assert.Same(manager, Assert.Single(tray.OwnedWindows.OfType<ImportedCalendarsWindow>()));

                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = false;
                manager.Activate();
                tray.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.True(tray.IsVisible);
                Assert.True(manager.IsVisible);
                Assert.Equal(WindowStyle.SingleBorderWindow, manager.WindowStyle);

                outside = new Window { Width = 150, Height = 100, Topmost = true, ShowInTaskbar = false };

                outside.Show();
                outside.Activate();
                tray.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.False(tray.IsVisible);
                Assert.False(manager.IsVisible);

                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = true;
                tray.Open(new(700, 800, 20, 20));
                Assert.True(CalendarImportActions.ManageAsync(tray).IsCompletedSuccessfully);
                Assert.Same(manager, Assert.Single(tray.OwnedWindows.OfType<ImportedCalendarsWindow>()));
                outside.Activate();
                tray.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.True(tray.IsVisible);
                Assert.True(manager.IsVisible);
                ((ToggleButton)tray.FindName("CalendarPin")).IsChecked = false;
                manager.Activate();
                manager.Close();
                tray.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.False(manager.IsVisible);
                Assert.True(tray.IsVisible);
            });
        }
        finally
        {
            fixture.Run(() =>
            {
                outside?.Close();
                tray?.Exit();
            });

            if (tray is not null)
            {
                await tray.DrainEventQueriesAsync();
            }
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowEnabled(nint handle);
}
