using System.Globalization;
using System.Reflection;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class RuntimeCalendarTests(WpfTestFixture fixture)
{
    private static readonly int[] RefreshYears = [1800, 2026, 1800, 2026];

    [Fact]
    public async Task UnsupportedRegionalDateKeepsTrayAndTimerAliveAndRecovers()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        Task cleanup = Task.CompletedTask;
        try
        {
            fixture.Run(() =>
            {
                var previousCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                    Strings.Current.SetLanguage("en");
                    runtime = new AppRuntime(new(root, false, true));
                    var tray = Field<NativeTray>(runtime, "_tray");
                    var timer = Field<DispatcherTimer>(runtime, "_midnight");
                    // Include failure on the first refresh, and after a valid icon was displayed.
                    foreach (var year in RefreshYears)
                    {
                        var date = new DateTime(year, 9, 9, 12, 0, 0, DateTimeKind.Local);
                        runtime.Model.SelectedDate = date;
                        runtime.Refresh(true, new DateTimeOffset(date));

                        Assert.True(timer.IsEnabled);
                        Assert.InRange(timer.Interval.TotalHours, 11, 13);
                        var rendered = Field<(int? Week, int Size, IconAppearance Appearance)>(
                            tray,
                            "_rendered"
                        );
                        var tooltip = Field<string>(tray, "_tooltip");
                        if (year == 1800)
                        {
                            Assert.Null(rendered.Week);
                            Assert.Contains(
                                Strings.Current["Invalid"],
                                tooltip,
                                StringComparison.Ordinal
                            );
                            Assert.Null(Field<object?>(runtime, "_calendarIdentity"));
                            Assert.Equal("—", runtime.Model.WeekText);
                            Assert.Null(runtime.Model.Preview);
                        }
                        else
                        {
                            Assert.NotNull(rendered.Week);
                            Assert.StartsWith("Week ", tooltip, StringComparison.Ordinal);
                            Assert.NotNull(Field<string>(runtime, "_calendarIdentity"));
                            Assert.NotNull(runtime.Model.Preview);
                        }
                    }
                }
                finally
                {
                    CultureInfo.CurrentCulture = previousCulture;
                    Strings.Current.SetLanguage("en");
                }
            });
        }
        finally
        {
            fixture.Run(() => cleanup = runtime?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await cleanup;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static T Field<T>(object instance, string name) =>
        (T)instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
}
