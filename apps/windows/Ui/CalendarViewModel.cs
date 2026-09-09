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
    private bool _followingToday = true;
    private AppSettings _settings = new();
    private string _status = string.Empty;
    private (int Week, IconAppearance Appearance)? _previewKey;
    public event PropertyChangedEventHandler? PropertyChanged;
    public DateLookupViewModel DayOfYear { get; } = new(false);
    public DateLookupViewModel JulianDay { get; } = new(true);
    public SettingsEditor Editor { get; } = new();
    public DateTime? SelectedDate
    {
        get => _date;
        set
        {
            if (_date == value?.Date)
            {
                return;
            }

            _date = value?.Date;
            _followingToday = false;
            Refresh();
        }
    }
    public string WeekText { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public string ConventionText { get; private set; } = string.Empty;
    public string WeekYearText { get; private set; } = string.Empty;
    public BitmapSource? Preview { get; private set; }
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }
    public string VersionText { get; } =
        "Voltura WeekNumber " + typeof(CalendarViewModel).Assembly.GetName().Version?.ToString(3);

    // AppRuntime refreshes the calendar and tray together after applying settings.
    public void Apply(AppSettings settings)
    {
        _settings = settings;
        Editor.Load(settings);
        Editor.RefreshLabels();
    }

    public void Refresh() => Refresh(DateTime.Today);

    public void Today()
    {
        _followingToday = true;
        Refresh();
    }

    internal void Refresh(DateTime today)
    {
        if (_followingToday)
        {
            _date = today.Date;
        }

        DayOfYear.Refresh(today);
        JulianDay.Refresh(today);
        var strings = Strings.Current;
        WeekYearText = string.Empty;
        ConventionText = strings[
            _settings.Calendar.Mode switch
            {
                CalendarMode.Iso => "Iso",
                CalendarMode.Custom => "Custom",
                _ => "Regional",
            }
        ];

        if (_date is { } date)
        {
            try
            {
                var result = WeekCalculator.Calculate(
                    DateOnly.FromDateTime(date),
                    _settings.Calendar,
                    CultureInfo.CurrentCulture
                );

                WeekText = strings.WeekNumber(result.Number);
                DateText = date.ToString("D", strings.Culture);
                WeekYearText =
                    result.IsoYear is { } year && year != date.Year
                        ? $"{strings["IsoYear"]}: {year}"
                        : string.Empty;
                var appearance = IconAppearance.Resolve(
                    _settings,
                    ThemeManager.IsTaskbarDark(),
                    System.Windows.SystemParameters.HighContrast
                );
                var previewKey = (result.Number, appearance);

                if (Preview is null || _previewKey != previewKey)
                {
                    Preview = CalendarIconRenderer.Render(result.Number, 128, appearance);
                    _previewKey = previewKey;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                WeekText = "—";
                DateText = strings["Invalid"];
                Preview = null;
            }
        }
        else
        {
            WeekText = "—";
            DateText = strings["ChooseDate"];
            Preview = null;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
