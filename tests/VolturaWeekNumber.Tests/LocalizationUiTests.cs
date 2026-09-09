using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Localization;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Features.Updates;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;
using CalendarMode = VolturaWeekNumber.Features.Calendar.CalendarMode;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class LocalizationUiTests(WpfTestFixture fixture)
{
    private static readonly string[] ColorChannels = ["Red", "Green", "Blue", "Opacity"];
    [Fact]
    public async Task SavedLanguagesRefreshWindowDatesTrayAndUpdateStatesWithoutChangingWeekRules()
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));
        AppRuntime? runtime = null;
        Task operation = Task.CompletedTask;
        var selected = new DateTime(2020, 12, 31);

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
                runtime!.Model.SelectedDate = selected;
                runtime.Model.DayOfYear.SelectedDate = selected;
                runtime.Model.JulianDay.SelectedDate = selected;
            });

            foreach (var language in LanguageCatalog.All)
            {
                foreach (var mode in Enum.GetValues<CalendarMode>())
                {
                    fixture.Run(() => operation = runtime!.ApplyReviewSettingsAsync(new AppSettings
                    {
                        Language = language.Id,
                        Calendar = new CalendarOptions { Mode = mode },
                    }));
                    await operation;
                    fixture.Run(() =>
                    {
                        var model = runtime!.Model;
                        var strings = Strings.Current;

                        Assert.Equal(selected, model.SelectedDate);
                        Assert.Equal(selected, model.DayOfYear.SelectedDate);
                        Assert.Equal(selected, model.JulianDay.SelectedDate);
                        model.Refresh();

                        var result = WeekCalculator.Calculate(
                            DateOnly.FromDateTime(selected), model.Editor.Value.Calendar, CultureInfo.CurrentCulture);

                        Assert.Equal(strings.WeekNumber(result.Number), model.WeekText);
                        Assert.Equal(selected.ToString("D", strings.Culture), model.DateText);
                        Assert.Equal(model.DateText, model.DayOfYear.DateText);
                        Assert.Equal(model.DateText, model.JulianDay.DateText);
                        Assert.Equal("366", model.DayOfYear.Result);
                        Assert.Equal(language.CultureName, runtime.Window.Language.IetfLanguageTag, ignoreCase: true);
                        Assert.False(model.Editor.HasChanges);
                        Assert.Equal(language.Id, model.Editor.Language);
                        Assert.Equal(strings.Culture.DateTimeFormat.GetDayName(DayOfWeek.Monday),
                            model.Editor.Days.Single(day => day.Value == DayOfWeek.Monday).Label);

                        var tray = (NativeTray)typeof(AppRuntime).GetField("_tray", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(runtime)!;
                        var menu = (System.Windows.Forms.ContextMenuStrip)typeof(NativeTray).GetField("_menu", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(tray)!;

                        Assert.Equal(strings["WeekTab"], menu.Items[0].Text);
                        Assert.Equal(strings["Preferences"], menu.Items[1].Text);
                        Assert.Equal(strings["Exit"], menu.Items[3].Text);

                        foreach (var status in Enum.GetValues<UpdateStatus>())
                        {
                            runtime.Window.UpdateState(new UpdateState(status), true);
                            Assert.Equal(strings[status.ToString()],
                                ((TextBlock)runtime.Window.FindName("UpdateStatus")).Text);
                        }
                    });
                }
            }
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
    public void TranslatedButtonsFitAtMinimumWindowWidth()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();
            var window = new MainWindow(model) { Width = 560, Height = 400 };
            var failures = new List<string>();

            try
            {
                foreach (var language in LanguageCatalog.All)
                {
                    Strings.Current.SetLanguage(language.Id);
                    model.Apply(new AppSettings { Language = language.Id });
                    window.UpdateLanguage();

                    foreach (var page in Enum.GetValues<MainPage>())
                    {
                        window.Open(page);
                        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        window.Width = window.MinWidth;
                        window.Height = 400;

                        if (page == MainPage.About)
                        {
                            window.UpdateState(new UpdateState(UpdateStatus.Ready, "review-only"), true);
                        }

                        foreach (var expander in Descendants(window).OfType<Expander>())
                        {
                            expander.IsExpanded = true;
                        }

                        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        window.UpdateLayout();
                        Assert.Equal(window.MinWidth, window.ActualWidth, 1);

                        foreach (var datePicker in Descendants(window).OfType<DatePicker>())
                        {
                            Assert.Equal(DatePickerFormat.Short, datePicker.SelectedDateFormat);
                            Assert.Equal(datePicker.SelectedDate?.ToString("d", Strings.Current.Culture), datePicker.Text);

                            var input = Descendants(datePicker).OfType<System.Windows.Controls.Primitives.DatePickerTextBox>().Single();
                            var dateText = new FormattedText(input.Text, Strings.Current.Culture, FlowDirection.LeftToRight,
                                new Typeface(input.FontFamily, input.FontStyle, input.FontWeight, input.FontStretch),
                                input.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(input).PixelsPerDip);

                            Assert.True(dateText.Width <= input.ActualWidth - input.Padding.Left - input.Padding.Right,
                                $"{language.Id}/{page}: date input is too narrow");
                        }

                        foreach (var button in Descendants(window).OfType<Button>())
                        {
                            var label = LocalizedButtonLabel(button);

                            if (label is null || button.ActualWidth == 0)
                            {
                                continue;
                            }

                            Assert.Equal(240, button.ActualWidth, 1);
                            Assert.Equal(40, button.ActualHeight, 1);

                            var text = new FormattedText(label.Value.Text, Strings.Current.Culture, FlowDirection.LeftToRight,
                                new Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch),
                                button.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(button).PixelsPerDip);
                            var contentWidth = label.Value.Container?.DesiredSize.Width ?? text.Width;
                            if (contentWidth > button.ActualWidth - button.Padding.Left - button.Padding.Right + 2)
                            {
                                failures.Add($"{language.Id}/{page}: {label.Value.Text} ({contentWidth:F1}px in {button.ActualWidth}px button)");
                            }
                        }
                    }
                }

                Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct()));
            }
            finally
            {
                window.Exit();
                Strings.Current.SetLanguage("en");
            }
        });
    }

    [Fact]
    public void LanguageChoicesAndActionsRemainKeyboardAccessibleWithSystemColors()
    {
        fixture.Run(() =>
        {
            var model = new CalendarViewModel();
            var window = new MainWindow(model);

            try
            {
                ThemeManager.ApplyPalette(false, true);

                foreach (var language in LanguageCatalog.All)
                {
                    Strings.Current.SetLanguage(language.Id);
                    model.Apply(new AppSettings { Language = language.Id });
                    window.UpdateLanguage();
                    window.Open(MainPage.Preferences);
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                    var picker = Descendants(window).OfType<ComboBox>()
                        .Single(combo => ReferenceEquals(combo.ItemsSource, model.Editor.Languages));

                    Assert.Equal(language.Id, picker.SelectedValue);
                    Assert.True(picker.Focusable && picker.IsTabStop);
                    Assert.Equal(Strings.Current["LanguageHeading"], System.Windows.Automation.AutomationProperties.GetName(picker));
                    model.Editor.Theme = "dark";
                    window.UpdateLayout();

                    var save = (Button)window.FindName("SaveChangesButton");
                    var discard = (Button)window.FindName("DiscardChangesButton");

                    Assert.True(save.IsEnabled && discard.IsEnabled);
                    Assert.True(save.Focus());
                    Assert.True(save.MoveFocus(new System.Windows.Input.TraversalRequest(
                        System.Windows.Input.FocusNavigationDirection.Next)));
                    Assert.True(discard.IsKeyboardFocusWithin);
                    Assert.Equal(SystemColors.WindowColor, ((SolidColorBrush)window.Background).Color);
                    Assert.Equal(SystemColors.WindowTextColor, ((SolidColorBrush)window.Foreground).Color);

                    var colors = new ColorPickerWindow(Strings.Current["Background"], "#FF123456");

                    try
                    {
                        foreach (var channel in ColorChannels)
                        {
                            var slider = (Slider)colors.FindName(channel == "Opacity"
                                ? "Alpha"
                                : channel);

                            slider.GetBindingExpression(System.Windows.Automation.AutomationProperties.NameProperty)!.UpdateTarget();
                            Assert.Equal(Strings.Current[channel], System.Windows.Automation.AutomationProperties.GetName(slider));
                        }
                    }
                    finally
                    {
                        colors.Close();
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

    private static (string Text, FrameworkElement? Container)? LocalizedButtonLabel(Button button)
    {
        if (button.Content is string label
            && System.Windows.Data.BindingOperations.GetBindingExpression(
                button,
                ContentControl.ContentProperty
            )?.ParentBinding.Source is Strings)
        {
            return (label, null);
        }

        if (button.Content is not FrameworkElement container)
        {
            return null;
        }

        var localizedLabel = Descendants(container)
            .Prepend(container)
            .OfType<TextBlock>()
            .SingleOrDefault(text =>
                System.Windows.Data.BindingOperations.GetBindingExpression(
                    text,
                    TextBlock.TextProperty
                )?.ParentBinding.Source is Strings
            );

        return localizedLabel is null
            ? null
            : (localizedLabel.Text, container);
    }
}
