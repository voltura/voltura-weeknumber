using System.ComponentModel;
using System.Globalization;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

internal enum TrayCalendarView { Month, Year, Decade }

internal sealed record CalendarPickerItem(DateOnly? Date, string Label, string AccessibleName,
    bool IsMuted, bool IsCurrent, string WeekRange = "", string WeekDescription = "")
{
    public bool IsEnabled => Date is not null;
    public bool HasWeekRange => WeekRange.Length > 0;
    public string TooltipText => WeekDescription.Length == 0
        ? AccessibleName
        : $"{AccessibleName} · {WeekDescription}";
}

internal sealed class TrayCalendarViewModel : INotifyPropertyChanged
{
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private int _decade;
    private CalendarOptions _options = new();
    public CalendarBrowserViewModel Month { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public TrayCalendarView View { get; private set; }
    public DateOnly Anchor { get; private set; } = DateOnly.FromDateTime(DateTime.Today);
    public IReadOnlyList<CalendarPickerItem> Items { get; private set; } = [];
    internal DateOnly TodayDate => _today;
    public bool IsMonth => View == TrayCalendarView.Month;
    public bool CanExport => View != TrayCalendarView.Decade;
    internal CalendarExportRequest ExportRequest => new(IsMonth
        ? CalendarExportScope.Month
        : CalendarExportScope.Year, Anchor);
    public string ExportLabel => CalendarExportActions.Label(ExportRequest);
    public bool IsPicker => !IsMonth;
    public bool CanZoomOut => View != TrayCalendarView.Decade;
    public string Heading => View switch
    {
        TrayCalendarView.Month => Anchor.ToString("Y", Strings.Current.Culture),
        TrayCalendarView.Year => Anchor.Year.ToString(CultureInfo.InvariantCulture),
        _ => $"{Math.Max(1, _decade)}–{Math.Min(9999, _decade + 9)}",
    };
    public string PreviousLabel => Strings.Current[View switch
    {
        TrayCalendarView.Month => "PreviousMonth",
        TrayCalendarView.Year => "PreviousYear",
        _ => "PreviousDecade",
    }];
    public string NextLabel => Strings.Current[View switch
    {
        TrayCalendarView.Month => "NextMonth",
        TrayCalendarView.Year => "NextYear",
        _ => "NextDecade",
    }];
    public bool CanPrevious => View switch
    {
        TrayCalendarView.Month => Anchor.Year > 1 || Anchor.Month > 1,
        TrayCalendarView.Year => Anchor.Year > 1,
        _ => _decade > 0,
    };
    public bool CanNext => View switch
    {
        TrayCalendarView.Month => Anchor.Year < 9999 || Anchor.Month < 12,
        TrayCalendarView.Year => Anchor.Year < 9999,
        _ => _decade < 9990,
    };

    internal void Refresh(CalendarOptions options, DateOnly today)
    {
        _today = today;
        _options = options;
        Month.Apply(options);
        Month.Refresh(today.ToDateTime(TimeOnly.MinValue));
        Rebuild();
    }

    internal void Today()
    {
        View = TrayCalendarView.Month;
        Anchor = new(_today.Year, _today.Month, 1);
        Rebuild();
    }

    internal void ZoomOut()
    {
        if (!CanZoomOut)
        {
            return;
        }

        View = IsMonth
            ? TrayCalendarView.Year
            : TrayCalendarView.Decade;
        _decade = Anchor.Year / 10 * 10;
        Rebuild();
    }

    internal void Select(CalendarPickerItem item)
    {
        if (item.Date is not { } date || IsMonth)
        {
            return;
        }

        Anchor = date;
        View = View == TrayCalendarView.Decade
            ? TrayCalendarView.Year
            : TrayCalendarView.Month;
        Rebuild();
    }

    internal void Move(int direction)
    {
        if (direction is not (-1 or 1) || (direction < 0
            ? !CanPrevious
            : !CanNext))
        {
            return;
        }

        if (View == TrayCalendarView.Decade)
        {
            _decade += direction * 10;
        }
        else
        {
            Anchor = IsMonth
                ? Anchor.AddMonths(direction)
                : Anchor.AddYears(direction);
        }

        Rebuild();
    }

    private void Rebuild()
    {
        var culture = Strings.Current.Culture;
        var monthCulture = culture.TwoLetterISOLanguageName == "en"
            ? CultureInfo.InvariantCulture
            : culture;

        if (IsMonth)
        {
            Month.ShowMonth(Anchor);
            Items = [];
        }
        else
        {
            Items = Enumerable.Range(0, 16).Select(index =>
            {
                var year = View == TrayCalendarView.Year
                    ? Anchor.Year + index / 12
                    : _decade - 2 + index;
                var month = View == TrayCalendarView.Year
                    ? index % 12 + 1
                    : 1;

                if (year is < 1 or > 9999)
                {
                    return new CalendarPickerItem(null, "", "", true, false);
                }

                var date = new DateOnly(year, month, 1);
                var weekRange = string.Empty;
                var weekDescription = string.Empty;

                if (View == TrayCalendarView.Year)
                {
                    try
                    {
                        weekRange = MonthWeekSummary.Format(date, _options, CultureInfo.CurrentCulture);
                        weekDescription = string.Format(culture, Strings.Current["WeeksFormat"], weekRange);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        weekRange = "—";
                        weekDescription = Strings.Current["CalendarUnavailable"];
                    }
                }

                return new CalendarPickerItem(date,
                    View == TrayCalendarView.Year
                        ? date.ToString("MMM", monthCulture)
                        : year.ToString(CultureInfo.InvariantCulture),
                    View == TrayCalendarView.Year
                        ? date.ToString("Y", culture)
                        : year.ToString(CultureInfo.InvariantCulture),
                    View == TrayCalendarView.Year
                        ? year != Anchor.Year
                        : year < _decade || year > _decade + 9,
                    year == _today.Year && (View == TrayCalendarView.Decade || month == _today.Month), weekRange, weekDescription);
            }).ToArray();
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
