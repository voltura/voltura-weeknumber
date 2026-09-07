using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;

namespace VolturaWeekNumber.Ui;

public partial class DateLookupPage : UserControl
{
    private string _lastYear = "1";
    private string _lastNumber = "1";
    private bool _updating;
    public DateLookupPage() => InitializeComponent();
    private static bool ContainsOnlyDigits(string text) => text.All(character => character is >= '0' and <= '9');
    private bool ValidInput(TextBox input, string text)
    {
        if (!ContainsOnlyDigits(text)) return false;
        if (DataContext is not DateLookupViewModel { ShowYear: true }) return true;
        if (text.Length == 0) return true; // Allow clearing a field while replacing its value.
        var maximum = input == YearInput ? 9999 : MaximumDay();
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1 && value <= maximum;
    }
    private int MaximumDay() => int.TryParse(YearInput.Text, out var year) && year is >= 1 and <= 9999 && DateTime.IsLeapYear(year) ? 366 : 365;
    private bool ValidInsertion(TextBox input, string text) => ValidInput(input,
        input.Text.Remove(input.SelectionStart, input.SelectionLength).Insert(input.SelectionStart, text));
    private void NumberTextInput(object sender, TextCompositionEventArgs args) => args.Handled = !ValidInsertion((TextBox)sender, args.Text);
    private void NumberKeyDown(object sender, KeyEventArgs args)
    {
        // WPF handles Space as a key command instead of text input.
        if (args.Key == Key.Space) args.Handled = true;
    }
    private void NumberPasting(object sender, DataObjectPastingEventArgs args)
    {
        if (args.DataObject.GetData(DataFormats.UnicodeText) is not string text || !ValidInsertion((TextBox)sender, text))
            args.CancelCommand();
    }
    private void OrdinalTextChanged(object sender, TextChangedEventArgs args)
    {
        if (_updating || DataContext is not DateLookupViewModel { ShowYear: true }) return;
        var input = (TextBox)sender;
        _updating = true;
        try
        {
            if (!ValidInput(input, input.Text))
            {
                var caret = input.CaretIndex;
                input.SetCurrentValue(TextBox.TextProperty, input == YearInput ? _lastYear : _lastNumber);
                input.CaretIndex = Math.Min(caret, input.Text.Length);
            }
            if (input.Text.Length == 0) return;
            if (input == YearInput)
            {
                _lastYear = input.Text;
                if (NumberInput is not null && int.TryParse(NumberInput.Text, out var day) && day > MaximumDay())
                {
                    _lastNumber = MaximumDay().ToString(CultureInfo.InvariantCulture);
                    NumberInput.SetCurrentValue(TextBox.TextProperty, _lastNumber);
                }
            }
            else _lastNumber = input.Text;
        }
        finally { _updating = false; }
    }
    private void OrdinalLostFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (DataContext is DateLookupViewModel { ShowYear: true } && sender is TextBox { Text.Length: 0 } input)
            input.SetCurrentValue(TextBox.TextProperty, input == YearInput ? _lastYear : _lastNumber);
    }
    private void TodayClick(object sender, RoutedEventArgs args) => ((DateLookupViewModel)DataContext).Today();
    private void ConvertClick(object sender, RoutedEventArgs args) => ((DateLookupViewModel)DataContext).Convert();
}
