using System.Globalization;
using VolturaWeekNumber.Features.Localization.Translations;

namespace VolturaWeekNumber.Features.Localization;

internal sealed record LanguageDefinition(
    string Id,
    string NativeName,
    string CultureName,
    IReadOnlyDictionary<string, string> Entries
);

internal static class LanguageCatalog
{
    internal static IReadOnlyList<LanguageDefinition> All { get; } = Array.AsReadOnly<LanguageDefinition>(
    [
        new("en", "English", "en-GB", English.Entries),
        new("sv", "Svenska", "sv-SE", Swedish.Entries),
        new("de", "Deutsch", "de-DE", German.Entries),
        new("fr", "Français", "fr-FR", French.Entries),
        new("da", "Dansk", "da-DK", Danish.Entries),
        new("fi", "Suomi", "fi-FI", Finnish.Entries),
        new("is", "Íslenska", "is-IS", Icelandic.Entries),
        new("nb", "Norsk bokmål", "nb-NO", Norwegian.Entries),
        new("pl", "Polski", "pl-PL", Polish.Entries),
        new("it", "Italiano", "it-IT", Italian.Entries),
        new("es", "Español", "es-ES", Spanish.Entries),
        new("yue", "粵語", "zh-HK", Cantonese.Entries),
        new("ja", "日本語", "ja-JP", Japanese.Entries),
        new("pt-BR", "Português (Brasil)", "pt-BR", Portuguese.Entries),
        new("zh-Hans", "简体中文", "zh-CN", SimplifiedChinese.Entries),
        new("zh-Hant", "繁體中文", "zh-TW", TraditionalChinese.Entries),
        new("nl", "Nederlands", "nl-NL", Dutch.Entries),
        new("ko", "한국어", "ko-KR", Korean.Entries),
        new("ru", "Русский", "ru-RU", Russian.Entries),
        new("tr", "Türkçe", "tr-TR", Turkish.Entries),
        new("id", "Bahasa Indonesia", "id-ID", Indonesian.Entries),
    ]);

    internal static bool IsSupported(string? id) =>
        id == "system" || All.Any(language => language.Id == id);

    internal static LanguageDefinition Resolve(string id, CultureInfo windowsCulture)
    {
        if (id != "system")
        {
            return All.FirstOrDefault(language => language.Id == id) ?? All[0];
        }

        var parts = windowsCulture.Name.Split('-');
        var root = parts[0].ToLowerInvariant();
        bool Has(string part) => parts.Contains(part, StringComparer.OrdinalIgnoreCase);
        var resolved = root switch
        {
            "yue" => "yue",
            "zh" when Has("HK") => "yue",
            "zh" when Has("Hant") || Has("CHT") => "zh-Hant",
            "zh" when Has("Hans") || Has("CHS") => "zh-Hans",
            "zh" when Has("TW") || Has("MO") => "zh-Hant",
            "zh" => "zh-Hans",
            "no" or "nb" => "nb",
            "pt" => "pt-BR",
            _ => root,
        };

        for (var culture = windowsCulture; ; culture = culture.Parent)
        {
            var match = All.FirstOrDefault(language => language.Id == resolved);
            if (match is not null)
            {
                return match;
            }

            if (culture.Equals(CultureInfo.InvariantCulture))
            {
                return All[0];
            }

            resolved = culture.Parent.Name;
        }
    }
}
