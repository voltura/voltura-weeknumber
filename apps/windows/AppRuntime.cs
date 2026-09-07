using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Features.Updates;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;

namespace VolturaWeekNumber;

internal sealed class AppRuntime : IAsyncDisposable
{
    private readonly AppPaths _paths;
    private readonly bool _traceDpi;
    private readonly SettingsStore _settings;
    private readonly ApplicationLog _log;
    private readonly UpdateService _updates;
    private readonly WeekTracker _tracker = new();
    private readonly DispatcherTimer _midnight;
    private readonly NativeTray _tray;
    private readonly SemaphoreSlim _actions = new(1, 1);
    private bool _hiddenExplained;
    private bool _shuttingDown;
    private bool _settingsDamaged;
    private int _refreshPending;
    private bool _updateReadyAnnounced;
    private string? _calendarIdentity;
    public CalendarViewModel Model { get; } = new();
    public MainWindow Window { get; }
    internal bool IsPerMonitorV2 => _tray.IsPerMonitorV2;
    internal uint CurrentDpi => _tray.CurrentDpi;
    internal int IconRenderCount => _tray.RenderCount;

    public AppRuntime(AppPaths paths, bool traceDpi = false)
    {
        _paths = paths;
        _traceDpi = paths.Isolated && traceDpi;
        _settings = new SettingsStore(paths.Data);
        _log = new ApplicationLog(paths.Data);
        _updates = new UpdateService(paths);
        Window = new MainWindow(Model);
        if (_traceDpi)
        {
            Window.Title += " · DPI test";
            WindowDpiDiagnostics.Attach(Window, message => _log.Record(message));
        }
        _tray = new NativeTray(promoteVisibility: !paths.Isolated);
        _midnight = new DispatcherTimer(DispatcherPriority.Background, Window.Dispatcher);
        _midnight.Tick += OnMidnight;
        _tray.OpenRequested += Open;
        _tray.PreferencesRequested += Preferences;
        _tray.ExitRequested += RequestExit;
        _tray.DisplayChanged += QueueRefresh;
        _tray.NotificationClicked += NotificationClicked;
        Window.ActionRequested += ActionRequested;
        Window.HiddenToTray += OnHidden;
        _updates.Changed += UpdateChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerChanged;
        SystemEvents.UserPreferenceChanged += OnPreferencesChanged;
        SystemEvents.DisplaySettingsChanged += OnTimeChanged;
    }

    public async Task StartAsync(bool hidden)
    {
        try { await _settings.LoadAsync(); }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        { _settingsDamaged = true; Model.Status = Strings.Current["SettingsRecovery"]; }
        ApplySettings();
        if (_settingsDamaged) Model.Status = Strings.Current["SettingsRecovery"];
        Refresh(false);
        _updates.Start(_settings.Current.AutomaticUpdates);
        if (!hidden || _settingsDamaged) Window.Open();
        if (_settings.Current.StartupNotification && !_paths.Isolated) _tray.Notify("Voltura WeekNumber", Strings.Current["Started"], _settings.Current.SilentNotifications);
        _log.Record("Started");
    }

