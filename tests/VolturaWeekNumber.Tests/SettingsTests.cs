using System.Text.Json;
using VolturaWeekNumber.Features.Settings;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task RoundTripPreservesAllSettings()
    {
        using var store = new SettingsStore(_directory);
        var settings = new AppSettings { Language = "sv", Theme = "dark", Background = "#00123456", Logging = true };
        await store.SaveAsync(settings);
        Assert.Equal(settings, await SettingsStore.ReadAsync(store.FilePath));
        Assert.Empty(Directory.GetFiles(_directory, "*.pending"));
    }
    [Fact]
    public async Task BadImportAndInvalidSaveLeaveCurrentFileIntact()
    {
        using var store = new SettingsStore(_directory);
        await store.SaveAsync(new());
        var original = await File.ReadAllTextAsync(store.FilePath, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new() { Schema = 99 }));
        Assert.Equal(original, await File.ReadAllTextAsync(store.FilePath, TestContext.Current.CancellationToken));
        var import = Path.Combine(_directory, "bad.json");
        await File.WriteAllTextAsync(import, "{broken", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<JsonException>(() => SettingsStore.ReadAsync(import));
        Assert.Equal(original, await File.ReadAllTextAsync(store.FilePath, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task OversizedSettingsAreRejected()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "large.json");
        await File.WriteAllBytesAsync(file, new byte[SettingsStore.MaximumBytes + 1], TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => SettingsStore.ReadAsync(file));
    }
    [Theory]
    [InlineData("#FFFFFFFF")]
    [InlineData("#00aabbcc")]
    [InlineData("#80123456")]
    public void CustomColorsAndTransparencyValidate(string color) => new AppSettings { Foreground = color, Background = color }.Validate();
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
