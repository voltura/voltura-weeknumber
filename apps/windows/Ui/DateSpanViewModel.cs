using System.ComponentModel;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

public sealed class DateSpanViewModel : INotifyPropertyChanged
{
    private DateTime? _firstDate = DateTime.Today;
    private DateTime? _secondDate = DateTime.Today;
    private bool _firstFollowingToday = true;
    private bool _secondFollowingToday = true;
    private DateSpanResult? _result;

    public DateSpanViewModel() => Recalculate();

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTime? FirstDate
    {
        get => _firstDate;
        set
        {
            _firstDate = value?.Date;
            _firstFollowingToday = false;
            Recalculate();
        }
    }

    public DateTime? SecondDate
    {
        get => _secondDate;
        set
        {
            _secondDate = value?.Date;
            _secondFollowingToday = false;
            Recalculate();
        }
    }

    public string CalendarDays => Format(_result?.CalendarDays);
    public string Weekdays => Format(_result?.Weekdays);
    public string WeekendDays => Format(_result?.WeekendDays);
    public string WholeWeeks => Format(_result?.WholeWeeks);
    internal DateSpanResult? Result => _result;

    public void FirstToday() => FirstToday(DateTime.Today);

    public void SecondToday() => SecondToday(DateTime.Today);

    internal void FirstToday(DateTime today)
    {
        _firstFollowingToday = true;
        _firstDate = today.Date;
        Recalculate();
    }

    internal void SecondToday(DateTime today)
    {
        _secondFollowingToday = true;
        _secondDate = today.Date;
        Recalculate();
    }

    internal void Refresh(DateTime today)
    {
        if (_firstFollowingToday)
        {
            _firstDate = today.Date;
        }

        if (_secondFollowingToday)
        {
            _secondDate = today.Date;
        }

        Recalculate();
    }

    private void Recalculate()
    {
        _result = _firstDate is { } first && _secondDate is { } second
            ? DateSpanCalculator.Calculate(
                DateOnly.FromDateTime(first),
                DateOnly.FromDateTime(second)
            )
            : null;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static string Format(int? value) =>
        value?.ToString("N0", Strings.Current.Culture) ?? "—";
}
