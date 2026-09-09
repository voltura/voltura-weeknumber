using System.ComponentModel;
using System.Globalization;
using VolturaWeekNumber.Features.Localization;

namespace VolturaWeekNumber.Ui;

public sealed class Strings : INotifyPropertyChanged
{
    public static Strings Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    private LanguageDefinition _language = LanguageCatalog.All[0];
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-GB");

    public string this[string key] =>
        _language.Entries.TryGetValue(key, out var value)
            ? value
            : key;

    public string WeekNumber(int? number) => string.Format(
        Culture,
        this["WeekNumberFormat"],
        number?.ToString("D2", CultureInfo.InvariantCulture) ?? "—"
    );

    public void SetLanguage(string language)
    {
        _language = LanguageCatalog.Resolve(language, CultureInfo.CurrentUICulture);
        Culture = CultureInfo.GetCultureInfo(_language.CultureName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
