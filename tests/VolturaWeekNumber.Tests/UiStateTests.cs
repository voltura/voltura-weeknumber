using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class UiStateTests
{
    [Fact]
    public void ChangesAreComparedToLastSavedSettings()
    {
        var saved = new AppSettings();
        var editor = new SettingsEditor();

        editor.Load(saved);
        Assert.False(editor.HasChanges);
        editor.StartWithWindows = !saved.StartWithWindows;
        Assert.True(editor.HasChanges);
        editor.StartWithWindows = saved.StartWithWindows;
        Assert.False(editor.HasChanges);
        editor.FirstDay = DayOfWeek.Tuesday;
        Assert.True(editor.HasChanges);
        editor.Load(editor.Value);
        Assert.False(editor.HasChanges);
        editor.Theme = "dark";
        editor.Load(saved);
        Assert.False(editor.HasChanges);
    }

    [Fact]
    public void DraftEditsPreserveSavedBaseline()
    {
        var editor = new SettingsEditor();
        var saved = new AppSettings { AutomaticIcon = false };

        editor.Load(saved);
        editor.Edit(saved with
        {
            AutomaticIcon = true
        });
        Assert.True(editor.HasChanges);

        var draft = editor.Value with
        {
            AutomaticUpdates = false
        };

        editor.Load(saved with
        {
            AutomaticUpdates = false
        });
        editor.Edit(draft);
        Assert.True(editor.HasChanges);
        editor.AutomaticIcon = false;
        Assert.False(editor.HasChanges);
    }

    [Fact]
    public void EditingSettingsDoesNotReplaceChoiceIdentities()
    {
        var editor = new SettingsEditor();
        var themes = editor.Themes;
        var selected = themes[1];

        editor.Load(new AppSettings { Theme = "light", Language = "de" });
        editor.RefreshLabels();
        editor.StartWithWindows = true;
        Assert.Same(themes, editor.Themes);
        Assert.Same(selected, editor.Themes[1]);
        Assert.Equal("light", editor.Theme);
        Assert.Equal("de", editor.Language);
    }
}
