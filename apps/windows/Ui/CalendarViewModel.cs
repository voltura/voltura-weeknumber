using System.ComponentModel;
using System.Globalization;
using System.Windows.Media.Imaging;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Features.Settings;

namespace VolturaWeekNumber.Ui;

public sealed class CalendarViewModel : INotifyPropertyChanged
{
    private DateTime? _date = DateTime.Today;
    private AppSettings _settings = new();
    private string _status = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
    public SettingsEditor Editor { get; } = new();
    public DateTime? SelectedDate { get => _date; set { _date = value; Refresh(); } }
    public string WeekText { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public string ConventionText { get; private set; } = string.Empty;
    public string WeekYearText { get; private set; } = string.Empty;
    public BitmapSource? Preview { get; private set; }
    public string Status { get => _status; set { _status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); } }
    public string VersionText { get; } = "Voltura WeekNumber " + typeof(CalendarViewModel).Assembly.GetName().Version?.ToString(3);
    public void Apply(AppSettings settings) { _settings = settings; Editor.Load(settings); Editor.RefreshLabels(); Refresh(); }
    public void Refresh()
    {
        var strings = Strings.Current;
        if (_date is { } date)
        {
            try
            {
                var result = WeekCalculator.Calculate(DateOnly.FromDateTime(date), _settings.Calendar, CultureInfo.CurrentCulture);
                WeekText = $"{strings["Week"]} {result.Number:00}";
                DateText = date.ToString("dddd, d MMMM yyyy", strings.Culture);
                ConventionText = strings[_settings.Calendar.Mode switch { CalendarMode.Iso => "Iso", CalendarMode.Custom => "Custom", _ => "Regional" }];
                WeekYearText = result.IsoYear is { } year && year != date.Year ? $"{strings["IsoYear"]}: {year}" : string.Empty;
                Preview = CalendarIconRenderer.Render(result.Number, 128, IconAppearance.Resolve(_settings, ThemeManager.IsTaskbarDark(), System.Windows.SystemParameters.HighContrast));
            }
            catch (ArgumentOutOfRangeException) { WeekText = "—"; DateText = strings["Invalid"]; Preview = null; }
        }
        else { WeekText = "—"; DateText = strings["ChooseDate"]; Preview = null; }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
