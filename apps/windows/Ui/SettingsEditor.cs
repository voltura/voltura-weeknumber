using System.ComponentModel;
using System.Globalization;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Features.Localization;
using VolturaWeekNumber.Features.Settings;

namespace VolturaWeekNumber.Ui;

public sealed class Choice<T>(T value, string label) : INotifyPropertyChanged
{
    public T Value { get; } = value;
    public string Label { get; private set; } = label;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Relabel(string label)
    {
        Label = label;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
    }
}

public sealed class SettingsEditor : INotifyPropertyChanged
{
    private AppSettings _value = new();
    private AppSettings _saved = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public AppSettings Value => _value;
    public bool HasChanges => _value != _saved;
    public string Language
    {
        get => _value.Language;
        set => Change(_value with { Language = value });
    }
    public string Theme
    {
        get => _value.Theme;
        set => Change(_value with { Theme = value });
    }
    public CalendarMode Mode
    {
        get => _value.Calendar.Mode;
        set => Change(_value with { Calendar = _value.Calendar with { Mode = value } });
    }
    public DayOfWeek FirstDay
    {
        get => _value.Calendar.FirstDay;
        set => Change(_value with { Calendar = _value.Calendar with { FirstDay = value } });
    }
    public CalendarWeekRule Rule
    {
        get => _value.Calendar.Rule;
        set => Change(_value with { Calendar = _value.Calendar with { Rule = value } });
    }
    public bool CustomCalendar => Mode == CalendarMode.Custom;
    public bool CustomIcon => !AutomaticIcon;
    public bool AutomaticIcon
    {
        get => _value.AutomaticIcon;
        set => Change(_value with { AutomaticIcon = value });
    }
    public string Foreground
    {
        get => _value.Foreground;
        set => Change(_value with { Foreground = value });
    }
    public string Background
    {
        get => _value.Background;
        set => Change(_value with { Background = value });
    }
    public bool StartWithWindows
    {
        get => _value.StartWithWindows;
        set => Change(_value with { StartWithWindows = value });
    }
    public bool StartupNotification
    {
        get => _value.StartupNotification;
        set => Change(_value with { StartupNotification = value });
    }
    public bool WeekNotification
    {
        get => _value.WeekNotification;
        set => Change(_value with { WeekNotification = value });
    }
    public bool SilentNotifications
    {
        get => _value.SilentNotifications;
        set => Change(_value with { SilentNotifications = value });
    }
    public bool Logging
    {
        get => _value.Logging;
        set => Change(_value with { Logging = value });
    }
    public bool AutomaticUpdates
    {
        get => _value.AutomaticUpdates;
        set => Change(_value with { AutomaticUpdates = value });
    }

    public void Load(AppSettings settings)
    {
        _saved = settings;
        _value = settings;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public void Edit(AppSettings settings) => Change(settings);

    private void Change(AppSettings settings)
    {
        if (_value == settings)
        {
            return;
        }

        _value = settings;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    // Stable option identities preserve SelectedValue while language and other settings change.
    public IReadOnlyList<Choice<string>> Languages { get; } =
    [
        new("system", ""),
        .. LanguageCatalog.All.Select(language => new Choice<string>(language.Id, language.NativeName)),
    ];
    public IReadOnlyList<Choice<string>> Themes { get; } =
    [new("system", ""), new("light", ""), new("dark", "")];
    public IReadOnlyList<Choice<CalendarMode>> Modes { get; } =
    [new(CalendarMode.Regional, ""), new(CalendarMode.Iso, ""), new(CalendarMode.Custom, "")];
    public IReadOnlyList<Choice<DayOfWeek>> Days { get; } =
        Enum.GetValues<DayOfWeek>().Select(day => new Choice<DayOfWeek>(day, "")).ToArray();
    public IReadOnlyList<Choice<CalendarWeekRule>> Rules { get; } =
    [
        new(CalendarWeekRule.FirstDay, ""),
        new(CalendarWeekRule.FirstFullWeek, ""),
        new(CalendarWeekRule.FirstFourDayWeek, ""),
    ];

    public void RefreshLabels()
    {
        var text = Strings.Current;

        Languages[0].Relabel(text["System"]);
        Themes[0].Relabel(text["System"]);
        Themes[1].Relabel(text["Light"]);
        Themes[2].Relabel(text["Dark"]);
        Modes[0].Relabel(text["Regional"]);
        Modes[1].Relabel(text["Iso"]);
        Modes[2].Relabel(text["Custom"]);

        foreach (var day in Days)
        {
            day.Relabel(text.Culture.DateTimeFormat.GetDayName(day.Value));
        }

        Rules[0].Relabel(text["FirstDayRule"]);
        Rules[1].Relabel(text["FirstFullRule"]);
        Rules[2].Relabel(text["FirstFourRule"]);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
