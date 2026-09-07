using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;
using System.Text.Json;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Platform;

namespace VolturaWeekNumber;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "WPF application lifetime is closed through awaited StopAsync before Shutdown.")]
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
                Shutdown(0); return;
            }
            var paths = AppPaths.Resolve(e.Args);
            _instance = new SingleInstance(paths.Data);
            if (!_instance.IsFirst) { _instance.Dispose(); _instance = null; Shutdown(); return; }
            _runtime = new AppRuntime(paths, e.Args.Contains("--trace-dpi", StringComparer.Ordinal));
            _instance.Listen(() => Dispatcher.BeginInvoke(() => _runtime?.Open()));
            await _runtime.StartAsync(e.Args.Contains("--autostart", StringComparer.Ordinal));
            var measureIndex = Array.IndexOf(e.Args, "--measure-idle");
            if (measureIndex >= 0 && paths.Isolated)
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
                    samples.Add(new { seconds = sample * 30, handles = process.HandleCount, workingSet = process.WorkingSet64, cpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds - cpu, iconRenders = _runtime.IconRenderCount });
                }
                process.Refresh();
                var report = new { startupMilliseconds, idleSeconds = 90, idleCpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds - cpu,
                    handlesBefore = handles, handlesAfter = process.HandleCount, workingSetBefore = memory, workingSetAfter = process.WorkingSet64,
                    iconRendersBefore = renders, iconRendersAfter = _runtime.IconRenderCount, samples,
                    perMonitorV2 = _runtime.IsPerMonitorV2, dpi = _runtime.CurrentDpi, window = WindowDpiDiagnostics.Capture(_runtime.Window) };
                var reportPath = Path.GetFullPath(e.Args[measureIndex + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, ReportJson));
                await StopAsync(); return;
            }
            var renderIndex = Array.IndexOf(e.Args, "--render-review");
            if (renderIndex >= 0 && paths.Isolated)
            {
                var output = Path.GetFullPath(e.Args[renderIndex + 1]);
                Directory.CreateDirectory(output);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                CalendarIconRenderer.CreateReviewSheet(Path.Combine(output, "icon-review.png"));
                await File.WriteAllBytesAsync(Path.Combine(output, "App.ico"), CalendarIconRenderer.EncodeIco(53, IconAppearance.Resolve(new(), false)));
                foreach (var theme in new[] { "light", "dark" })
                {
                    Ui.ThemeManager.Apply(theme);
                    _runtime.Window.UpdateLayout();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(_runtime.Window, Path.Combine(output, "window-" + theme + ".png"));
                }
                await File.WriteAllTextAsync(Path.Combine(output, "window-dpi.json"), JsonSerializer.Serialize(WindowDpiDiagnostics.Capture(_runtime.Window), ReportJson));
                foreach (var language in new[] { "en", "sv", "de" })
                {
                    await _runtime.ApplyReviewSettingsAsync(new AppSettings { Language = language, Theme = "light" });
                    _runtime.Window.Width = 480; _runtime.Window.Height = 400;
                    foreach (var page in new[] { MainPage.DayOfYear, MainPage.JulianDay })
                    {
                        foreach (var reviewTheme in new[] { "light", "dark" })
                        {
                            Ui.ThemeManager.Apply(reviewTheme);
                            _runtime.Window.Width = 640; _runtime.Window.Height = 700;
                            _runtime.Window.Open(page);
                            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                            _runtime.Window.UpdateLayout();
                            Capture(_runtime.Window, Path.Combine(output, page + "-" + language + "-" + reviewTheme + ".png"));
                            _runtime.Window.Width = 360; _runtime.Window.Height = 520;
                            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                            _runtime.Window.UpdateLayout();
                            Capture(_runtime.Window, Path.Combine(output, page + "-" + language + "-" + reviewTheme + "-compact.png"));
                        }
                    }
                    Ui.ThemeManager.Apply("light");
                    _runtime.Window.Width = 720; _runtime.Window.Height = 640;
                    _runtime.Window.Open(MainPage.About);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    _runtime.Window.UpdateLayout();
                    Capture(_runtime.Window, Path.Combine(output, "about-" + language + ".png"));
                    _runtime.Window.Open(MainPage.Preferences);
                    _runtime.Window.PrepareReview();
                    await Task.Delay(350);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    _runtime.Window.UpdateLayout();
                    Capture(_runtime.Window, Path.Combine(output, "preferences-" + language + ".png"));
                    _runtime.Window.Width = 480; _runtime.Window.Height = 400;
                    _runtime.Window.Open(MainPage.Preferences);
                    _runtime.Window.PrepareReview();
                    await Task.Delay(350);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    _runtime.Window.UpdateLayout();
                    Capture(_runtime.Window, Path.Combine(output, "preferences-" + language + "-compact.png"));
                    _runtime.Window.Open(MainPage.About);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    _runtime.Window.UpdateLayout();
                    Capture(_runtime.Window, Path.Combine(output, "about-" + language + "-compact.png"));
                }
                var colorReview = new Ui.ColorPickerWindow(Ui.Strings.Current["Background"], "#80245CB4") { Owner = _runtime.Window };
                colorReview.Show();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                colorReview.UpdateLayout();
                Capture(colorReview, Path.Combine(output, "color-picker.png"));
                colorReview.Close();
                await StopAsync();
            }
        }
        catch (Exception error)
        {
            System.Windows.MessageBox.Show(error.Message, "Voltura WeekNumber", MessageBoxButton.OK, MessageBoxImage.Error);
            await StopAsync(1);
        }
    }

    private static void Capture(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + 40), (int)Math.Ceiling(content.ActualHeight + 40), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        File.WriteAllBytes(path, CalendarIconRenderer.Png(bitmap));
    }
    public async Task StopAsync(int exitCode = 0)
    {
        if (_stopping) return;
        _stopping = true;
        if (_runtime is not null) await _runtime.DisposeAsync();
        _instance?.Dispose();
        Shutdown(exitCode);
    }
}
