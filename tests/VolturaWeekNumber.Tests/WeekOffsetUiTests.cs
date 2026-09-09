using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class WeekOffsetUiTests(WpfTestFixture fixture)
{
    private static readonly string[] InvalidOffsets = ["", "-", "+12", "12a", "2147483648"];
    private static readonly (string Text, bool Valid)[] InputCases =
    [
        ("12", true),
        ("-12", true),
        ("-", true),
        ("+12", false),
        ("12a", false),
        ("12 3", false),
        ("１２", false),
        ("١٢", false),
        ("-1234567", false),
    ];

    [Fact]
    public void OffsetsUseTodayAndZeroRestoresFollowingToday()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();
            var today = new DateTime(2024, 2, 29);

            model.WeekOffsetInput = "12";
            model.ApplyWeekOffset(today);
            Assert.Equal(today.AddDays(84), model.SelectedDate);

            model.WeekOffsetInput = "-12";
            model.ApplyWeekOffset(today);
            Assert.Equal(today.AddDays(-84), model.SelectedDate);

            model.WeekOffsetInput = "0";
            model.ApplyWeekOffset(today);
            model.Refresh(today.AddDays(1));
            Assert.Equal(today.AddDays(1), model.SelectedDate);
            Assert.Empty(model.WeekOffsetErrorText);
        });
    }

    [Fact]
    public void NavigationMovesSevenDaysAndHonorsDateBoundaries()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel { SelectedDate = new DateTime(2024, 2, 29) };

            model.NextWeek();
            Assert.Equal(new DateTime(2024, 3, 7), model.SelectedDate);
            model.PreviousWeek();
            Assert.Equal(new DateTime(2024, 2, 29), model.SelectedDate);
            model.Refresh(new DateTime(2024, 3, 1));
            Assert.Equal(new DateTime(2024, 2, 29), model.SelectedDate);

            model.SelectedDate = DateTime.MinValue;
            Assert.False(model.CanMovePreviousWeek);
            Assert.True(model.CanMoveNextWeek);
            model.PreviousWeek();
            Assert.Equal(DateTime.MinValue, model.SelectedDate);

            model.SelectedDate = DateTime.MaxValue.Date;
            Assert.True(model.CanMovePreviousWeek);
            Assert.False(model.CanMoveNextWeek);
            model.NextWeek();
            Assert.Equal(DateTime.MaxValue.Date, model.SelectedDate);

            model.SelectedDate = null;
            Assert.False(model.CanMovePreviousWeek);
            Assert.False(model.CanMoveNextWeek);
        });
    }

    [Fact]
    public void LargestRepresentableOffsetsReachTheCalendarBoundaries()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();
            var today = new DateOnly(2026, 9, 10);
            var forwardWeeks = (DateOnly.MaxValue.DayNumber - today.DayNumber) / 7;

            model.WeekOffsetInput = forwardWeeks.ToString(CultureInfo.InvariantCulture);
            model.ApplyWeekOffset(today.ToDateTime(TimeOnly.MinValue));
            Assert.Equal(
                DateOnly.FromDayNumber(today.DayNumber + forwardWeeks * 7),
                DateOnly.FromDateTime(model.SelectedDate!.Value)
            );
            Assert.False(model.CanMoveNextWeek);

            var backwardWeeks = -(today.DayNumber / 7);

            model.WeekOffsetInput = backwardWeeks.ToString(CultureInfo.InvariantCulture);
            model.ApplyWeekOffset(today.ToDateTime(TimeOnly.MinValue));
            Assert.Equal(
                DateOnly.FromDayNumber(today.DayNumber + backwardWeeks * 7),
                DateOnly.FromDateTime(model.SelectedDate!.Value)
            );
            Assert.False(model.CanMovePreviousWeek);
            Assert.Empty(model.WeekOffsetErrorText);
        });
    }

    [Fact]
    public void InvalidOrOverflowingOffsetsPreserveTheSelection()
    {
        fixture.Run(() =>
        {
            Strings.Current.SetLanguage("en");

            var selected = new DateTime(2026, 9, 10);
            var model = new CalendarViewModel { SelectedDate = selected };

            foreach (var input in InvalidOffsets)
            {
                model.WeekOffsetInput = input;
                model.ApplyWeekOffset(selected);
                Assert.Equal(selected, model.SelectedDate);
                Assert.NotEmpty(model.WeekOffsetErrorText);
            }

            model.WeekOffsetInput = "1";
            model.ApplyWeekOffset(DateTime.MaxValue.Date);
            Assert.Equal(selected, model.SelectedDate);
            Assert.NotEmpty(model.WeekOffsetErrorText);

            model.WeekOffsetInput = "-1";
            model.ApplyWeekOffset(DateTime.MinValue);
            Assert.Equal(selected, model.SelectedDate);
            Assert.NotEmpty(model.WeekOffsetErrorText);
        });
    }

    [Fact]
    public void InputIsStrictAndButtonsAreCompactAndAccessible()
    {
        fixture.Run(() =>
        {
            Strings.Current.SetLanguage("en");

            var model = new CalendarViewModel();
            var window = new MainWindow(model);

            try
            {
                window.Open();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                var input = Assert.IsType<TextBox>(window.FindName("WeekOffsetInput"));
                var previous = Assert.IsType<Button>(window.FindName("PreviousWeekButton"));
                var next = Assert.IsType<Button>(window.FindName("NextWeekButton"));
                var show = Assert.IsType<Button>(window.FindName("ShowOffsetDateButton"));

                Assert.Equal(40, previous.ActualWidth);
                Assert.Equal(40, next.ActualWidth);
                Assert.Equal("Previous week", AutomationProperties.GetName(previous));
                Assert.Equal("Next week", AutomationProperties.GetName(next));
                Assert.Equal("Show date", AutomationProperties.GetName(show));
                Assert.Equal("Previous week", previous.ToolTip);
                Assert.Equal("Next week", next.ToolTip);
                Assert.False(input.AllowDrop);
                Assert.False(InputMethod.GetIsInputMethodEnabled(input));

                foreach (var (text, valid) in InputCases)
                {
                    VerifyReplacement(input, text, valid);
                }

                model.WeekOffsetInput = "12";

                var before = DateTime.Today;

                show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(before.AddDays(84), model.SelectedDate);

                model.WeekOffsetInput = "-1";

                using var source = new HwndSource(new HwndSourceParameters("Week offset input test"));
                var enter = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };

                input.RaiseEvent(enter);
                Assert.True(enter.Handled);
                Assert.Equal(DateTime.Today.AddDays(-7), model.SelectedDate);
            }
            finally
            {
                window.Exit();
                Strings.Current.SetLanguage("en");
            }
        });
    }

    private static void VerifyReplacement(TextBox input, string text, bool valid)
    {
        input.SelectAll();

        var typing = new TextCompositionEventArgs(
            Keyboard.PrimaryDevice,
            new TextComposition(InputManager.Current, input, text)
        )
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
        };

        input.RaiseEvent(typing);
        Assert.Equal(!valid, typing.Handled);

        var paste = new DataObjectPastingEventArgs(
            new DataObject(DataFormats.UnicodeText, text),
            false,
            DataFormats.UnicodeText
        );

        input.RaiseEvent(paste);
        Assert.Equal(!valid, paste.CommandCancelled);
    }
}
