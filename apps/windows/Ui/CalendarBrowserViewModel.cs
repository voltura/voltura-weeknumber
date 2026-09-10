using System.ComponentModel;
using System.Globalization;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

internal enum CalendarZoom { Year, Month, Week }

internal sealed record CalendarDayItem(DateOnly? Date, string Number, string Weekday, string DateText,
    string AccessibleName, bool IsToday, bool IsOutsideMonth);

internal sealed record CalendarWeekItem(CalendarWeek Week, string NumberText, string RangeText,
    string AccessibleName, bool IsCurrent, IReadOnlyList<CalendarDayItem> Days);

internal sealed record CalendarMonthItem(DateOnly Date, string Name, string AccessibleName,
    IReadOnlyList<CalendarWeekItem> Weeks);

internal sealed class CalendarBrowserViewModel : INotifyPropertyChanged
{
    private CalendarOptions _options = new();
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private bool _followingToday = true;
    private bool _active;

    public event PropertyChangedEventHandler? PropertyChanged;
    public CalendarZoom Zoom { get; private set; }
    public DateOnly Anchor { get; private set; } = DateOnly.FromDateTime(DateTime.Today);
    public IReadOnlyList<CalendarMonthItem> Months { get; private set; } = [];
    public IReadOnlyList<CalendarWeekItem> Weeks { get; private set; } = [];
    public IReadOnlyList<CalendarDayItem> Days { get; private set; } = [];
    public IReadOnlyList<string> Weekdays { get; private set; } = [];
    public string Title { get; private set; } = string.Empty;
    public string RangeText { get; private set; } = string.Empty;
    public string ErrorText { get; private set; } = string.Empty;
    public string TodayText => Days.Any(day => day.IsToday)
        ? $"{Strings.Current["Today"]}: {_today.ToString("D", Strings.Current.Culture)}"
        : string.Empty;
    public string ConventionText => Strings.Current[_options.Mode switch
    {
        CalendarMode.Iso => "Iso",
        CalendarMode.Custom => "Custom",
        _ => "Regional",
    }];
    public string YearText => Anchor.Year.ToString(CultureInfo.InvariantCulture);
    public string MonthText => Anchor.ToString("MMMM", Strings.Current.Culture);
    public string PreviousLabel => Strings.Current[$"Previous{Zoom}"];
    public string NextLabel => Strings.Current[$"Next{Zoom}"];
    public string HelpText => Strings.Current[Zoom == CalendarZoom.Year
        ? "ChooseMonth"
        : "ChooseWeek"];
    public bool IsYear => Zoom == CalendarZoom.Year;
    public bool IsMonth => Zoom == CalendarZoom.Month;
    public bool IsWeek => Zoom == CalendarZoom.Week;
    public bool HasPeriod => ErrorText.Length == 0;
    public bool CanPrevious => CanMove(-1);
    public bool CanNext => CanMove(1);

    internal void Apply(CalendarOptions options)
    {
        _options = options;
    }

    internal void SetActive(bool active)
    {
        _active = active;

        if (active)
        {
            Rebuild();
        }
    }

    internal void Refresh(DateTime today)
    {
        _today = DateOnly.FromDateTime(today);

        if (_followingToday)
        {
            Anchor = _today;
        }

        if (_active)
        {
            Rebuild();
        }
    }

    internal void ShowMonth(DateOnly month)
    {
        Anchor = month;
        Zoom = CalendarZoom.Month;
        _followingToday = false;
        Rebuild();
    }

    internal void ShowWeek(CalendarWeekItem week)
    {
        // Keep the originating month in the breadcrumb for a week spanning two months.
        Anchor = week.Week.Days.OfType<DateOnly>()
            .First(date => date.Year == Anchor.Year && date.Month == Anchor.Month);
        Zoom = CalendarZoom.Week;
        _followingToday = false;
        Rebuild();
    }

    internal void ZoomOut(CalendarZoom zoom)
    {
        Zoom = zoom;
        Rebuild();
    }

    internal void Today()
    {
        Anchor = _today;
        _followingToday = true;
        Rebuild();
    }

    internal void Move(int direction)
    {
        if (!CanMove(direction))
        {
            return;
        }

        Anchor = Shift(direction);
        _followingToday = false;
        Rebuild();
    }

