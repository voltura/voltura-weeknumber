using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class UiStateTests
{
    [Fact]
    public void EditingSettingsDoesNotReplaceChoiceIdentities()
    {
        var editor = new SettingsEditor();
        var themes = editor.Themes;
        var selected = themes[1];
        editor.Load(new AppSettings { Theme = "light", Language = "de" });
        editor.RefreshLabels();
        editor.StartWithWindows = true;
        Assert.Same(themes, editor.Themes); Assert.Same(selected, editor.Themes[1]);
        Assert.Equal("light", editor.Theme); Assert.Equal("de", editor.Language);
    }
}
