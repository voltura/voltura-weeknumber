using System.Reflection;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class TrayCalendarRuntimeTests(WpfTestFixture fixture)
{
    [Fact]
    public void NativeSelectionTogglesCalendarWithoutAlsoOpeningMainWindow() => fixture.Run(() =>
    {
        using var tray = new NativeTray(false, new InMemoryHotKeyApi());
        var toggles = 0;
        var opens = 0;
        var resets = 0;

        tray.CalendarToggleRequested += _ => toggles++;
        tray.OpenRequested += () => opens++;
        tray.CalendarPointerIdle += () => resets++;

        var procedure = typeof(NativeTray).GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance)!;

        void Send(int code) => procedure.Invoke(tray, [nint.Zero, 0x8001, nint.Zero, new nint(code), false]);
        Send(0x201);
        Send(0x400);
        Assert.Equal(1, toggles);
        Send(0x203);
        Assert.Equal(1, toggles);
        Send(0x400);
        Assert.Equal(2, toggles);
        Send(0x401);
        Assert.Equal(3, toggles);
        Assert.Equal(1, resets);
        Assert.Equal(0, opens);
    });

    [Fact]
    public async Task RuntimeToggleHidesReopensAndConsumesTrayDismissalWithoutChangingMainCalendar()
    {
        AppRuntime? runtime = null;
        Task cleanup = Task.CompletedTask;

        try
        {
            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N")), false, true));

                var mainBrowser = runtime.Model.CalendarBrowser;

                mainBrowser.ShowMonth(new(2024, 2, 1));

                var toggle = typeof(AppRuntime).GetMethod("ToggleCalendar", BindingFlags.NonPublic | BindingFlags.Instance)!;

                void Click() => toggle.Invoke(runtime, [new System.Drawing.Rectangle(1000, 900, 20, 20)]);
                Click();

                var flyout = (TrayCalendarWindow)typeof(AppRuntime).GetField("_calendarFlyout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(runtime)!;

                Assert.True(flyout.IsVisible);
                flyout.Model.ZoomOut();
                Click();
                Assert.False(flyout.IsVisible);
                Click();
                Assert.True(flyout.IsVisible);
                Assert.True(flyout.Model.IsMonth);
                Assert.Equal(new DateOnly(2024, 2, 1), mainBrowser.Anchor);
                Assert.False(runtime.Window.IsVisible);
                flyout.Hide();
                typeof(AppRuntime).GetField("_calendarDismissedByTrayClick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(runtime, true);
                Click();
                Assert.False(flyout.IsVisible);
                Click();
                Assert.True(flyout.IsVisible);
            });
        }
        finally
        {
            if (runtime is not null)
            {
                fixture.Run(() => cleanup = runtime.DisposeAsync().AsTask());
            }

            await cleanup;
        }
    }
}
