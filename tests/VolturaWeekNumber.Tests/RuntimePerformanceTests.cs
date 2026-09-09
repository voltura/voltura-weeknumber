using System.Reflection;
using System.Windows;
using System.Windows.Media;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class RuntimePerformanceTests(WpfTestFixture fixture)
{
    private static readonly string[] ThemeChoices = ["light", "dark", "system"];
    private static readonly bool[] PaletteModes = [false, true];

    [Fact]
    public void UnchangedThemeReusesFluentResourcesAndBrushes()
    {
        fixture.Run(() =>
        {
            var app = Application.Current;
            string[] keys =
            [
                "WindowBrush",
                "SurfaceBrush",
                "TextBrush",
                "MutedBrush",
                "AccentBrush",
                "BorderBrush",
            ];

            try
            {
                foreach (var choice in ThemeChoices)
                {
                    ThemeManager.Apply(choice);
#pragma warning disable WPF0001
                    Assert.Equal(
                        choice switch
                        {
                            "light" => ThemeMode.Light,
                            "dark" => ThemeMode.Dark,
                            _ => ThemeMode.System,
                        },
                        app.ThemeMode
                    );
#pragma warning restore WPF0001

                    var dictionaries = app.Resources.MergedDictionaries.ToArray();
                    var brushes = keys.Select(key => app.Resources[key]).ToArray();

                    for (var i = 0; i < 100; i++)
                    {
                        ThemeManager.Apply(choice);
                    }

                    Assert.Equal(dictionaries.Length, app.Resources.MergedDictionaries.Count);

                    for (var i = 0; i < dictionaries.Length; i++)
                    {
                        Assert.Same(dictionaries[i], app.Resources.MergedDictionaries[i]);
                    }

                    for (var i = 0; i < keys.Length; i++)
                    {
                        Assert.Same(brushes[i], app.Resources[keys[i]]);
                    }
                }
            }
            finally
            {
                ThemeManager.Apply("system");
            }
        });
    }

    [Fact]
    public void HighContrastTransitionReloadsFluentEvenWhenThemeChoiceIsUnchanged()
    {
        fixture.Run(() =>
        {
            ThemeManager.Apply("system");

            var resources = Application.Current.Resources;
            var dictionaries = resources.MergedDictionaries.ToArray();
            // Simulate the previous contrast state, without changing the user's Windows settings.

            typeof(ThemeManager)
                .GetField("_highContrast", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, !SystemParameters.HighContrast);
            ThemeManager.Apply("system");
            Assert.Contains(
                dictionaries,
                dictionary => !resources.MergedDictionaries.Contains(dictionary)
            );

            var updated = resources.MergedDictionaries.ToArray();

            ThemeManager.Apply("system");

            for (var i = 0; i < updated.Length; i++)
            {
                Assert.Same(updated[i], resources.MergedDictionaries[i]);
            }
        });
    }

    [Fact]
    public void PaletteUpdatesLightDarkAndHighContrastColorsWithoutChangingWindowsSettings()
    {
        fixture.Run(() =>
        {
            var resources = Application.Current.Resources;
            string[] keys =
            [
                "WindowBrush",
                "SurfaceBrush",
                "TextBrush",
                "MutedBrush",
                "AccentBrush",
                "BorderBrush",
            ];

            try
            {
                foreach (var dark in PaletteModes)
                {
                    ThemeManager.ApplyPalette(dark, false);

                    var normal = keys.Select(key =>
                            Assert.IsType<SolidColorBrush>(resources[key]).Color
                        )
                        .ToArray();

                    Assert.Equal(
                        (Color)ColorConverter.ConvertFromString(dark
                            ? "#111720"
                            : "#F4F6FA"),
                        normal[0]
                    );
                    Assert.Equal(
                        (Color)ColorConverter.ConvertFromString(dark
                            ? "#F1F5FB"
                            : "#17253B"),
                        normal[2]
                    );
                    ThemeManager.ApplyPalette(dark, true);

                    Color[] expected =
                    [
                        SystemColors.WindowColor,
                        SystemColors.WindowColor,
                        SystemColors.WindowTextColor,
                        SystemColors.WindowTextColor,
                        SystemColors.HighlightColor,
                        SystemColors.WindowTextColor,
                    ];

                    for (var i = 0; i < keys.Length; i++)
                    {
                        Assert.Equal(
                            expected[i],
                            Assert.IsType<SolidColorBrush>(resources[keys[i]]).Color
                        );
                        // A stale color must be repaired even though the selected palette mode has not changed.
                        resources[keys[i]] = new SolidColorBrush(Colors.Transparent);
                    }

                    ThemeManager.ApplyPalette(dark, true);

                    for (var i = 0; i < keys.Length; i++)
                    {
                        var brush = Assert.IsType<SolidColorBrush>(resources[keys[i]]);

                        Assert.Equal(expected[i], brush.Color);
                        Assert.True(brush.IsFrozen);
                    }

                    ThemeManager.ApplyPalette(dark, false);

                    for (var i = 0; i < keys.Length; i++)
                    {
                        Assert.Equal(
                            normal[i],
                            Assert.IsType<SolidColorBrush>(resources[keys[i]]).Color
                        );
                    }
                }
            }
            finally
            {
                ThemeManager.Apply("system");
            }
        });
    }

    [Fact]
    public void PreviewReusesOnlyMatchingWeekAndColorsWhileDateAndLanguageStayCurrent()
    {
        fixture.Run(() =>
        {
            Strings.Current.SetLanguage("en");

            try
            {
                var settings = new AppSettings
                {
                    Calendar = new() { Mode = CalendarMode.Iso },
                    AutomaticIcon = false,
                };
                var model = new CalendarViewModel();

                model.Apply(settings);
                model.SelectedDate = new DateTime(2026, 9, 7);

                var preview = model.Preview;

                Assert.NotNull(preview);
                Assert.True(preview.IsFrozen);

                for (var i = 0; i < 100; i++)
                {
                    model.Refresh();
                }

                Assert.Same(preview, model.Preview);

                var dateText = model.DateText;

                model.SelectedDate = new DateTime(2026, 9, 8);
                Assert.NotEqual(dateText, model.DateText);
                Assert.Same(preview, model.Preview);
                Strings.Current.SetLanguage("de");
                model.Refresh();
                Assert.StartsWith("Woche ", model.WeekText, StringComparison.Ordinal);
                Assert.Same(preview, model.Preview);
                model.SelectedDate = new DateTime(2026, 9, 14);
                Assert.NotSame(preview, model.Preview);

                foreach (
                    var changed in new[]
                    {
                        settings with
                        {
                            Background = "#FF123456",
                        },
                        settings with
                        {
                            Foreground = "#FF987654",
                        },
                    }
                )
                {
                    preview = model.Preview;

                    var oldAppearance = IconAppearance.Resolve(
                        settings,
                        false,
                        SystemParameters.HighContrast
                    );
                    var newAppearance = IconAppearance.Resolve(
                        changed,
                        false,
                        SystemParameters.HighContrast
                    );

                    model.Apply(changed);
                    model.Refresh();

                    if (oldAppearance == newAppearance)
                    {
                        Assert.Same(preview, model.Preview);
                    }
                    else
                    {
                        Assert.NotSame(preview, model.Preview);
                    }

                    settings = changed;
                }

                model.SelectedDate = null;
                Assert.Null(model.Preview);
                model.SelectedDate = new DateTime(2026, 9, 14);
                Assert.NotNull(model.Preview);
                Assert.NotSame(preview, model.Preview);
            }
            finally
            {
                Strings.Current.SetLanguage("en");
            }
        });
    }

    [Fact]
    public async Task StartupAndSettingsSaveRefreshCalendarOnceEach()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        var refreshes = 0;
        Task operation = Task.CompletedTask;

        try
        {
            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(root, false, true));
                runtime.Model.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is null)
                    {
                        refreshes++;
                    }
                };

                operation = runtime.StartAsync(true);
            });
            await operation;
            fixture.Run(() =>
            {
                Assert.Equal(1, refreshes);
                Assert.NotNull(runtime!.Model.Preview);
                operation = runtime.ApplyReviewSettingsAsync(
                    new AppSettings
                    {
                        Language = "de",
                        AutomaticIcon = false,
                        Background = "#FF123456",
                    }
                );
            });
            await operation;
            fixture.Run(() =>
            {
                Assert.Equal(2, refreshes);
                Assert.StartsWith("Woche ", runtime!.Model.WeekText, StringComparison.Ordinal);
                Assert.False(runtime.Model.Editor.HasChanges);
                Assert.NotNull(runtime.Model.Preview);
            });
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
    public void TrayMenuReusesItemsAndUpdatesTranslatedCommands()
    {
        fixture.Run(() =>
        {
            Strings.Current.SetLanguage("en");

            using var tray = new NativeTray(promoteVisibility: false);

            try
            {
                var menu = Assert.IsType<System.Windows.Forms.ContextMenuStrip>(
                    typeof(NativeTray)
                        .GetField("_menu", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(tray)
                );
                var firstItem = menu.Items[0];
                var renderer = menu.Renderer;

                for (var index = 0; index < 100; index++)
                {
                    tray.RebuildMenu();
                }

                Assert.Same(firstItem, menu.Items[0]);
                Assert.Same(renderer, menu.Renderer);
                Strings.Current.SetLanguage("de");
                tray.RebuildMenu();
                Assert.True(firstItem.IsDisposed);
                Assert.Equal("Kalenderwoche", menu.Items[0].Text);

                var opened = 0;

                tray.OpenRequested += () => opened++;
                menu.Items[0].PerformClick();
                Assert.Equal(1, opened);
            }
            finally
            {
                Strings.Current.SetLanguage("en");
            }
        });
    }
}
