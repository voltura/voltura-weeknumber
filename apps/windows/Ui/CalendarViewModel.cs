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
    private string _weekOffsetInput = "0";
    private string _weekOffsetErrorText = string.Empty;
    private (int Week, IconAppearance Appearance)? _previewKey;
    private WeekReference? _reference;
    public bool CanCopyWeek => _reference is not null;

    internal string CopyText(WeekReferenceFormat format) => WeekReferenceFormatter.Format(
        _reference is { } reference
            ? [reference]
            : [],
        format, Strings.Current.Culture, Strings.Current["WeekNumberFormat"]);
    public event PropertyChangedEventHandler? PropertyChanged;
    public WeekLookupViewModel WeekLookup { get; } = new();
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
            WeekOffsetErrorText = string.Empty;
            Refresh();
        }
    }
    public string WeekOffsetInput
    {
        get => _weekOffsetInput;
        set
        {
            if (_weekOffsetInput == value)
            {
                return;
            }

            _weekOffsetInput = value;
            WeekOffsetErrorText = string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeekOffsetInput)));
        }
    }
    public string WeekOffsetErrorText
    {
        get => _weekOffsetErrorText;
        private set
        {
            if (_weekOffsetErrorText == value)
            {
                return;
            }

            _weekOffsetErrorText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeekOffsetErrorText)));
        }
    }
    public bool CanMovePreviousWeek => CanMoveWeek(-1);
    public bool CanMoveNextWeek => CanMoveWeek(1);
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
        WeekLookup.Apply(settings.Calendar);
        WeekOffsetErrorText = string.Empty;
    }

    public void Refresh() => Refresh(DateTime.Today);

    public void Today()
    {
        _followingToday = true;
        WeekOffsetErrorText = string.Empty;
        Refresh();
    }

    public void PreviousWeek() => MoveWeek(-1);

    public void NextWeek() => MoveWeek(1);

    public void ApplyWeekOffset() => ApplyWeekOffset(DateTime.Today);

    internal void ApplyWeekOffset(DateTime today)
    {
        if (!TryParseWeekOffset(WeekOffsetInput, out var weeks))
        {
            WeekOffsetErrorText = Strings.Current["InvalidWeekOffset"];

            return;
        }

        if (weeks == 0)
        {
            _followingToday = true;
            WeekOffsetErrorText = string.Empty;
            Refresh(today);

            return;
        }

        var dayNumber = (long)DateOnly.FromDateTime(today).DayNumber + (long)weeks * 7;

        if (dayNumber is < 0 || dayNumber > DateOnly.MaxValue.DayNumber)
        {
            WeekOffsetErrorText = Strings.Current["InvalidWeekOffset"];

            return;
        }

        _date = DateOnly.FromDayNumber((int)dayNumber).ToDateTime(TimeOnly.MinValue);
        _followingToday = false;
        WeekOffsetErrorText = string.Empty;
        Refresh(today);
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
        _reference = null;
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
                _reference = new(DateOnly.FromDateTime(date), result.Number,
                    WeekReferenceFormatter.RangeFromStart(result.WeekStart));
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

    private bool CanMoveWeek(int direction) =>
        _date is { } date
        && (direction < 0
            ? date.Date >= DateTime.MinValue.AddDays(7)
            : date.Date <= DateTime.MaxValue.AddDays(-7));

    private void MoveWeek(int direction)
    {
        if (!CanMoveWeek(direction) || _date is not { } date)
        {
            return;
        }

        _date = date.AddDays(direction * 7);
        _followingToday = false;
        WeekOffsetErrorText = string.Empty;
        Refresh();
    }

    private static bool TryParseWeekOffset(string text, out int weeks)
    {
        weeks = 0;

        var digits = text.Length > 0 && text[0] == '-'
            ? text.AsSpan(1)
            : text.AsSpan();

        return digits.Length > 0
            && digits.IndexOfAnyExceptInRange('0', '9') < 0
            && int.TryParse(text, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out weeks);
    }
}
