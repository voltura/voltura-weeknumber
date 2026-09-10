using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;

namespace VolturaWeekNumber;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001",
    Justification = "WPF application lifetime is closed through awaited StopAsync before Shutdown."
)]
public partial class App : System.Windows.Application
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
    private AppRuntime? _runtime;
    private SingleInstance? _instance;
    private bool _stopping;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var startupWatch = Stopwatch.StartNew();

            if (e.Args.Contains("--installer-health-check", StringComparer.Ordinal))
            {
                _ = CalendarIconRenderer.Render(53, 16, IconAppearance.Resolve(new(), false));
                Shutdown(0);

                return;
            }

            var paths = AppPaths.Resolve(e.Args);
            var reportPath = paths.Isolated
                ? AppPaths.ReadPathArgument(e.Args, "--measure-idle")
                : null;
            var output = paths.Isolated
                ? AppPaths.ReadPathArgument(e.Args, "--render-review")
                : null;

            _instance = new SingleInstance(paths.Data);

            if (!_instance.IsFirst)
            {
                _instance.Dispose();
                _instance = null;
                Shutdown();

                return;
            }

            _runtime = new AppRuntime(
                paths,
                e.Args.Contains("--trace-dpi", StringComparer.Ordinal)
            );
            _instance.Listen(() => Dispatcher.BeginInvoke(() => _runtime?.Open()));
            await _runtime.StartAsync(e.Args.Contains("--autostart", StringComparer.Ordinal));

            if (reportPath is not null)
            {
                var startupMilliseconds = startupWatch.Elapsed.TotalMilliseconds;
                using var process = Process.GetCurrentProcess();

                await Task.Delay(2000);
                process.Refresh();

                var cpu = process.TotalProcessorTime.TotalMilliseconds;
                var handles = process.HandleCount;
                var memory = process.WorkingSet64;
                var renders = _runtime.IconRenderCount;
                var samples = new List<object>();

                for (var sample = 1; sample <= 3; sample++)
                {
                    await Task.Delay(30000);
                    process.Refresh();
                    samples.Add(
                        new
                        {
                            seconds = sample * 30,
                            handles = process.HandleCount,
                            workingSet = process.WorkingSet64,
                            cpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds - cpu,
                            iconRenders = _runtime.IconRenderCount,
                        }
                    );
                }

                process.Refresh();

                var report = new
                {
                    startupMilliseconds,
                    idleSeconds = 90,
                    idleCpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds - cpu,
                    handlesBefore = handles,
                    handlesAfter = process.HandleCount,
                    workingSetBefore = memory,
                    workingSetAfter = process.WorkingSet64,
                    iconRendersBefore = renders,
                    iconRendersAfter = _runtime.IconRenderCount,
                    samples,
                    perMonitorV2 = _runtime.IsPerMonitorV2,
                    dpi = _runtime.CurrentDpi,
                    window = WindowDpiDiagnostics.Capture(_runtime.Window),
                };

                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                await File.WriteAllTextAsync(
                    reportPath,
                    JsonSerializer.Serialize(report, ReportJson)
                );
                await StopAsync();

                return;
            }

            if (output is not null)
            {
                Directory.CreateDirectory(output);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                CalendarIconRenderer.CreateReviewSheet(Path.Combine(output, "icon-review.png"));
                await File.WriteAllBytesAsync(
                    Path.Combine(output, "App.ico"),
                    CalendarIconRenderer.EncodeIco(53, IconAppearance.Resolve(new(), false))
                );

                foreach (var theme in new[] { "light", "dark" })
                {
                    Ui.ThemeManager.Apply(theme);
                    _runtime.Window.UpdateLayout();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(_runtime.Window, Path.Combine(output, "window-" + theme + ".png"));
                    _runtime.Window.Open(MainPage.DateSpan);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(_runtime.Window, Path.Combine(output, "date-span-" + theme + ".png"));

                    var reviewWidth = _runtime.Window.Width;

                    _runtime.Window.Width = _runtime.Window.MinWidth;
                    _runtime.Window.UpdateLayout();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(
                        _runtime.Window,
                        Path.Combine(output, "date-span-narrow-" + theme + ".png")
                    );
                    _runtime.Window.Width = reviewWidth;
                    _runtime.Window.Open(MainPage.Calendar);

                    var browser = _runtime.Model.CalendarBrowser;

                    browser.ZoomOut(Ui.CalendarZoom.Year);
                    browser.Today();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(_runtime.Window, Path.Combine(output, "calendar-year-" + theme + ".png"));
                    browser.ShowMonth(browser.Anchor);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(_runtime.Window, Path.Combine(output, "calendar-month-" + theme + ".png"));

                    if (browser.HasPeriod)
                    {
                        browser.ShowWeek(browser.Weeks.First(week => week.IsCurrent));
                        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                        Capture(_runtime.Window, Path.Combine(output, "calendar-week-" + theme + ".png"));
                    }

                    _runtime.Window.Open(MainPage.Preferences);
                    _runtime.Window.ApplicationPreferences.IsExpanded = true;
                    _runtime.Window.CalendarPreferences.IsExpanded = false;
                    _runtime.Window.IconPreferences.IsExpanded = false;
                    _runtime.Window.PreferencesScroll.ScrollToTop();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(
                        _runtime.Window,
                        Path.Combine(output, "preferences-" + theme + ".png")
                    );

                    var reviewDraft = _runtime.Model.Editor.Value;

                    _runtime.Model.Editor.WeekNumberShortcut = new(
                        true,
                        false,
                        false,
                        true,
                        0x59
                    );
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                    _runtime.Window.UpdateLayout();
                    Capture(
                        _runtime.Window,
                        Path.Combine(output, "preferences-assigned-" + theme + ".png")
                    );
                    _runtime.Model.Editor.Edit(reviewDraft);

                    _runtime.Window.Width = _runtime.Window.MinWidth;
                    _runtime.Window.UpdateLayout();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(
                        _runtime.Window,
                        Path.Combine(output, "preferences-narrow-" + theme + ".png")
                    );
                    _runtime.Window.Width = reviewWidth;

                    _runtime.Window.ApplicationPreferences.IsExpanded = false;
                    _runtime.Window.CalendarPreferences.IsExpanded = true;
                    _runtime.Window.IconPreferences.IsExpanded = false;
                    _runtime.Window.UpdateLayout();
                    _runtime.Window.PreferencesScroll.ScrollToTop();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    _runtime.Window.UpdateLayout();
                    Capture(
                        _runtime.Window,
                        Path.Combine(output, "preferences-calendar-" + theme + ".png")
                    );
                    _runtime.Window.ApplicationPreferences.IsExpanded = false;
                    _runtime.Window.CalendarPreferences.IsExpanded = false;
                    _runtime.Window.IconPreferences.IsExpanded = true;
                    _runtime.Window.UpdateLayout();
                    _runtime.Window.PreferencesScroll.ScrollToTop();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    _runtime.Window.UpdateLayout();
                    Capture(
                        _runtime.Window,
                        Path.Combine(output, "preferences-icon-" + theme + ".png")
                    );
                    _runtime.Window.ApplicationPreferences.IsExpanded = true;
                    _runtime.Window.CalendarPreferences.IsExpanded = false;
                    _runtime.Window.IconPreferences.IsExpanded = false;

                    var shortcutDialog = new ShortcutAssignmentWindow(
                        ActivationTarget.WeekNumber,
                        null,
                        (_, _) => false,
                        _ => new PassiveShortcutCaptureHook()
                    )
                    {
                        Owner = _runtime.Window,
                    };

                    shortcutDialog.Show();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    shortcutDialog.UpdateLayout();
                    Capture(
                        shortcutDialog,
                        Path.Combine(output, "shortcut-empty-" + theme + ".png")
                    );
                    shortcutDialog.SetReviewCandidate(
                        new(true, false, false, true, 0x59)
                    );
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                    shortcutDialog.UpdateLayout();
                    Capture(
                        shortcutDialog,
                        Path.Combine(output, "shortcut-conflict-" + theme + ".png")
                    );
                    shortcutDialog.SetReviewCandidate(
                        new(false, false, false, false, 0x08)
                    );
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                    shortcutDialog.UpdateLayout();
                    Capture(
                        shortcutDialog,
                        Path.Combine(output, "shortcut-invalid-" + theme + ".png")
                    );
                    shortcutDialog.Close();

                    _runtime.Window.Open();
                }

                await File.WriteAllTextAsync(
                    Path.Combine(output, "window-dpi.json"),
                    JsonSerializer.Serialize(
                        WindowDpiDiagnostics.Capture(_runtime.Window),
                        ReportJson
                    )
                );
                await StopAsync();
            }

        }
        catch (Exception error)
        {
            System.Windows.MessageBox.Show(
                error.Message,
                "Voltura WeekNumber",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            await StopAsync(1);
        }
    }

    private static void Capture(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth + content.Margin.Left + content.Margin.Right),
            (int)Math.Ceiling(content.ActualHeight + content.Margin.Top + content.Margin.Bottom),
            96,
            96,
            PixelFormats.Pbgra32
        );

        bitmap.Render(window);
        File.WriteAllBytes(path, CalendarIconRenderer.Png(bitmap));
    }

    public async Task StopAsync(int exitCode = 0)
    {
        if (_stopping)
        {
            return;
        }

        _stopping = true;

        if (_runtime is not null)
        {
            await _runtime.DisposeAsync();
        }

        _instance?.Dispose();
        Shutdown(exitCode);
    }
}
