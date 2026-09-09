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
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;
using CalendarMode = VolturaWeekNumber.Features.Calendar.CalendarMode;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class WeekCopyUiTests(WpfTestFixture fixture)
{
    private static readonly string[] ControlNames = ["SelectedWeekCopy", "LookupWeekCopy"];
    private static readonly string[] Themes = ["light", "dark"];
    [Fact]
    public void BothControlsCopyEachFormatAndReportSuccessOrFailure() => fixture.Run(() =>
    {
        Strings.Current.SetLanguage("en");

        var clipboard = new FakeClipboard();
        var model = new CalendarViewModel();

        model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });
        model.SelectedDate = new(2026, 9, 9);

        var window = new MainWindow(model, clipboard);

        try
        {
            window.Open();
            ((Expander)window.FindName("WeekLookupExpander")).IsExpanded = true;
            model.WeekLookup.YearInput = "2026";
            model.WeekLookup.WeekInput = "37";
            model.WeekLookup.Find();
            Idle(window);

            foreach (var name in ControlNames)
            {
                var control = (WeekCopyControl)window.FindName(name);

                Assert.True(control.IsVisible);

                var primary = (Button)control.FindName("CopyButton");
                var arrow = (Button)control.FindName("FormatsButton");

                Assert.Equal("Copy week reference", AutomationProperties.GetName(primary));
                Assert.Equal("Copy formats", AutomationProperties.GetName(arrow));
                primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Week 37 · Monday, 7 September 2026 – Sunday, 13 September 2026", clipboard.Text);
                Assert.Equal("Copied to clipboard.", model.Status);
                arrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(arrow.ContextMenu.IsOpen);

                foreach (var item in arrow.ContextMenu.Items.Cast<MenuItem>())
                {
                    item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

                    var expected = (string)item.Tag switch
                    {
                        "Iso" => "2026-W37 · 2026-09-07/2026-09-13",
                        "Short" => "W37 · 07/09/2026–13/09/2026",
                        _ => "Week 37 · Monday, 7 September 2026 – Sunday, 13 September 2026",
                    };

                    Assert.Equal(expected, clipboard.Text);
                }

                arrow.ContextMenu.IsOpen = false;
                clipboard.Succeeds = false;
                primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Could not copy. Try again.", model.Status);
                clipboard.Succeeds = true;
            }

            Assert.Equal(new DateTime(2026, 9, 9), model.SelectedDate);
            Assert.Equal("37", model.WeekLookup.WeekInput);
            model.SelectedDate = null;
            model.WeekLookup.WeekInput = "";
            Idle(window);
            Assert.False(((WeekCopyControl)window.FindName("SelectedWeekCopy")).IsEnabled);
            Assert.False(((WeekCopyControl)window.FindName("LookupWeekCopy")).IsVisible);

            var writes = clipboard.Writes;

            ((Button)((WeekCopyControl)window.FindName("LookupWeekCopy")).FindName("CopyButton"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(writes, clipboard.Writes);
        }
        finally
        {
            window.Exit();
        }
    });

    [Fact]
    public void RegionalLookupsKeepMultipleRangesAndRefreshInvalidatesStaleReferences() => fixture.Run(() =>
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Strings.Current.SetLanguage("en");

            var model = new CalendarViewModel();
            var settings = new AppSettings { Calendar = new(CalendarMode.Custom, DayOfWeek.Sunday, CalendarWeekRule.FirstFullWeek) };

            model.Apply(settings);
            model.SelectedDate = new(2026, 9, 9);

            var lookup = model.WeekLookup;

            lookup.YearInput = "2021";
            lookup.WeekInput = "52";
            lookup.Find();
            Assert.True(lookup.CanCopyWeek);
            Assert.Equal(2, lookup.CopyText(WeekReferenceFormat.Short).Split(Environment.NewLine).Length);
            Assert.StartsWith("2020-W52", lookup.CopyText(WeekReferenceFormat.Iso));
            Strings.Current.SetLanguage("sv");
            model.Refresh();
            Assert.StartsWith("Vecka", model.CopyText(WeekReferenceFormat.Localized));
            Assert.StartsWith("Vecka", lookup.CopyText(WeekReferenceFormat.Localized));
            model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });
            model.Refresh();
            Assert.False(lookup.CanCopyWeek);
            lookup.Find();
            Assert.True(lookup.CanCopyWeek);
            lookup.WeekInput = "53";
            lookup.Find();
            Assert.False(lookup.CanCopyWeek);
            Assert.Empty(lookup.CopyText(WeekReferenceFormat.Iso));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            model.Apply(new AppSettings());
            model.SelectedDate = new(1800, 1, 1);
            Assert.False(model.CanCopyWeek);
            Assert.Empty(model.CopyText(WeekReferenceFormat.Localized));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            Strings.Current.SetLanguage("en");
        }
    });

    [Fact]
    public void CopyControlsFitMinimumWidthInEveryLanguageAndTheme() => fixture.Run(() =>
    {
        var model = new CalendarViewModel();

        model.Apply(new AppSettings { Calendar = new(CalendarMode.Iso) });
        model.SelectedDate = new(2026, 9, 9);

        var window = new MainWindow(model, new FakeClipboard());

        try
        {
            window.Open();
            Idle(window);
            window.Width = window.MinWidth;
            window.Height = 850;
            ((Expander)window.FindName("WeekLookupExpander")).IsExpanded = true;
            model.WeekLookup.YearInput = "2026";
            model.WeekLookup.WeekInput = "37";

            foreach (var theme in Themes)
            {
                ThemeManager.Apply(theme);

                foreach (var language in VolturaWeekNumber.Features.Localization.LanguageCatalog.All)
                {
                    Strings.Current.SetLanguage(language.Id);
                    window.UpdateLanguage();
                    model.Refresh();
                    model.WeekLookup.Find();
                    Idle(window);
                    Assert.Equal(window.MinWidth, window.Width);

                    foreach (var name in ControlNames)
                    {
                        var control = (WeekCopyControl)window.FindName(name);
                        var arrow = (Button)control.FindName("FormatsButton");
                        var right = arrow.TranslatePoint(new Point(arrow.ActualWidth, 0), control).X;

                        Assert.True(right <= control.ActualWidth + 1, $"{language.Id}: {name} overflows");
                    }

                    var output = Environment.GetEnvironmentVariable("VOLTURA_WEEKCOPY_REVIEW");

                    if (output is not null && language.Id is "en" or "sv" or "fr" or "ja")
                    {
                        Directory.CreateDirectory(output);
                        // Let Fluent expansion/theme animations finish before taking review images.

                        var frame = new DispatcherFrame();
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };

                        timer.Tick += (_, _) =>
                        {
                            timer.Stop();
                            frame.Continue = false;
                        };

                        timer.Start();
                        Dispatcher.PushFrame(frame);

                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);

                        bitmap.Render(window);
                        File.WriteAllBytes(Path.Combine(output, $"copy-{language.Id}-{theme}.png"), CalendarIconRenderer.Png(bitmap));

                        var arrow = (Button)((WeekCopyControl)window.FindName("SelectedWeekCopy")).FindName("FormatsButton");

                        arrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Idle(window);

                        var menu = arrow.ContextMenu;
                        var menuBitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                        var menuVisual = new DrawingVisual();

                        using (var drawing = menuVisual.RenderOpen())
                        {
                            drawing.DrawRectangle(new VisualBrush(menu), null, new Rect(0, 0, menu.ActualWidth, menu.ActualHeight));
                        }

                        menuBitmap.Render(menuVisual);
                        File.WriteAllBytes(Path.Combine(output, $"menu-{language.Id}-{theme}.png"), CalendarIconRenderer.Png(menuBitmap));
                        menu.IsOpen = false;
                    }
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

    private static void Idle(MainWindow window) => window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private sealed class FakeClipboard : IClipboardWriter
    {
        internal bool Succeeds { get; set; } = true;
        internal string Text { get; private set; } = "";
        internal int Writes { get; private set; }
        public bool TryWrite(string text)
        {
            Writes++;

            if (Succeeds)
            {
                Text = text;
            }

            return Succeeds;
        }
    }
}
