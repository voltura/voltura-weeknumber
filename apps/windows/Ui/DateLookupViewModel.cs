using System.ComponentModel;
using System.Globalization;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

public sealed class DateLookupViewModel : INotifyPropertyChanged
{
    private readonly bool _julian;
    private DateTime? _date;
    private bool _followingToday = true;
    private bool _invalid;

    public DateLookupViewModel(bool julian)
    {
        _julian = julian;
        _date = DateTime.Today;
        InitializeInputs();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public DateTime? SelectedDate
    {
        get => _date;
        set
        {
            _date = value?.Date;
            _followingToday = false;
            _invalid = false;
            InitializeInputs();
            Refresh(DateTime.Today);
        }
    }
    public bool ShowYear => !_julian;
    public string YearInput { get; set; } = string.Empty;
    public string NumberInput { get; set; } = string.Empty;
    public string Result =>
        _date is { } date
            ? (
                _julian
                    ? DateLookup
                        .ToJulianDay(DateOnly.FromDateTime(date))
                        .ToString(CultureInfo.InvariantCulture)
                    : date.DayOfYear.ToString("D3", CultureInfo.InvariantCulture)
            )
            : "—";
    public string DateText =>
        _date?.ToString("dddd, d MMMM yyyy", Strings.Current.Culture)
        ?? Strings.Current["ChooseDate"];
    public string Help => Strings.Current[_julian ? "JulianHelp" : "OrdinalHelp"];
    public string NumberLabel => Strings.Current[_julian ? "JulianTab" : "OrdinalTab"];
    public string Error =>
        _invalid ? Strings.Current[_julian ? "InvalidJulian" : "InvalidOrdinal"] : string.Empty;

    public void Today()
    {
        _followingToday = true;
        _invalid = false;
        _date = DateTime.Today;
        InitializeInputs();
        Refresh(DateTime.Today);
    }

    public void Refresh(DateTime today)
    {
        if (_followingToday)
        {
            _date = today.Date;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private void InitializeInputs()
    {
        YearInput = (_date ?? DateTime.Today).Year.ToString(CultureInfo.InvariantCulture);
        NumberInput = Result;
    }

    public void Convert()
    {
        _invalid = true;

        if (
            int.TryParse(
                NumberInput,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number
            )
        )
        {
            try
            {
                DateOnly date;

                if (_julian)
                {
                    date = DateLookup.FromJulianDay(number);
                }
                else
                {
                    if (
                        !int.TryParse(
                            YearInput,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var year
                        )
                    )
                    {
                        Refresh(DateTime.Today);

                        return;
                    }

                    date = DateLookup.FromDayOfYear(year, number);
                }

                SelectedDate = date.ToDateTime(TimeOnly.MinValue);

                return;
            }
            catch (ArgumentOutOfRangeException) { }
        }

        Refresh(DateTime.Today);
    }
}
