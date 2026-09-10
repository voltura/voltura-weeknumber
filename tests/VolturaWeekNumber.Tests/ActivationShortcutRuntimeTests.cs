using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Platform;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class ActivationShortcutRuntimeTests(WpfTestFixture fixture)
{
    [Fact]
    public async Task ImmediateShortcutSavePreservesDraftAndActivatesNamedPagesInEveryWindowState()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        Task operation = Task.CompletedTask;
        var week = new ActivationShortcut(true, true, false, false, 0x57);
        var calendar = new ActivationShortcut(false, true, true, false, 0x43);

        try
        {
            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(root, false, true));
                operation = runtime.StartAsync(true);
            });
            await operation;

            fixture.Run(() => runtime!.Model.Editor.StartWithWindows = true);
            fixture.Run(() => operation = runtime!.ApplyReviewShortcutAsync(
                ActivationTarget.WeekNumber,
                week
            ));
            await operation;
            fixture.Run(() => operation = runtime!.ApplyReviewShortcutAsync(
                ActivationTarget.Calendar,
                calendar
            ));
            await operation;

            var saved = await SettingsStore.ReadAsync(Path.Combine(root, "settings.json"));

            Assert.Equal(week, saved.WeekNumberShortcut);
            Assert.Equal(calendar, saved.CalendarShortcut);
            Assert.False(saved.StartWithWindows);

            fixture.Run(() =>
            {
                Assert.True(runtime!.Model.Editor.StartWithWindows);
                Assert.Equal(week, runtime.Model.Editor.WeekNumberShortcut);
                Assert.Equal(calendar, runtime.Model.Editor.CalendarShortcut);
                Assert.True(runtime.Model.Editor.HasChanges);

                var registry = Registry(runtime);
                var tabs = Assert.IsType<TabControl>(runtime.Window.FindName("Tabs"));

                runtime.Window.Open(MainPage.About);
                Assert.True(registry.ProcessMessage(0x4100));
                Idle(runtime.Window);
                Assert.Equal((int)MainPage.WeekNumber, tabs.SelectedIndex);

                runtime.Window.WindowState = WindowState.Minimized;
                Assert.True(registry.ProcessMessage(0x4101));
                Idle(runtime.Window);
                Assert.Equal(WindowState.Normal, runtime.Window.WindowState);
                Assert.Equal((int)MainPage.Calendar, tabs.SelectedIndex);

                runtime.Window.Hide();
                Assert.True(registry.ProcessMessage(0x4100));
                Idle(runtime.Window);
                Assert.True(runtime.Window.IsVisible);
                Assert.Equal((int)MainPage.WeekNumber, tabs.SelectedIndex);

                runtime.Model.Editor.Load(saved);
                Assert.Equal(week, runtime.Model.Editor.WeekNumberShortcut);
                Assert.Equal(calendar, runtime.Model.Editor.CalendarShortcut);
                Assert.False(runtime.Model.Editor.HasChanges);
            });
        }
        finally
        {
            fixture.Run(() => operation = runtime?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await operation;

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static ActivationShortcutRegistry Registry(AppRuntime runtime)
    {
        var tray = (NativeTray)typeof(AppRuntime)
            .GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(runtime)!;

        return (ActivationShortcutRegistry)typeof(NativeTray)
            .GetField("_activationShortcuts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(tray)!;
    }

    private static void Idle(MainWindow window) =>
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
