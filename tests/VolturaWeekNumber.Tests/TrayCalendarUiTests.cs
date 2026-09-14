using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Localization;
using VolturaWeekNumber.Ui;
using Xunit;
using CalendarMode = VolturaWeekNumber.Features.Calendar.CalendarMode;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class TrayCalendarUiTests(WpfTestFixture fixture)
{
    [Fact]
    public void HeaderPaddingIsDraggableButButtonsAndCalendarAreNot() => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();

        try
        {
            window.Open(new(1000, 900, 20, 20));
            Idle(window);
            Assert.True(window.CanDragFrom(new Point(100, 5)));
            Assert.True(window.CanDragFrom(new Point(5, 35)));
            Assert.True(window.CanDragFrom(new Point(window.ActualWidth - 5, 35)));
            Assert.False(window.CanDragFrom(new Point(100, 120)));

            foreach (var button in new[] { window.HeadingButton, window.PreviousButton, window.NextButton })
            {
                var center = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), window);

                Assert.False(window.CanDragFrom(center));
            }
        }
        finally
        {
            window.Exit();
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReopeningResetsTemporaryPositionAndMonthWithoutChangingPin(bool pinned) => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();

        try
        {
            window.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
            window.Open(new(1000, 900, 20, 20));
            Idle(window);

            var initial = new Point(window.Left, window.Top);

            window.CalendarPin.IsChecked = pinned;
            window.Left += 30;
            window.Top += 30;
            window.Model.Move(1);
            Idle(window);
            Assert.InRange(window.Left, initial.X + 29, initial.X + 31);
            Assert.InRange(window.Top, initial.Y + 29, initial.Y + 31);
            Assert.Equal(pinned, window.IsPinned);
            window.Hide();
            window.Open(new(1000, 900, 20, 20));
            Idle(window);
            Assert.InRange(window.Left, initial.X - 1, initial.X + 1);
            Assert.InRange(window.Top, initial.Y - 1, initial.Y + 1);
            Assert.Equal(pinned, window.IsPinned);
            Assert.True(window.Model.IsMonth);
            Assert.Equal("September 2026", window.Model.Heading);
        }
        finally
        {
            window.Exit();
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SystemCloseDismissesAndPreservesPinUntilExplicitExit(bool pinned) => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();
        var closed = false;

        window.Closed += (_, _) => closed = true;

        try
        {
            window.Open(new(1000, 900, 20, 20));
            window.CalendarPin.IsChecked = pinned;
            SystemCommands.CloseWindow(window);
            Idle(window);
            Assert.False(window.IsVisible);
            Assert.False(closed);
            window.Open(new(1000, 900, 20, 20));
            Assert.True(window.IsVisible);
            Assert.Equal(pinned, window.IsPinned);
        }
        finally
        {
            window.Exit();
        }

        Assert.True(closed);
        Assert.DoesNotContain(window, Application.Current.Windows.Cast<Window>());
    });

    [Fact]
    public void ActualPickerButtonsNavigateIncludingMutedCellsAndEscapeHides() => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();

        try
        {
            Strings.Current.SetLanguage("en");
            window.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
            window.Open(new(1000, 900, 20, 20));
            Idle(window);
            Click(window, "HeadingButton");

            var january = PickerButtons(window)[12];

            Assert.True(january.IsEnabled);
            Assert.Equal("January 2027 · Weeks 53, 1–4", AutomationProperties.GetName(january));
            Click(january);
            Assert.Equal(new DateOnly(2027, 1, 1), window.Model.Anchor);
            Click(window, "TodayButton");
            Click(window, "HeadingButton");
            Click(window, "HeadingButton");
            Assert.False(((Button)window.FindName("HeadingButton")).IsVisible);
            Click(PickerButtons(window)[0]);
            Assert.Equal(TrayCalendarView.Year, window.Model.View);
            Assert.Equal(2018, window.Model.Anchor.Year);
            Click(window, "HeadingButton");
            Click(window, "NextButton");
            Click(PickerButtons(window)[15]);
            Assert.Equal(2033, window.Model.Anchor.Year);

            var source = PresentationSource.FromVisual(window)!;

            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            Assert.False(window.IsVisible);
            window.Open(new(1000, 900, 20, 20));
            Assert.True(window.Model.IsMonth);
            Assert.Equal(new DateOnly(2026, 9, 1), window.Model.Anchor);
        }
        finally
        {
            window.Exit();
        }
    });

    [Theory]
    [InlineData("light", false)]
    [InlineData("dark", false)]
    [InlineData("light", true)]
    public void PickerLabelsUseMutedAndCurrentColorsInEveryLanguage(string theme, bool highContrast) => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();

        try
        {
            ThemeManager.Apply(theme);

            if (highContrast)
            {
                ThemeManager.ApplyPalette(false, true);
            }

            foreach (var language in LanguageCatalog.All)
            {
                Strings.Current.SetLanguage(language.Id);
                window.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
                window.Open(new(1000, 900, 20, 20));
                window.Model.ZoomOut();
                Idle(window);

                var buttons = PickerButtons(window);

                Assert.Equal(16, buttons.Length);

                var muted = buttons[12];
                var label = Descendants<TextBlock>(muted).First();

                Assert.Same(window.FindResource("MutedBrush"), label.Foreground);
                Assert.Same(window.FindResource("AccentTextBrush"), Descendants<TextBlock>(buttons[8]).First().Foreground);

                foreach (var button in buttons)
                {
                    Assert.True(button.IsEnabled);
                    Assert.NotEmpty(AutomationProperties.GetName(button));
                    Assert.True(button.ActualWidth > 0);

                    var right = button.TranslatePoint(new Point(button.ActualWidth, 0), window).X;

                    Assert.InRange(right, 0, window.ActualWidth);
                }

                window.Model.ZoomOut();
                Idle(window);
                Assert.Same(window.FindResource("MutedBrush"), Descendants<TextBlock>(PickerButtons(window)[0]).First().Foreground);
            }
        }
        finally
        {
            window.Exit();
            Strings.Current.SetLanguage("en");
            ThemeManager.Apply("system");
        }
    });

    [Fact]
    public void ClickedDateIsSeparateFromTodayAndSurvivesNavigation() => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();

        try
        {
            window.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
            window.Open(new(1000, 900, 20, 20));
            Idle(window);

            var days = Descendants<RadioButton>(window).ToArray();
            var tenth = Assert.Single(days, day => ((CalendarDayItem)day.DataContext).Date == new DateOnly(2026, 9, 10));

            tenth.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Idle(window);
            Assert.True(tenth.IsChecked);
            Assert.Equal(new DateOnly(2026, 9, 10), window.SelectedDate);

            var today = Assert.Single(days, day => ((CalendarDayItem)day.DataContext).IsToday);

            Assert.False(today.IsChecked);
            Assert.Same(window.FindResource("AccentBrush"), today.Background);
            window.Model.Move(1);
            window.Model.Move(-1);
            Idle(window);
            Assert.True(Assert.Single(Descendants<RadioButton>(window), day => ((CalendarDayItem)day.DataContext).Date == new DateOnly(2026, 9, 10)).IsChecked);
            Click(window, "TodayButton");
            Assert.Equal(new DateOnly(2026, 9, 13), window.SelectedDate);
        }
        finally
        {
            window.Exit();
        }
    });

    [Fact]
    public void CalendarPinPreventsOutsideDismissalAndDoesNotChangeMainWindow() => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();
        var main = new MainWindow(new CalendarViewModel());

        try
        {
            window.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
            window.Open(new(1000, 900, 20, 20));

            var pin = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("CalendarPin");

            pin.IsChecked = true;
            window.DismissOnDeactivate();
            Assert.True(window.IsVisible);
            Assert.True(window.Topmost);
            Assert.False(main.Topmost);
            window.Hide();
            Assert.False(window.IsVisible);
            window.Open(new(1000, 900, 20, 20));
            Assert.True(window.IsPinned);
            pin.IsChecked = false;
            window.DismissOnDeactivate();
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Exit();
            main.Exit();
        }
    });
    [Fact]
    public void EveryDateAcceptsHitsOnNumberAndSurroundingAreaWithOneFocusRing() => fixture.Run(() =>
    {
        var window = new TrayCalendarWindow();

        try
        {
            window.Model.Refresh(new(CalendarMode.Iso), new(2026, 9, 13));
            window.Open(new(1000, 900, 20, 20));
            Idle(window);

            foreach (var day in Descendants<RadioButton>(window))
            {
                foreach (var point in new[] { new Point(18, 18), new Point(2, 18), new Point(34, 18) })
                {
                    var hit = window.InputHitTest(day.TranslatePoint(point, window)) as DependencyObject;

                    while (hit is not null && hit is not RadioButton)
                    {
                        hit = VisualTreeHelper.GetParent(hit);
                    }

                    Assert.Same(day, hit);
                }

                day.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                day.Focus();
                Idle(window);
                Assert.True(day.IsChecked);
                Assert.Null(day.FocusVisualStyle);
                Assert.Null(day.Template.FindName("FocusMark", day));

                var ring = (Border)day.Template.FindName("Selection", day);

                Assert.Equal(Visibility.Visible, ring.Visibility);
                Assert.Same(window.FindResource("AccentBrush"), ring.BorderBrush);
            }
        }
        finally
        {
            window.Exit();
        }
    });
    private static Button[] PickerButtons(Window window)
    {
        Idle(window);

        return Descendants<Button>((ItemsControl)window.FindName("PickerItems")).ToArray();
    }
    private static void Click(Window window, string name)
    {
        Click((Button)window.FindName(name));
        Idle(window);
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Idle(Window window) => window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
