using System.Text.RegularExpressions;
using VolturaWeekNumber.Features.Localization;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed partial class InstallerLocalizationTests
{
    [Fact]
    public void InstallerOffersEveryApplicationLanguageWithMatchingNativeNames()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InstallerLanguages.nsh"));
        var languages = LanguageDeclarations().Matches(source);

        Assert.Equal(LanguageCatalog.All.Count, languages.Count);
        Assert.Equal(languages.Count, languages.Select(match => match.Groups[2].Value).Distinct().Count());
        Assert.Equal(
            LanguageCatalog.All.Select(language => (language.Id, language.NativeName)),
            languages.Select(match => (match.Groups[1].Value, match.Groups[3].Value)));

        var messages = MessageDeclarations().Matches(source);

        Assert.Equal(languages.Count, messages.Count);
        Assert.Equal(
            languages.Select(match => match.Groups[2].Value).Order(),
            messages.Select(match => match.Groups[1].Value).Order());

        foreach (Match message in messages)
        {
            // Each language must provide all four application-owned messages.
            for (var group = 2; group <= 5; group++)
            {
                Assert.False(string.IsNullOrWhiteSpace(message.Groups[group].Value));
                Assert.DoesNotContain('\uFFFD', message.Groups[group].Value);
            }
        }
    }

    [GeneratedRegex("(?m)^!insertmacro VolturaLanguage \"([^\"]+)\" \"([^\"]+)\" \"([^\"]+)\"")]
    private static partial Regex LanguageDeclarations();

    [GeneratedRegex("(?m)^!insertmacro VolturaSetupStrings (\\w+)\\s+\\\\\\s+\"([^\"]+)\"\\s+\\\\\\s+\"([^\"]+)\"\\s+\\\\\\s+\"([^\"]+)\"\\s+\\\\\\s+\"([^\"]+)\"")]
    private static partial Regex MessageDeclarations();
}
