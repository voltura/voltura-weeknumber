using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class PageFocusUiTests(WpfTestFixture fixture)
{
    [Theory]
    [InlineData(MainPage.WeekNumber)]
    [InlineData(MainPage.Calendar)]
    [InlineData(MainPage.DateSpan)]
    [InlineData(MainPage.DayOfYear)]
    [InlineData(MainPage.JulianDay)]
    [InlineData(MainPage.Preferences)]
    [InlineData(MainPage.About)]
    public void PageScrollContainersNeverDrawFocusAdorners(MainPage page) => fixture.Run(() =>
    {
        var window = new MainWindow(new CalendarViewModel());

        try
        {
            window.Open(page);
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var scrolls = Descendants<ScrollViewer>(window).Where(scroll => scroll.IsVisible && !HasScrollParent(scroll)).ToArray();

            Assert.NotEmpty(scrolls);

            foreach (var scroll in scrolls)
            {
                Assert.Null(scroll.FocusVisualStyle);
                scroll.Focus();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Null(scroll.FocusVisualStyle);
            }
        }
        finally
        {
            window.Exit();
        }
    });

    private static bool HasScrollParent(DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer)
            {
                return true;
            }
        }

        return false;
    }

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