    public void Open() { if (!_shuttingDown) Window.Open(); }
    private void Preferences() => Window.Open(MainPage.Preferences);
    internal Task ApplyReviewSettingsAsync(AppSettings value)
    {
        if (!_paths.Isolated) throw new InvalidOperationException("Review settings require an isolated profile.");
        return SaveAsync(value);
    }
    private void NotificationClicked() => Window.Open(_updates.Ready ? MainPage.About : MainPage.WeekNumber);
    private void ApplySettings()
    {
        var settings = _settings.Current;
        Strings.Current.SetLanguage(settings.Language);
        ThemeManager.Apply(settings.Theme);
        Model.Apply(settings);
        Window.UpdateLanguage();
        _log.Enabled = settings.Logging || _traceDpi;
        _tray.RebuildMenu();
        _updates.Start(settings.AutomaticUpdates);
        UpdateChanged();
    }
    private void OnHidden()
    {
        if (_hiddenExplained || _paths.Isolated) return;
        _hiddenExplained = true;
        _tray.Notify("Voltura WeekNumber", Strings.Current["Hidden"], true);
    }
    private void OnMidnight(object? sender, EventArgs args) => Refresh(true);
    private void OnTimeChanged(object? sender, EventArgs args) => QueueRefresh();
    private void OnPreferencesChanged(object sender, UserPreferenceChangedEventArgs args) => QueueRefresh();
    private void OnPowerChanged(object sender, PowerModeChangedEventArgs args) { if (args.Mode == PowerModes.Resume) QueueRefresh(); }
    private void QueueRefresh()
    {
        if (_shuttingDown || Interlocked.Exchange(ref _refreshPending, 1) != 0) return;
        _ = Window.Dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _refreshPending, 0);
            if (_shuttingDown) return;
            CultureInfo.CurrentCulture.ClearCachedData();
            TimeZoneInfo.ClearCachedData();
            ThemeManager.Apply(_settings.Current.Theme);
            Refresh(true);
            WindowWorkAreaPlacement.EnsureVisibleOnCurrentMonitor(Window);
            if (_traceDpi) _log.Record("DPI system-refresh " + JsonSerializer.Serialize(WindowDpiDiagnostics.Capture(Window)));
        }, DispatcherPriority.ContextIdle);
    }
    private void Refresh(bool notify)
    {
        if (_shuttingDown) return;
        _midnight.Stop();
        var now = DateTimeOffset.Now;
        var date = DateOnly.FromDateTime(now.LocalDateTime);
        var settings = _settings.Current;
        var region = CultureInfo.CurrentCulture;
        var firstDay = settings.Calendar.Mode == CalendarMode.Regional ? region.DateTimeFormat.FirstDayOfWeek : settings.Calendar.FirstDay;
        var rule = settings.Calendar.Mode == CalendarMode.Regional ? region.DateTimeFormat.CalendarWeekRule : settings.Calendar.Rule;
        var identity = $"{settings.Calendar.Mode}/{region.Calendar.GetType().FullName}/{firstDay}/{rule}";
        var rulesUnchanged = _calendarIdentity == identity;
        _calendarIdentity = identity;
        var result = WeekCalculator.Calculate(date, settings.Calendar, region);
        var text = $"{Strings.Current["Week"]} {result.Number:00}\n{date.ToString("dddd, d MMMM yyyy", Strings.Current.Culture)}";
        _tray.Update(result.Number, IconAppearance.Resolve(settings, ThemeManager.IsTaskbarDark(), SystemParameters.HighContrast),
            _traceDpi ? "Voltura WeekNumber · DPI test\n" + text : text);
        if (_tracker.Observe(result, notify && rulesUnchanged) && settings.WeekNotification && !_paths.Isolated)
            _tray.Notify(Strings.Current["NewWeek"], text, settings.SilentNotifications);
        Model.Refresh();
        _midnight.Interval = WeekCalculator.UntilNextMidnight(now, TimeZoneInfo.Local);
        _midnight.Start();
    }
    private void UpdateChanged()
    {
        if (_shuttingDown) return;
        _ = Window.Dispatcher.BeginInvoke(() =>
        {
            if (_shuttingDown) return;
            Window.UpdateState(_updates.Status, _updates.Ready, _updates.Eligible);
            if (_updates.Ready && !_updateReadyAnnounced && !_paths.Isolated)
            {
                _updateReadyAnnounced = true;
                _tray.Notify("Voltura WeekNumber", Strings.Current["Ready"], _settings.Current.SilentNotifications);
            }
        });
    }
    private async void ActionRequested(string action)
    {
        if (_shuttingDown || !await _actions.WaitAsync(0)) return;
        try { await ExecuteAsync(action); }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        {
            _log.Record(action, error);
            Model.Status = Strings.Current[error is InvalidDataException or ArgumentException ? "Invalid" : "Error"] + " " + error.Message;
        }
        finally { _actions.Release(); }
    }
    private async Task SaveAsync(AppSettings value, bool imported = false)
    {
        value.Validate();
        if (_settingsDamaged && !imported) { Model.Status = Strings.Current["SettingsRecovery"]; return; }
        var previous = _settings.Current;
        if (!_paths.Isolated && value.StartWithWindows != previous.StartWithWindows) StartupRegistration.Apply(value.StartWithWindows);
        try { await _settings.SaveAsync(value); }
        catch
        {
            if (!_paths.Isolated && value.StartWithWindows != previous.StartWithWindows) StartupRegistration.Apply(previous.StartWithWindows);
            throw;
        }
        _settingsDamaged = false;
        ApplySettings(); Refresh(false);
        Model.Status = Strings.Current[imported ? "Imported" : "Saved"];
        _log.Record("Settings saved");
    }
    private async Task ExecuteAsync(string action)
    {
        switch (action)
        {
            case "save": await SaveAsync(Model.Editor.Value); break;
            case "auto-updates":
                var draft = Model.Editor.Value;
                await SaveAsync(_settings.Current with { AutomaticUpdates = draft.AutomaticUpdates });
                Model.Editor.Load(draft);
                break;
            case "discard": Model.Editor.Load(_settings.Current); Model.Status = string.Empty; break;
            case "reset-icon": Model.Editor.Load(Model.Editor.Value with { AutomaticIcon = true, Foreground = "#FFFFFFFF", Background = "#FF151B26" }); break;
            case "foreground":
            case "background":
                var foreground = action == "foreground";
                var colorPicker = new ColorPickerWindow(Strings.Current[foreground ? "Foreground" : "Background"], foreground ? Model.Editor.Foreground : Model.Editor.Background) { Owner = Window };
                if (colorPicker.ShowDialog() == true)
                {
                    if (foreground) Model.Editor.Foreground = colorPicker.SelectedColor;
                    else Model.Editor.Background = colorPicker.SelectedColor;
                }
                break;
            case "import":
                var open = new OpenFileDialog { Filter = "Voltura WeekNumber (*.json)|*.json", CheckFileExists = true };
                if (open.ShowDialog(Window) == true) await SaveAsync(await SettingsStore.ReadAsync(open.FileName), true);
                break;
            case "export":
                var export = new SaveFileDialog { Filter = "Voltura WeekNumber (*.json)|*.json", FileName = "VolturaWeekNumber-settings.json" };
                if (export.ShowDialog(Window) == true) { await SettingsStore.WriteAsync(export.FileName, _settings.Current); Model.Status = Strings.Current["Exported"]; }
                break;
            case "export-icon":
                var saveIcon = new SaveFileDialog { Filter = "Icon (*.ico)|*.ico", FileName = "WeekNumber.ico" };
                if (saveIcon.ShowDialog(Window) == true)
                {
                    var settings = _settings.Current;
                    var week = WeekCalculator.Calculate(DateOnly.FromDateTime(DateTime.Now), settings.Calendar, CultureInfo.CurrentCulture);
                    var bytes = CalendarIconRenderer.EncodeIco(week.Number, IconAppearance.Resolve(settings, ThemeManager.IsTaskbarDark(), SystemParameters.HighContrast));
                    await File.WriteAllBytesAsync(saveIcon.FileName, bytes);
                    Model.Status = Strings.Current["Exported"];
                }
                break;
            case "log":
                Directory.CreateDirectory(_paths.Data);
                if (!File.Exists(_log.FilePath)) await File.WriteAllTextAsync(_log.FilePath, string.Empty);
                Launch(_log.FilePath); break;
            case "check-update":
                if (_updates.Eligible) await _updates.CheckAsync(); else Launch(UpdateService.ProjectUrl + "/releases/latest");
                break;
            // Setup stops the verified installed process only after payload verification.
            // Cancelling setup before that point leaves this application running.
            case "install-update": await _updates.InstallAsync(); break;
            case "project": Launch(UpdateService.ProjectUrl); break;
            case "license": Launch(Path.Combine(AppContext.BaseDirectory, "LICENSE.txt")); break;
            case "donate": Launch("https://www.paypal.com/donate?hosted_button_id=7PN65YXN64DBG"); break;
            case "coffee": Launch("https://ko-fi.com/G2G74W5F8"); break;
        }
    }
    private static void Launch(string target) { using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
    private async void RequestExit() => await ((App)System.Windows.Application.Current).StopAsync();
    public async ValueTask DisposeAsync()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        _midnight.Stop(); _midnight.Tick -= OnMidnight;
        SystemEvents.TimeChanged -= OnTimeChanged;
        SystemEvents.PowerModeChanged -= OnPowerChanged;
        SystemEvents.UserPreferenceChanged -= OnPreferencesChanged;
        SystemEvents.DisplaySettingsChanged -= OnTimeChanged;
        Window.ActionRequested -= ActionRequested;
        Window.HiddenToTray -= OnHidden;
        _updates.Changed -= UpdateChanged;
        await _updates.DisposeAsync();
        await _actions.WaitAsync(); _actions.Release();
        _tray.Dispose();
        Window.Exit();
        _settings.Dispose();
        _actions.Dispose();
        await _log.DisposeAsync();
    }
}
