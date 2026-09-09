using System.Globalization;
using System.Text.RegularExpressions;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Features.Settings;

public sealed record AppSettings
{
    public int Schema { get; init; } = 1;
    public string Language { get; init; } = "system";
    public string Theme { get; init; } = "system";
    public CalendarOptions Calendar { get; init; } = new();
    public bool StartWithWindows { get; init; }
    public bool StartupNotification { get; init; } = true;
    public bool WeekNotification { get; init; } = true;
    public bool SilentNotifications { get; init; } = true;
    public bool AutomaticIcon { get; init; } = true;
    public string Foreground { get; init; } = "#FFFFFFFF";
    public string Background { get; init; } = "#FF151B26";
    public bool Logging { get; init; }
    public bool AutomaticUpdates { get; init; } = true;
    public void Validate()
    {
        if (Schema != 1)
        {
            throw new InvalidDataException("Unsupported settings version.");
        }

        if (
            Language is not ("system" or "en" or "sv" or "de")
            || Theme is not ("system" or "light" or "dark")
        )
        {
            throw new InvalidDataException("Invalid language or appearance.");
        }

        if (
            Calendar is null
            || !Enum.IsDefined(Calendar.Mode)
            || !Enum.IsDefined(Calendar.FirstDay)
            || !Enum.IsDefined(Calendar.Rule)
        )
        {
            throw new InvalidDataException("Invalid calendar options.");
        }

        foreach (var color in new[] { Foreground, Background })
        {
            ValidateColor(color);
        }
    }

    public static void ValidateColor(string color)
    {
        if (
            color is not { Length: 9 }
            || !Regex.IsMatch(
                color,
                "^#[0-9A-Fa-f]{8}$",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)
            )
        )
        {
            throw new InvalidDataException("Colors must use #AARRGGBB.");
        }
    }
}
