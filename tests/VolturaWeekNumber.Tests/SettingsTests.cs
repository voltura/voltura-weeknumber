using System.Text.Json;
using VolturaWeekNumber.Features.Settings;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "VolturaWeekNumber-tests",
        Guid.NewGuid().ToString("N")
    );

    [Fact]
    public async Task RoundTripPreservesAllSettings()
    {
        using var store = new SettingsStore(_directory);
        var settings = new AppSettings
        {
            Language = "sv",
            Theme = "dark",
            Background = "#00123456",
            Logging = true,
            AlwaysOnTop = true,
            WeekNumberShortcut = new(true, true, false, false, 0x57),
            CalendarShortcut = new(false, true, true, false, 0x43),
        };

        await store.SaveAsync(settings);
        Assert.Equal(settings, await SettingsStore.ReadAsync(store.FilePath));
        Assert.Empty(Directory.GetFiles(_directory, "*.pending"));
    }

    [Fact]
    public async Task OlderSettingsKeepNewOptionsAtTheirDefaults()
    {
        Directory.CreateDirectory(_directory);

        var path = Path.Combine(_directory, "legacy.json");

        await File.WriteAllTextAsync(
            path,
            """{"Schema":1}""",
            TestContext.Current.CancellationToken
        );

        var settings = await SettingsStore.ReadAsync(path);

        Assert.False(settings.AlwaysOnTop);
        Assert.Null(settings.WeekNumberShortcut);
        Assert.Null(settings.CalendarShortcut);
    }

    [Fact]
    public async Task ClearedShortcutsRoundTripThroughExportAndImport()
    {
        Directory.CreateDirectory(_directory);

        var path = Path.Combine(_directory, "export.json");
        var assigned = new AppSettings
        {
            WeekNumberShortcut = new(false, true, true, false, 0x57),
        };

        await SettingsStore.WriteAsync(path, assigned);

        var imported = await SettingsStore.ReadAsync(path);

        Assert.Equal(assigned.WeekNumberShortcut, imported.WeekNumberShortcut);

        var cleared = imported with { WeekNumberShortcut = null };

        await SettingsStore.WriteAsync(path, cleared);
        Assert.Null((await SettingsStore.ReadAsync(path)).WeekNumberShortcut);
    }

    [Theory]
    [MemberData(nameof(InvalidShortcuts))]
    public void MalformedActivationShortcutsAreRejected(ActivationShortcut shortcut) =>
        Assert.Throws<InvalidDataException>(() => new AppSettings
        {
            WeekNumberShortcut = shortcut,
        }.Validate());

    public static TheoryData<ActivationShortcut> InvalidShortcuts =>
    [
        new ActivationShortcut(false, false, false, false, 0x57),
        new ActivationShortcut(false, true, false, false, 0),
        new ActivationShortcut(false, true, false, false, 0x11),
        new ActivationShortcut(false, true, false, false, 0x7B),
        new ActivationShortcut(false, false, false, true, 0x09),
    ];

    [Fact]
    public void DuplicateActivationShortcutsAreRejected()
    {
        var shortcut = new ActivationShortcut(false, true, true, false, 0x57);

        Assert.Throws<InvalidDataException>(() => new AppSettings
        {
            WeekNumberShortcut = shortcut,
            CalendarShortcut = shortcut,
        }.Validate());
    }

    [Fact]
    public async Task BadImportAndInvalidSaveLeaveCurrentFileIntact()
    {
        using var store = new SettingsStore(_directory);

        await store.SaveAsync(new());

        var original = await File.ReadAllTextAsync(
            store.FilePath,
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.SaveAsync(new()
            {
                Schema = 99
            })
        );
        Assert.Equal(
            original,
            await File.ReadAllTextAsync(store.FilePath, TestContext.Current.CancellationToken)
        );

        var import = Path.Combine(_directory, "bad.json");

        await File.WriteAllTextAsync(import, "{broken", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<JsonException>(() => SettingsStore.ReadAsync(import));
        Assert.Equal(
            original,
            await File.ReadAllTextAsync(store.FilePath, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task OversizedSettingsAreRejected()
    {
        Directory.CreateDirectory(_directory);

        var file = Path.Combine(_directory, "large.json");

        await File.WriteAllBytesAsync(
            file,
            new byte[SettingsStore.MaximumBytes + 1],
            TestContext.Current.CancellationToken
        );
        await Assert.ThrowsAsync<InvalidDataException>(() => SettingsStore.ReadAsync(file));
    }

    [Theory]
    [InlineData("#FFFFFFFF")]
    [InlineData("#00aabbcc")]
    [InlineData("#80123456")]
    public void CustomColorsAndTransparencyValidate(string color) =>
        new AppSettings { Foreground = color, Background = color }.Validate();
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