    private DateOnly Shift(int direction) => Zoom switch
    {
        CalendarZoom.Year => Anchor.AddYears(direction),
        CalendarZoom.Month => Anchor.AddMonths(direction),
        _ => DateOnly.FromDayNumber(Math.Clamp(WeekStart() + direction * 7, 0, DateOnly.MaxValue.DayNumber)),
    };

    private int WeekStart() => Anchor.DayNumber
        - ((int)Anchor.DayOfWeek - (int)WeekCalculator.FirstWeekday(_options, CultureInfo.CurrentCulture) + 7) % 7;

    private bool CanMove(int direction) => Zoom switch
    {
        CalendarZoom.Year => direction < 0
            ? Anchor.Year > 1
            : Anchor.Year < 9999,
        CalendarZoom.Month => direction < 0
            ? Anchor.Year > 1 || Anchor.Month > 1
            : Anchor.Year < 9999 || Anchor.Month < 12,
        _ => direction < 0
            ? WeekStart() > 0
            : WeekStart() + 7 <= DateOnly.MaxValue.DayNumber,
    };

    private void Rebuild()
    {
        Months = [];
        Weeks = [];
        Days = [];
        RangeText = string.Empty;
        ErrorText = string.Empty;
        Title = IsYear
            ? YearText
            : IsMonth
                ? Anchor.ToString("Y", Strings.Current.Culture)
                : Strings.Current["Week"];

        var first = WeekCalculator.FirstWeekday(_options, CultureInfo.CurrentCulture);

        Weekdays = Enumerable.Range(0, 7)
            .Select(index => Strings.Current.Culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(((int)first + index) % 7)))
            .ToArray();

        try
        {
            if (IsYear)
            {
                Months = Enumerable.Range(1, 12).Select(month =>
                {
                    var date = new DateOnly(Anchor.Year, month, 1);
                    var weeks = MonthWeeks(date);
                    var accessible = date.ToString("Y", Strings.Current.Culture);
                    var current = weeks.FirstOrDefault(week => week.IsCurrent);

                    if (current is not null)
                    {
                        accessible += $", {Strings.Current["CurrentWeek"]}: {current.NumberText}";
                    }

                    return new CalendarMonthItem(date, date.ToString("MMMM", Strings.Current.Culture),
                        accessible, weeks);
                }).ToArray();
            }
            else if (IsMonth)
            {
                Weeks = MonthWeeks(Anchor);
            }
            else
            {
                var week = Project(CalendarPeriod.Weeks(Anchor, Anchor, _options, CultureInfo.CurrentCulture)[0]);

                Title = week.NumberText;
                RangeText = week.RangeText;
                Days = week.Days;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            Months = [];
            Weeks = [];
            Days = [];
            ErrorText = Strings.Current["CalendarUnavailable"];
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private CalendarWeekItem[] MonthWeeks(DateOnly month) => CalendarPeriod.Weeks(
        new(month.Year, month.Month, 1), new(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month)),
        _options, CultureInfo.CurrentCulture).Select(Project).ToArray();

    private CalendarWeekItem Project(CalendarWeek week)
    {
        var strings = Strings.Current;
        var number = string.Join(" / ", week.Numbers.Select(value => strings.WeekNumber(value)));
        var range = $"{CompactDate(week.Range.Start)} – {CompactDate(week.Range.End)}";
        var current = _today >= week.Range.Start && _today <= week.Range.End;
        var accessible = $"{number}, {week.Range.Start.ToString("D", strings.Culture)} – {week.Range.End.ToString("D", strings.Culture)}";

        if (current)
        {
            accessible += $", {strings["CurrentWeek"]}";
        }

        var days = week.Days.Select(date => new CalendarDayItem(date,
            date?.Day.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            date?.ToString("ddd", strings.Culture) ?? string.Empty,
            date?.ToString("MMM", strings.Culture) ?? string.Empty,
            date?.ToString("D", strings.Culture) + (date == _today
                ? $", {strings["Today"]}"
                : string.Empty),
            date == _today, date is { } value && (value.Month != Anchor.Month || value.Year != Anchor.Year))).ToArray();

        return new(week, number, range, accessible, current, days);
    }

    private string CompactDate(DateOnly date) => date.ToString(
        date.Year != Anchor.Year || IsWeek
            ? "d"
            : Strings.Current.Culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal),
        Strings.Current.Culture);
}
