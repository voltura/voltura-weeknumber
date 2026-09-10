using System.Reflection;
using System.Windows;
using System.Windows.Automation;
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
public sealed class AlwaysOnTopTests(WpfTestFixture fixture)
{
    [Fact]
    public async Task HeaderAndPreferenceToggleOnePersistedWindowSetting()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        Task operation = Task.CompletedTask;

        try
        {
            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(root, false, true));
                operation = runtime.StartAsync(true);
            });
            await operation;
            fixture.Run(() =>
            {
                runtime!.Window.Open();
                runtime.Model.Editor.StartWithWindows = true;

                var pin = HeaderPin(runtime.Window);

                pin.IsChecked = true;
                pin.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                pin.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            });
            await WaitForActions(runtime!);

            fixture.Run(() =>
            {
                Assert.True(runtime!.Window.Topmost);
                Assert.True(HeaderPin(runtime.Window).IsChecked);
                Assert.True(Assert.IsType<CheckBox>(
                    runtime.Window.FindName("AlwaysOnTopCheck")
                ).IsChecked);
                Assert.True(runtime.Model.Editor.StartWithWindows);
                Assert.True(runtime.Model.Editor.HasChanges);

                foreach (var page in Enum.GetValues<MainPage>())
                {
                    runtime.Window.Open(page);
                    Assert.True(runtime.Window.Topmost);
                }

                runtime.Window.Hide();
                runtime.Window.Open(MainPage.Calendar);
                Assert.True(runtime.Window.Topmost);
            });

            var saved = await SettingsStore.ReadAsync(Path.Combine(root, "settings.json"));

            Assert.True(saved.AlwaysOnTop);
            Assert.False(saved.StartWithWindows);

            fixture.Run(() => operation = runtime!.DisposeAsync().AsTask());
            await operation;
            runtime = null;

            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(root, false, true));
                operation = runtime.StartAsync(true);
            });
            await operation;
            fixture.Run(() =>
            {
                Assert.True(runtime!.Window.Topmost);
                runtime.Window.Open(MainPage.Preferences);

                var preference = Assert.IsType<CheckBox>(
                    runtime.Window.FindName("AlwaysOnTopCheck")
                );

                preference.IsChecked = false;
                preference.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                preference.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            });
            await WaitForActions(runtime!);

            fixture.Run(() =>
            {
                Assert.False(runtime!.Window.Topmost);
                Assert.False(HeaderPin(runtime.Window).IsChecked);
            });
            Assert.False((await SettingsStore.ReadAsync(
                Path.Combine(root, "settings.json")
            )).AlwaysOnTop);
        }
        finally
        {
            fixture.Run(() => operation = runtime?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await operation;
            fixture.Run(() => Strings.Current.SetLanguage("en"));

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void PinStaysRightOfNavigationAndPreferencesStayOnOneRow()
    {
        fixture.Run(() =>
        {
            var window = new MainWindow(new CalendarViewModel())
            {
                Width = 560,
                Height = 700,
            };

            try
            {
                window.Open(MainPage.Preferences);

                foreach (var language in LanguageCatalog.All)
                {
                    Strings.Current.SetLanguage(language.Id);
                    window.UpdateLanguage();
                    Idle(window);
                    window.UpdateLayout();

                    var tabs = Assert.IsType<TabControl>(window.FindName("Tabs"));
                    var pin = HeaderPin(window);
                    var pinBounds = BoundsIn(pin, tabs);

                    foreach (var tab in tabs.Items.OfType<TabItem>())
                    {
                        Assert.False(pinBounds.IntersectsWith(BoundsIn(tab, tabs)), language.Id);
                    }

                    var start = Assert.IsType<CheckBox>(
                        window.FindName("StartWithWindowsCheck")
                    );
                    var alwaysOnTop = Assert.IsType<CheckBox>(
                        window.FindName("AlwaysOnTopCheck")
                    );

                    Assert.Equal(
                        start.TranslatePoint(new Point(), window).Y,
                        alwaysOnTop.TranslatePoint(new Point(), window).Y,
                        1
                    );
                    Assert.Equal(
                        Strings.Current["AlwaysOnTop"],
                        AutomationProperties.GetName(pin)
                    );
                    Assert.NotNull(
                        new ToggleButtonAutomationPeer(pin).GetPattern(PatternInterface.Toggle)
                            as IToggleProvider
                    );
                }
            }
            finally
            {
                window.Exit();
            }
        });
    }

    [Fact]
    public async Task FailedImmediateSaveRestoresTheSavedWindowState()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        Task operation = Task.CompletedTask;

        try
        {
            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(root, false, true));
                operation = runtime.StartAsync(true);
            });
            await operation;
            Directory.CreateDirectory(Path.Combine(root, "settings.json"));
            fixture.Run(() =>
            {
                runtime!.Window.Open();

                var pin = HeaderPin(runtime.Window);

                pin.IsChecked = true;
                pin.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                pin.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            });
            await WaitForActions(runtime!);

            fixture.Run(() =>
            {
                Assert.False(runtime!.Window.Topmost);
                Assert.False(runtime.Model.Editor.AlwaysOnTop);
                Assert.False(HeaderPin(runtime.Window).IsChecked);
                Assert.NotEmpty(runtime.Model.Status);
            });
        }
        finally
        {
            fixture.Run(() => operation = runtime?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await operation;

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void SelectedTabsMatchTheActivePinAndTheGlyphIsLifted()
    {
        fixture.Run(() =>
        {
            var window = new MainWindow(new CalendarViewModel());

            try
            {
                window.Open();

                var tabs = Assert.IsType<TabControl>(window.FindName("Tabs"));
                var pin = HeaderPin(window);

                pin.IsChecked = true;
                pin.ApplyTemplate();
                Idle(window);

                var pinChrome = Assert.IsType<Border>(
                    pin.Template.FindName("Chrome", pin)
                );
                var glyph = Assert.IsType<TextBlock>(pin.Content);

                Assert.Equal(-1, Assert.IsType<TranslateTransform>(glyph.RenderTransform).Y);

                foreach (var tab in tabs.Items.OfType<TabItem>())
                {
                    tab.IsSelected = true;
                    tab.ApplyTemplate();
                    Idle(window);

                    var tabChrome = Assert.IsType<Border>(
                        tab.Template.FindName("Chrome", tab)
                    );

                    Assert.Equal(pinChrome.Background, tabChrome.Background);
                    Assert.Equal(pinChrome.BorderBrush, tabChrome.BorderBrush);
                    Assert.Equal(pinChrome.BorderThickness, tabChrome.BorderThickness);
                    Assert.Equal(pinChrome.CornerRadius, tabChrome.CornerRadius);
                    Assert.Equal(pin.Foreground, tab.Foreground);
                }
            }
            finally
            {
                window.Exit();
            }
        });
    }

    [Fact]
    public void SelectedPagesStretchFromTheTopInsteadOfCenteringWithTheirTabs()
    {
        fixture.Run(() =>
        {
            var window = new MainWindow(new CalendarViewModel())
            {
                Width = 720,
                Height = 640,
            };

            try
            {
                window.Open(MainPage.Preferences);
                Idle(window);
                window.UpdateLayout();

                var tabs = Assert.IsType<TabControl>(window.FindName("Tabs"));
                var contentHost = Assert.IsType<ContentPresenter>(
                    tabs.Template.FindName("PART_SelectedContentHost", tabs)
                );
                var preferences = Assert.IsType<ScrollViewer>(
                    window.FindName("PreferencesScroll")
                );
                var save = Assert.IsType<Button>(window.FindName("SaveChangesButton"));
                var page = Assert.IsType<DockPanel>(VisualTreeHelper.GetParent(preferences));
                var pageBounds = BoundsIn(preferences, contentHost);
                var saveBounds = BoundsIn(save, contentHost);

                Assert.Equal(HorizontalAlignment.Stretch, ((TabItem)tabs.SelectedItem).HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Stretch, ((TabItem)tabs.SelectedItem).VerticalContentAlignment);
                Assert.Equal(contentHost.ActualWidth, page.ActualWidth, 1);
                Assert.Equal(0, pageBounds.X, 1);
                Assert.InRange(pageBounds.Y, 13, 15);
                Assert.Equal(contentHost.ActualWidth, preferences.ActualWidth, 1);
                Assert.Equal(contentHost.ActualHeight, saveBounds.Bottom, 1);
            }
            finally
            {
                window.Exit();
            }
        });
    }

    private static ToggleButton HeaderPin(MainWindow window)
    {
        var tabs = Assert.IsType<TabControl>(window.FindName("Tabs"));

        tabs.ApplyTemplate();

        return Assert.IsType<ToggleButton>(
            tabs.Template.FindName("HeaderPinToggle", tabs)
        );
    }

    private static Rect BoundsIn(FrameworkElement element, UIElement ancestor) =>
        new(element.TranslatePoint(new Point(), ancestor), element.RenderSize);

    private static async Task WaitForActions(AppRuntime runtime)
    {
        var actions = (SemaphoreSlim)typeof(AppRuntime)
            .GetField("_actions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(runtime)!;

        Assert.True(await actions.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken
        ));
        actions.Release();
    }

    private static void Idle(MainWindow window) =>
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
