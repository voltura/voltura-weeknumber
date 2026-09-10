using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Ui;
using Xunit;
using CalendarMode = VolturaWeekNumber.Features.Calendar.CalendarMode;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class CalendarBrowserUiTests(WpfTestFixture fixture)
{
    [Fact]
    public void DrillDownBreadcrumbsAndTabReentryPreserveTheBrowsedPeriod() => fixture.Run(() =>
    {
        var model = CreateModel();
        var browser = model.CalendarBrowser;
        var window = new MainWindow(model);

        try
        {
            window.Open(MainPage.Calendar);
            Idle(window);
            Assert.True(browser.IsYear);
            Assert.Equal(12, browser.Months.Count);

            var page = (CalendarBrowserPage)window.FindName("CalendarPage");
            var monthButtons = Descendants<Button>((ItemsControl)page.FindName("YearMonths")).ToArray();

            Assert.Equal(12, monthButtons.Length);
            Click(monthButtons[8]);
            Idle(window);
            Assert.True(browser.IsMonth);
            Assert.Equal(9, browser.Anchor.Month);

            var rows = Descendants<Button>((ItemsControl)page.FindName("MonthWeeks")).ToArray();
            var current = Assert.Single(rows, button => ((CalendarWeekItem)button.DataContext).IsCurrent);

            Assert.Contains("Current week", AutomationProperties.GetName(current), StringComparison.Ordinal);
            Click(current);
            Idle(window);
            Assert.True(browser.IsWeek);
            Assert.Equal(7, browser.Days.Count);
            Assert.Single(browser.Days, day => day.IsToday);
            window.Open(MainPage.Preferences);
            window.Open(MainPage.Calendar);
            Idle(window);
            Assert.True(browser.IsWeek);
            Click((Button)page.FindName("MonthBreadcrumb"));
            Assert.True(browser.IsMonth);
            Assert.Equal(9, browser.Anchor.Month);
            Click((Button)page.FindName("YearBreadcrumb"));
            Assert.True(browser.IsYear);
            Assert.Equal(2026, browser.Anchor.Year);
            Assert.Equal(new DateTime(2026, 9, 10), model.SelectedDate);
        }
        finally
        {
            window.Exit();
        }
    });

    [Fact]
    public void NavigationTodayAndMidnightRespectExplicitSelectionAndDateBoundaries() => fixture.Run(() =>
    {
        var browser = CreateModel().CalendarBrowser;

        browser.SetActive(true);
        browser.Move(1);
        Assert.Equal(2027, browser.Anchor.Year);
        browser.Refresh(new DateTime(2028, 1, 2));
        Assert.Equal(2027, browser.Anchor.Year);
        browser.Today();
        Assert.Equal(2028, browser.Anchor.Year);
        browser.ShowMonth(new(2026, 9, 1));
        browser.Refresh(new DateTime(2026, 9, 13));
        browser.ShowWeek(Assert.Single(browser.Weeks, week => week.IsCurrent));
        browser.Refresh(new DateTime(2026, 9, 14));
        Assert.DoesNotContain(browser.Days, day => day.IsToday);
        Assert.Equal(new DateOnly(2026, 9, 7), browser.Days[0].Date);
        browser.Today();
        Assert.True(browser.IsWeek);
        Assert.Equal(new DateOnly(2026, 9, 14), browser.Days[0].Date);
        browser.Refresh(new DateTime(2026, 9, 21));
        Assert.Equal(new DateOnly(2026, 9, 21), browser.Days[0].Date);

        browser.ShowMonth(DateOnly.MinValue);
        Assert.False(browser.CanPrevious);
        browser.ShowWeek(browser.Weeks[0]);
        Assert.False(browser.CanPrevious);
        browser.Move(-1);
        Assert.Equal(DateOnly.MinValue, browser.Anchor);
        browser.ShowMonth(DateOnly.MaxValue);
        Assert.False(browser.CanNext);
        browser.ShowWeek(browser.Weeks[^1]);
        Assert.False(browser.CanNext);
        browser.Move(1);
        Assert.Contains(browser.Days, day => day.Date == DateOnly.MaxValue);
        browser.ZoomOut(CalendarZoom.Year);
        Assert.False(browser.CanNext);
    });

    [Fact]
    public void LanguageAndRulesRecalculateWithoutLosingPositionAndUnsupportedYearClearsAllCards() => fixture.Run(() =>
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            var browser = CreateModel().CalendarBrowser;

            browser.SetActive(true);
            browser.ShowMonth(new(2026, 9, 1));

            var anchor = browser.Anchor;

            browser.Apply(new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstDay));
            Strings.Current.SetLanguage("de");
            browser.Refresh(new DateTime(2026, 9, 10));
            Assert.Equal(anchor, browser.Anchor);
            Assert.True(browser.IsMonth);
            Assert.Equal(DayOfWeek.Sunday, browser.Weeks[0].Week.Range.Start.DayOfWeek);
            Assert.StartsWith("Woche", browser.Weeks[0].NumberText, StringComparison.Ordinal);
            browser.ShowMonth(new(1800, 1, 1));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            browser.Apply(new());
            browser.ZoomOut(CalendarZoom.Year);
            Assert.False(browser.HasPeriod);
            Assert.Empty(browser.Months);
            browser.Apply(new(CalendarMode.Iso));
            browser.Refresh(new DateTime(2026, 9, 10));
            Assert.True(browser.HasPeriod);
            Assert.Equal(12, browser.Months.Count);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            Strings.Current.SetLanguage("en");
        }
    });

    [Theory]
    [InlineData("en", "light", 720)]
    [InlineData("en", "dark", 720)]
    [InlineData("de", "light", 560)]
    [InlineData("fr", "dark", 560)]
    [InlineData("ja", "light", 560)]
    [InlineData("en", "contrast", 560)]
    public void CalendarViewsFitAndRender(string language, string theme, int width) => fixture.Run(() =>
    {
        var model = CreateModel();

        Strings.Current.SetLanguage(language);

        if (theme == "contrast")
        {
            ThemeManager.Apply("light");
            ThemeManager.ApplyPalette(false, true);
        }
        else
        {
            ThemeManager.Apply(theme);
        }

        var window = new MainWindow(model) { Width = width };

        try
        {
            window.UpdateLanguage();
            window.Open(MainPage.Calendar);
            Idle(window);
            window.Width = width;

            var browser = model.CalendarBrowser;
            var page = (CalendarBrowserPage)window.FindName("CalendarPage");

            foreach (var zoom in Enum.GetValues<CalendarZoom>())
            {
                if (zoom == CalendarZoom.Month)
                {
                    browser.ShowMonth(new(2026, 9, 1));
                }
                else if (zoom == CalendarZoom.Week)
                {
                    browser.ShowWeek(Assert.Single(browser.Weeks, week => week.IsCurrent));
                }

                Idle(window);
                Assert.True(browser.HasPeriod);

                var scroll = (ScrollViewer)page.FindName("CalendarScroll");

                Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);

                foreach (var button in Descendants<Button>(page).Where(button => button.IsVisible))
                {
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));

                    var right = button.TranslatePoint(new Point(button.ActualWidth, 0), page).X;

                    Assert.InRange(right, 0, page.ActualWidth + 1);
                }

                var output = Environment.GetEnvironmentVariable("VOLTURA_CALENDAR_REVIEW");

                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);

                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);

                    bitmap.Render(window);
                    File.WriteAllBytes(Path.Combine(output, $"calendar-{zoom}-{language}-{theme}-{width}.png"), CalendarIconRenderer.Png(bitmap));
                }
            }
        }
        finally
        {
            window.Exit();
            Strings.Current.SetLanguage("en");
            ThemeManager.Apply("system");
        }
    });

    private static CalendarViewModel CreateModel()
    {
        Strings.Current.SetLanguage("en");

        var model = new CalendarViewModel();

        model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });
        model.Refresh(new DateTime(2026, 9, 10));

        return model;
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
