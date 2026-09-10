using System.Globalization;
using System.Text;
using VolturaWeekNumber.Features.Localization;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class LocalizationTests
{
    public static TheoryData<string> Languages =>
        new(LanguageCatalog.All.Select(language => language.Id));

    [Fact]
    public void CatalogHasCompleteTranslationsAndValidPlaceholders()
    {
        Assert.Equal(21, LanguageCatalog.All.Count);
        Assert.Equal(21, LanguageCatalog.All.Select(language => language.Id).Distinct().Count());

        var english = LanguageCatalog.All[0].Entries;

        Assert.Equal(115, english.Count);

        foreach (var language in LanguageCatalog.All)
        {
            Assert.IsType<GregorianCalendar>(CultureInfo.GetCultureInfo(language.CultureName).Calendar);
            Assert.Equal(english.Keys.Order(), language.Entries.Keys.Order());
            Assert.False(string.IsNullOrWhiteSpace(language.NativeName));

            foreach (var (key, value) in language.Entries)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language.Id}/{key}");
                Assert.DoesNotContain('\uFFFD', value);

                var format = CompositeFormat.Parse(value);

                Assert.Equal(CompositeFormat.Parse(english[key]).MinimumArgumentCount, format.MinimumArgumentCount);

                if (key == "WeekNumberFormat")
                {
                    var formatted = string.Format(CultureInfo.InvariantCulture, value, "37");

                    Assert.Contains("37", formatted, StringComparison.Ordinal);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public async Task LanguageSurvivesSaveRestartAndExportImport(string language)
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var settings = new AppSettings { Language = language };

            settings.Validate();

            using (var store = new SettingsStore(root))
            {
                await store.SaveAsync(settings);
            }

            using var restarted = new SettingsStore(root);

            await restarted.LoadAsync();
            Assert.Equal(settings, restarted.Current);
            Assert.Equal(1, restarted.Current.Schema);

            var export = Path.Combine(root, "export.json");

            await SettingsStore.WriteAsync(export, restarted.Current);

            var imported = await SettingsStore.ReadAsync(export);

            await restarted.SaveAsync(imported);
            Assert.Equal(settings, restarted.Current);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("sv-FI", "sv")]
    [InlineData("de-AT", "de")]
    [InlineData("fr-CA", "fr")]
    [InlineData("da-DK", "da")]
    [InlineData("fi-FI", "fi")]
    [InlineData("is-IS", "is")]
    [InlineData("nb", "nb")]
    [InlineData("nb-NO", "nb")]
    [InlineData("no", "nb")]
    [InlineData("no-NO", "nb")]
    [InlineData("nn-NO", "en")]
    [InlineData("pl-PL", "pl")]
    [InlineData("it-CH", "it")]
    [InlineData("es-MX", "es")]
    [InlineData("ja-JP", "ja")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt-AO", "pt-BR")]
    [InlineData("pt", "pt-BR")]
    [InlineData("zh", "zh-Hans")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("zh-Hant", "zh-Hant")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-MO", "zh-Hant")]
    [InlineData("zh-HK", "yue")]
    [InlineData("zh-Hant-HK", "yue")]
    [InlineData("zh-Hans-HK", "yue")]
    [InlineData("zh-Hans-TW", "zh-Hans")]
    [InlineData("zh-Hant-CN", "zh-Hant")]
    [InlineData("yue", "yue")]
    [InlineData("yue-Hant-HK", "yue")]
    [InlineData("yue-Hans-CN", "yue")]
    [InlineData("nl-BE", "nl")]
    [InlineData("ko-KR", "ko")]
    [InlineData("ru-RU", "ru")]
    [InlineData("tr-TR", "tr")]
    [InlineData("id-ID", "id")]
    [InlineData("ar-SA", "en")]
    [InlineData("hi-IN", "en")]
    [InlineData("", "en")]
    public void FollowWindowsResolvesRegionalAndScriptVariants(string culture, string expected) =>
        Assert.Equal(expected, LanguageCatalog.Resolve("system", CultureInfo.GetCultureInfo(culture)).Id);

    [Theory]
    [MemberData(nameof(Languages))]
    public void ExplicitSelectionOverridesWindowsAndKeepsChoiceIdentities(string language)
    {
        Assert.Equal(language, LanguageCatalog.Resolve(language, CultureInfo.GetCultureInfo("ar-SA")).Id);

        var editor = new SettingsEditor();
        var choices = editor.Languages.ToArray();

        Assert.Equal(22, choices.Length);
        Assert.Equal("system", choices[0].Value);
        Assert.Equal(LanguageCatalog.All.Select(item => item.Id), choices.Skip(1).Select(item => item.Value));
        editor.Load(new AppSettings());
        editor.Language = language;
        Assert.True(editor.HasChanges);
        editor.RefreshLabels();
        Assert.Equal(language, editor.Language);
        Assert.True(choices.Zip(editor.Languages).All(pair => ReferenceEquals(pair.First, pair.Second)));
        editor.Load(new AppSettings());
        Assert.Equal("system", editor.Language);
        Assert.False(editor.HasChanges);
        editor.Language = language;
        editor.Load(editor.Value);
        Assert.Equal(language, editor.Language);
        Assert.False(editor.HasChanges);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("pt-PT")]
    [InlineData("zh-HK")]
    [InlineData("nn")]
    [InlineData("")]
    [InlineData(null)]
    public void NonCanonicalSavedIdentifiersAreRejected(string? language) =>
        Assert.Throws<InvalidDataException>(() => new AppSettings { Language = language! }.Validate());

    [Fact]
    public void EastAsianWeekPhrasesAndMissingWeekKeepNaturalOrder()
    {
        var strings = new Strings();

        strings.SetLanguage("ja");
        Assert.Equal("第37週", strings.WeekNumber(37));
        strings.SetLanguage("ko");
        Assert.Equal("37주차", strings.WeekNumber(37));
        strings.SetLanguage("yue");
        Assert.Equal("第 37 週", strings.WeekNumber(37));
        Assert.Equal("第 — 週", strings.WeekNumber(null));
        strings.SetLanguage("zh-Hans");
        Assert.Equal("第 37 周", strings.WeekNumber(37));
    }
}
