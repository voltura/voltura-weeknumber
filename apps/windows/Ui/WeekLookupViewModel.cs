using System.ComponentModel;
using System.Globalization;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

public sealed class WeekLookupViewModel : INotifyPropertyChanged
{
    private CalendarOptions _options = new();
    private string _yearInput = string.Empty;
    private string _weekInput = string.Empty;
    private string _lastDefaultYear = string.Empty;
    private string _lastDefaultWeek = string.Empty;
    private string _resultText = string.Empty;
    private string _errorText = string.Empty;
    private WeekReference[] _references = [];
    public bool CanCopyWeek => _references.Length > 0;

    internal string CopyText(WeekReferenceFormat format) => WeekReferenceFormatter.Format(
        _references, format, Strings.Current.Culture, Strings.Current["WeekNumberFormat"]);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string YearInput
    {
        get => _yearInput;
        set => SetField(ref _yearInput, value, nameof(YearInput));
    }

    public string WeekInput
    {
        get => _weekInput;
        set => SetField(ref _weekInput, value, nameof(WeekInput));
    }

    public string ResultText
    {
        get => _resultText;
        private set => SetField(ref _resultText, value, nameof(ResultText));
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetField(ref _errorText, value, nameof(ErrorText));
    }

    public void Apply(CalendarOptions options)
    {
        _options = options;
        ClearResult();
    }

    public void Refresh(DateTime today)
    {
        var followsToday = _lastDefaultYear.Length == 0
            || (YearInput == _lastDefaultYear && WeekInput == _lastDefaultWeek);
        WeekResult result;

        try
        {
            result = WeekCalculator.Calculate(
                DateOnly.FromDateTime(today),
                _options,
                CultureInfo.CurrentCulture
            );
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        var year = (result.IsoYear ?? today.Year).ToString(CultureInfo.InvariantCulture);
        var week = result.Number.ToString(CultureInfo.InvariantCulture);

        if (followsToday)
        {
            YearInput = year;
            WeekInput = week;
        }

        _lastDefaultYear = year;
        _lastDefaultWeek = week;
    }

    public void Find()
    {
        if (
            !int.TryParse(YearInput, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(WeekInput, NumberStyles.None, CultureInfo.InvariantCulture, out var week)
            || year is < 1 or > 9999
            || week is < 1 or > 56
        )
        {
            ShowError();

            return;
        }

        var ranges = WeekCalculator.FindRanges(
            year,
            week,
            _options,
            CultureInfo.CurrentCulture
        );

        if (ranges.Count == 0)
        {
            ShowError();

            return;
        }

        ResultText = string.Join(
            Environment.NewLine,
            ranges.Select(range =>
                $"{Format(range.Start)} – {Format(range.End)}"
            )
        );
        _references = ranges.Select(range => new WeekReference(range.Start, week, range)).ToArray();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCopyWeek)));
        ErrorText = string.Empty;
    }

    private static string Format(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("D", Strings.Current.Culture);

    private void ShowError()
    {
        ClearResult();
        ErrorText = Strings.Current["InvalidWeekLookup"];
    }

    private void ClearResult()
    {
        _references = [];
        ResultText = string.Empty;
        ErrorText = string.Empty;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCopyWeek)));
    }

    private void SetField(ref string field, string value, string property)
    {
        if (field == value)
        {
            return;
        }

        field = value;

        if (property is nameof(YearInput) or nameof(WeekInput))
        {
            ClearResult();
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
