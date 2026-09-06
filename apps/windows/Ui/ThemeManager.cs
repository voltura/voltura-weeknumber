using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace VolturaWeekNumber.Ui;

internal static class ThemeManager
{
    internal static bool IsTaskbarDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 0;
    }

    internal static void Apply(string choice)
    {
        var app = System.Windows.Application.Current;
        // Fluent theme selection is annotated experimental in .NET 10; isolate its use here.
#pragma warning disable WPF0001
        app.ThemeMode = choice switch { "light" => ThemeMode.Light, "dark" => ThemeMode.Dark, _ => ThemeMode.System };
#pragma warning restore WPF0001
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var dark = choice == "dark" || (choice == "system" && key?.GetValue("AppsUseLightTheme") is int value && value == 0);
        var hc = SystemParameters.HighContrast;
        app.Resources["WindowBrush"] = hc ? SystemColors.WindowBrush : Brush(dark ? "#111720" : "#F4F6FA");
        app.Resources["SurfaceBrush"] = hc ? SystemColors.WindowBrush : Brush(dark ? "#1B2431" : "#FFFFFF");
        app.Resources["TextBrush"] = hc ? SystemColors.WindowTextBrush : Brush(dark ? "#F1F5FB" : "#17253B");
        app.Resources["MutedBrush"] = hc ? SystemColors.WindowTextBrush : Brush(dark ? "#B4C2D3" : "#52627A");
        app.Resources["AccentBrush"] = hc ? SystemColors.HighlightBrush : Brush(dark ? "#87B5FF" : "#245CB4");
        app.Resources["BorderBrush"] = hc ? SystemColors.WindowTextBrush : Brush(dark ? "#354155" : "#D9E0EB");
    }
    private static SolidColorBrush Brush(string value) => new((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));
}
