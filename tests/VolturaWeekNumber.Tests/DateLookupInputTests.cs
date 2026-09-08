using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class DateLookupInputTests(WpfTestFixture fixture)
{
    private static readonly string[] InputCases = ["2461291a", "a", " ", "2461291\n", "2.5", "-1", "１２３", "١٢٣", "2461291", "0123456789"];
    [Fact]
    public void NumberFieldRejectsNonAsciiDigitsFromTypingAndPaste()
    {
        fixture.Run(() =>
        {
            var page = new DateLookupPage { DataContext = new DateLookupViewModel(true) };
            var input = Assert.IsType<TextBox>(page.FindName("NumberInput"));
            foreach (var text in InputCases)
            {
                var valid = text is "2461291" or "0123456789";
                var typing = new TextCompositionEventArgs(Keyboard.PrimaryDevice,
                    new TextComposition(InputManager.Current, input, text))
                { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
                input.RaiseEvent(typing);
                Assert.Equal(!valid, typing.Handled);

                var paste = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), false, DataFormats.UnicodeText);
                input.RaiseEvent(paste);
                Assert.Equal(!valid, paste.CommandCancelled);
            }
            var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Number input test"));
            using (source)
            {
                var space = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                input.RaiseEvent(space);
                Assert.True(space.Handled);
            }
            Assert.False(input.AllowDrop);
            Assert.False(InputMethod.GetIsInputMethodEnabled(input));
            VerifyOrdinalInput();
            VerifyPreferencesNavigation();
        });
    }

    private static void VerifyPreferencesNavigation()
    {
        var window = new MainWindow(new CalendarViewModel());
        try
        {
            window.Open(MainPage.WeekNumber);
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var tabs = Assert.IsType<TabControl>(window.FindName("Tabs"));
            Assert.IsType<TabItem>(tabs.Items[0]).Focus();
            window.Hide();
            window.Open(MainPage.Preferences);
            dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.Equal((int)MainPage.Preferences, tabs.SelectedIndex);
        }
        finally { window.Exit(); }
    }

    private static void VerifyOrdinalInput()
    {
        var model = new DateLookupViewModel(false) { SelectedDate = new DateTime(2024, 1, 1) };
        var page = new DateLookupPage { DataContext = model };
        page.Measure(new Size(720, 640));
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var year = Assert.IsType<TextBox>(page.FindName("YearInput"));
        var day = Assert.IsType<TextBox>(page.FindName("NumberInput"));
        Assert.Equal("2024", year.Text);
        VerifyReplacement(year, "0000", false);
        VerifyReplacement(year, "0", false);
        VerifyReplacement(year, "10000", false);
        VerifyReplacement(year, "2026a", false);
        VerifyReplacement(year, "1", true);
        VerifyReplacement(year, "9999", true);
        VerifyReplacement(day, "00", false);
        VerifyReplacement(day, "0", false);
        VerifyReplacement(day, "367", false);
        VerifyReplacement(day, "001", true);
        VerifyReplacement(day, "366", true);
        day.SetCurrentValue(TextBox.TextProperty, "366");
        year.SetCurrentValue(TextBox.TextProperty, "2026");
        Assert.Equal("365", day.Text);
        VerifyReplacement(day, "366", false);
        day.SetCurrentValue(TextBox.TextProperty, "00");
        Assert.Equal("365", day.Text);
        year.SetCurrentValue(TextBox.TextProperty, "0000");
        Assert.Equal("2026", year.Text);
        year.SetCurrentValue(TextBox.TextProperty, "");
        year.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, year, day)
        { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
        Assert.Equal("2026", year.Text);
        Assert.Equal("365", model.NumberInput);
        Assert.Equal("2026", model.YearInput);
    }

    private static void VerifyReplacement(TextBox input, string text, bool valid)
    {
        input.SelectAll();
        var typing = new TextCompositionEventArgs(Keyboard.PrimaryDevice,
            new TextComposition(InputManager.Current, input, text))
        { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
        input.RaiseEvent(typing);
        Assert.Equal(!valid, typing.Handled);
        var paste = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), false, DataFormats.UnicodeText);
        input.RaiseEvent(paste);
        Assert.Equal(!valid, paste.CommandCancelled);
    }
}
