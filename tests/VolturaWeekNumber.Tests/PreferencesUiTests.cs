using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Localization;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class PreferencesUiTests(WpfTestFixture fixture)
{
    private static readonly Point[] HeaderEdgePoints =
    [
        new(12, double.NaN),
        new(double.PositiveInfinity, double.NaN),
        new(double.NaN, 12),
        new(double.NaN, double.PositiveInfinity),
    ];

    [Fact]
    public void EveryPreferencesHeaderEdgeReachesItsToggleAndTogglesExactlyOnce()
    {
        fixture.Run(() =>
        {
            var window = OpenPreferences(new CalendarViewModel());

            try
            {
                var expanders = Descendants(window).OfType<Expander>().ToArray();

                Assert.Equal(5, expanders.Length);

                foreach (var expander in expanders)
                {
                    expander.IsExpanded = false;
                }

                window.UpdateLayout();

                foreach (var expander in expanders)
                {
                    var header = Assert.IsType<ToggleButton>(expander.Template.FindName("HeaderSite", expander));
                    var headerBorder = Assert.IsType<Border>(expander.Template.FindName("ToggleButtonBorder", expander));
                    var expanded = 0;
                    var collapsed = 0;

                    expander.Expanded += (_, _) => expanded++;
                    expander.Collapsed += (_, _) => collapsed++;

                    Assert.InRange(
                        Math.Abs(
                            header.ActualHeight
                                - (
                                    headerBorder.ActualHeight
                                    - headerBorder.BorderThickness.Top
                                    - headerBorder.BorderThickness.Bottom
                                )
                        ),
                        0,
                        1
                    );

                    foreach (var edge in HeaderEdgePoints)
                    {
                        var point = new Point(
                            double.IsNaN(edge.X)
                                ? header.ActualWidth / 2
                                : double.IsPositiveInfinity(edge.X)
                                    ? header.ActualWidth - 12
                                    : edge.X,
                            double.IsNaN(edge.Y)
                                ? header.ActualHeight / 2
                                : double.IsPositiveInfinity(edge.Y)
                                    ? header.ActualHeight - 12
                                    : edge.Y
                        );
                        var hit = Assert.IsAssignableFrom<DependencyObject>(
                            header.InputHitTest(point)
                        );

                        Assert.Same(header, Ancestor<ToggleButton>(hit));

                        var wasExpanded = expander.IsExpanded;
                        var expandedBefore = expanded;
                        var collapsedBefore = collapsed;
                        var provider = Assert.IsAssignableFrom<IToggleProvider>(
                            new ToggleButtonAutomationPeer(header).GetPattern(PatternInterface.Toggle)
                        );

                        provider.Toggle();

                        Assert.Equal(!wasExpanded, expander.IsExpanded);
                        Assert.Equal(expandedBefore + (wasExpanded
                            ? 0
                            : 1), expanded);
                        Assert.Equal(collapsedBefore + (wasExpanded
                            ? 1
                            : 0), collapsed);
                        Assert.True(expanders.Count(candidate => candidate.IsExpanded) <= 1);
                    }
                }
            }
            finally
            {
                window.Exit();
            }
        });
    }

    [Fact]
    public void LanguageHeadingUsesTheEffectiveLanguageAndExistingIndexerNotification()
    {
        fixture.Run(() =>
        {
            var originalCulture = CultureInfo.CurrentUICulture;
            var notifications = 0;
            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (args.PropertyName == "Item[]")
                {
                    notifications++;
                }
            };

            Strings.Current.PropertyChanged += handler;

            try
            {
                Strings.Current.SetLanguage("en");
                Assert.Equal("Language", Strings.Current["LanguageHeading"]);

                Strings.Current.SetLanguage("sv");
                Assert.Equal("Språk / Language", Strings.Current["LanguageHeading"]);

                Strings.Current.SetLanguage("de");
                Assert.Equal("Sprache / Language", Strings.Current["LanguageHeading"]);

                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("sv-SE");
                Strings.Current.SetLanguage("system");
                Assert.Equal("Språk / Language", Strings.Current["LanguageHeading"]);
                Assert.Equal(4, notifications);
            }
            finally
            {
                Strings.Current.PropertyChanged -= handler;
                CultureInfo.CurrentUICulture = originalCulture;
                Strings.Current.SetLanguage("en");
            }
        });
    }

    [Fact]
    public void SaveAndDiscardUseLocalizedAccessibleGlyphAndTextContent()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();
            var window = OpenPreferences(model);

            try
            {
                foreach (var language in LanguageCatalog.All)
                {
                    Strings.Current.SetLanguage(language.Id);
                    model.Apply(new AppSettings { Language = language.Id });
                    window.UpdateLanguage();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();

                    AssertActionButton(
                        (Button)window.FindName("SaveChangesButton"),
                        "\uE74E",
                        Strings.Current["Save"]
                    );
                    AssertActionButton(
                        (Button)window.FindName("DiscardChangesButton"),
                        "\uE7A7",
                        Strings.Current["Cancel"]
                    );
                }
            }
            finally
            {
                window.Exit();
                Strings.Current.SetLanguage("en");
            }
        });
    }

    [Fact]
    public void IconSwatchesAndPreferenceActionRowsTrackDraftLayoutAndWrapping()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();
            var saved = new AppSettings
            {
                AutomaticIcon = false,
                Foreground = "#FF123456",
                Background = "#80112233",
            };

            model.Apply(saved);

            var window = OpenPreferences(model);

            try
            {
                var expanders = Descendants(window).OfType<Expander>().ToArray();
                var icon = expanders.Single(expander => Equals(expander.Header, Strings.Current["Icon"]));

                icon.IsExpanded = true;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();

                var foregroundSwatch = (Border)window.FindName("ForegroundSwatch");
                var backgroundSwatch = (Border)window.FindName("BackgroundSwatch");

                Assert.Equal(Color.FromArgb(0xFF, 0x12, 0x34, 0x56), SwatchColor(foregroundSwatch));
                Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), SwatchColor(backgroundSwatch));
                AssertColorRow(foregroundSwatch, "foreground");
                AssertColorRow(backgroundSwatch, "background");

                model.Editor.Foreground = "#FFAABBCC";
                model.Editor.Background = "#40654321";
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

                Assert.Equal(Color.FromArgb(0xFF, 0xAA, 0xBB, 0xCC), SwatchColor(foregroundSwatch));
                Assert.Equal(Color.FromArgb(0x40, 0x65, 0x43, 0x21), SwatchColor(backgroundSwatch));

                model.Editor.Edit(model.Editor.Value with
                {
                    AutomaticIcon = true,
                    Foreground = "#FFFFFFFF",
                    Background = "#FF151B26",
                });
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

                Assert.Equal(Colors.White, SwatchColor(foregroundSwatch));
                Assert.Equal(Color.FromArgb(0xFF, 0x15, 0x1B, 0x26), SwatchColor(backgroundSwatch));

                model.Editor.Load(saved);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

                Assert.Equal(Color.FromArgb(0xFF, 0x12, 0x34, 0x56), SwatchColor(foregroundSwatch));
                Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), SwatchColor(backgroundSwatch));

                var iconActions = (WrapPanel)window.FindName("IconActionsPanel");

                Assert.Equal(["reset-icon", "export-icon"], ButtonTags(iconActions));
                AssertSameRow(iconActions);

                var data = expanders.Single(expander => Equals(expander.Header, Strings.Current["Data"]));

                data.IsExpanded = true;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();

                var diagnostics = (WrapPanel)window.FindName("DiagnosticsActionsPanel");
                var transfers = (WrapPanel)window.FindName("SettingsTransferActionsPanel");

                Assert.Collection(
                    diagnostics.Children.Cast<UIElement>(),
                    child => Assert.IsType<CheckBox>(child),
                    child => Assert.Equal("log", Assert.IsType<Button>(child).Tag)
                );
                Assert.Equal(["import", "export"], ButtonTags(transfers));
                AssertSameRow(diagnostics);
                AssertSameRow(transfers);

                window.Width = window.MinWidth;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();

                AssertWrapped(transfers);

                icon.IsExpanded = true;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                AssertWrapped(iconActions);
            }
            finally
            {
                window.Exit();
            }
        });
    }

    private static MainWindow OpenPreferences(CalendarViewModel model)
    {
        var window = new MainWindow(model);

        window.Open(MainPage.Preferences);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();

        return window;
    }

    private static void AssertActionButton(Button button, string glyph, string label)
    {
        Assert.Equal(240, button.ActualWidth, 1);
        Assert.Equal(40, button.ActualHeight, 1);
        Assert.Equal(label, System.Windows.Automation.AutomationProperties.GetName(button));

        var content = Assert.IsType<SpacingStackPanel>(button.Content);
        var children = content.Children.Cast<TextBlock>().ToArray();

        Assert.Equal(2, children.Length);
        Assert.Equal(8, content.Spacing);
        Assert.Equal(glyph, children[0].Text);
        Assert.Equal("Segoe Fluent Icons", children[0].FontFamily.Source);
        Assert.Equal(label, children[1].Text);
        Assert.True(children[0].TranslatePoint(new Point(), content).X
            < children[1].TranslatePoint(new Point(), content).X);
    }

    private static void AssertColorRow(Border swatch, string action)
    {
        var row = Assert.IsType<Grid>(VisualTreeHelper.GetParent(swatch));
        var input = row.Children.OfType<TextBox>().Single();
        var button = row.Children.OfType<Button>().Single();

        Assert.Equal(40, swatch.ActualWidth, 1);
        Assert.Equal(40, swatch.ActualHeight, 1);
        Assert.Equal(160, input.ActualWidth, 1);
        Assert.Equal(40, input.ActualHeight, 1);
        Assert.Equal(240, button.ActualWidth, 1);
        Assert.Equal(40, button.ActualHeight, 1);
        Assert.Equal(action, button.Tag);
        Assert.Equal(0, swatch.TranslatePoint(new Point(), row).Y, 1);
        Assert.Equal(0, input.TranslatePoint(new Point(), row).Y, 1);
        Assert.Equal(0, button.TranslatePoint(new Point(), row).Y, 1);
    }

    private static void AssertSameRow(Panel panel)
    {
        var positions = panel.Children.Cast<UIElement>()
            .Select(child => child.TranslatePoint(new Point(), panel).Y + child.RenderSize.Height / 2)
            .DistinctBy(position => Math.Round(position, 1))
            .ToArray();

        Assert.Single(positions);
    }

    private static void AssertWrapped(Panel panel)
    {
        var positions = panel.Children.Cast<UIElement>()
            .Select(child => child.TranslatePoint(new Point(), panel).Y + child.RenderSize.Height / 2)
            .DistinctBy(position => Math.Round(position, 1))
            .ToArray();

        Assert.True(positions.Length > 1);
    }

    private static string[] ButtonTags(Panel panel) => panel.Children
        .OfType<Button>()
        .Select(button => Assert.IsType<string>(button.Tag))
        .ToArray();

    private static Color SwatchColor(Border swatch) =>
        Assert.IsType<SolidColorBrush>(swatch.Background).Color;

    private static T? Ancestor<T>(DependencyObject? child)
        where T : DependencyObject
    {
        for (var current = child; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
