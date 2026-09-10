using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class ActivationShortcutTests
{
    [Fact]
    public void NativeCaptureHookUsesAnAvailableWindowsEntryPointAndDisposes()
    {
        using var hook = new KeyboardShortcutCapture((_, _) => false);

        Assert.False(hook.IsDisposed);

        hook.Dispose();

        Assert.True(hook.IsDisposed);
    }

    [Fact]
    public void SpecialKeyLabelsFollowTheApplicationLanguage()
    {
        Strings.Current.SetLanguage("en");

        Assert.Equal("Backspace", ShortcutDisplay.KeyName(0x08));

        Strings.Current.SetLanguage("sv");

        Assert.Equal("Backsteg", ShortcutDisplay.KeyName(0x08));
        Strings.Current.SetLanguage("en");
    }

    [Fact]
    public void RecorderNormalizesModifiersAndKeepsOneMainKey()
    {
        var state = new ShortcutCaptureState(null);

        state.KeyDown(0xA2);
        state.KeyDown(0xA3);
        state.KeyUp(0xA2);
        state.KeyDown(0x12);
        state.KeyDown(0x57);
        state.KeyUp(0xA3);
        state.KeyUp(0x12);

        Assert.Equal(new(false, true, true, false, 0x57), state.Candidate);
        Assert.Equal("Ctrl + Alt + W", ShortcutDisplay.Text(state.Candidate));
    }

    [Fact]
    public void WindowsModifierUsesTheDedicatedVectorLogoPart()
    {
        var part = Assert.Single(ShortcutDisplay.Parts(new(true, false, false, false, 0x57)),
            item => item.WindowsLogo);

        Assert.Equal("Windows", part.AccessibleText);
        Assert.Empty(part.Text);
    }

    [Fact]
    public void RecorderSupportsIncompleteClearAndResetStates()
    {
        var initial = new ActivationShortcut(true, false, false, false, 0x43);
        var state = new ShortcutCaptureState(initial);

        state.KeyDown(0x11);

        Assert.Null(state.Candidate);
        Assert.Equal("Ctrl", Assert.Single(state.Preview).Text);

        state.KeyDown(0x7B);
        Assert.False(state.Candidate!.IsValid);

        state.Clear();
        Assert.Null(state.Candidate);
        Assert.Empty(state.Preview);

        state.Reset(initial);
        Assert.Equal(initial, state.Candidate);
        Assert.Equal("Windows + C", ShortcutDisplay.Text(state.Candidate));
    }

    [Theory]
    [InlineData(0xBA)]
    [InlineData(0x6F)]
    [InlineData(0x25)]
    public void OemAndLocalizedVirtualKeysHaveDisplayLabels(int virtualKey) =>
        Assert.False(string.IsNullOrWhiteSpace(ShortcutDisplay.KeyName(virtualKey)));

    [Fact]
    public void RegistryUsesNoRepeatAndDispatchesStableActionIds()
    {
        var api = new FakeHotKeyApi();
        using var registry = new ActivationShortcutRegistry(42, api);
        var week = new ActivationShortcut(true, true, false, false, 0x57);
        var calendar = new ActivationShortcut(false, true, true, false, 0x43);
        var activations = new List<ActivationTarget>();

        registry.Activated += activations.Add;
        registry.Configure(week, calendar);

        Assert.Contains(api.Registered, call => call is { Id: 0x4100, Key: 0x57 }
            && (call.Modifiers & ActivationShortcutRegistry.NoRepeat) != 0);
        Assert.Contains(api.Registered, call => call is { Id: 0x4101, Key: 0x43 }
            && (call.Modifiers & ActivationShortcutRegistry.NoRepeat) != 0);
        Assert.True(registry.ProcessMessage(0x4100));
        Assert.True(registry.ProcessMessage(0x4101));
        Assert.Equal([ActivationTarget.WeekNumber, ActivationTarget.Calendar], activations);
    }

    [Fact]
    public void RegistryRejectsInternalAndExternalConflicts()
    {
        var api = new FakeHotKeyApi();
        using var registry = new ActivationShortcutRegistry(42, api);
        var calendar = new ActivationShortcut(false, true, true, false, 0x43);
        var external = new ActivationShortcut(false, true, false, false, 0x58);

        registry.Configure(null, calendar);
        api.Blocked.Add((0x4002, 0x58));

        Assert.False(registry.IsAvailable(ActivationTarget.WeekNumber, calendar));
        Assert.False(registry.IsAvailable(ActivationTarget.WeekNumber, external));
        Assert.True(registry.IsAvailable(
            ActivationTarget.WeekNumber,
            new(false, true, false, false, 0x57)
        ));
    }

    [Fact]
    public void FailedReplacementRestoresThePreviousWorkingShortcut()
    {
        var api = new FakeHotKeyApi();
        using var registry = new ActivationShortcutRegistry(42, api);
        var previous = new ActivationShortcut(false, true, false, false, 0x57);
        var replacement = new ActivationShortcut(false, true, true, false, 0x43);
        var activated = 0;

        registry.Activated += _ => activated++;
        registry.Configure(previous, null);
        api.FailActual.Add((0x4003, 0x43));

        Assert.False(registry.TryReplace(ActivationTarget.WeekNumber, replacement));
        Assert.True(registry.ProcessMessage(0x4100));
        Assert.Equal(1, activated);
        Assert.Equal(0x57u, api.Active[0x4100].Key);
    }

    [Fact]
    public void PairReplacementSupportsSwapsAndRollsBackBothActionsOnFailure()
    {
        var api = new FakeHotKeyApi();
        using var registry = new ActivationShortcutRegistry(42, api);
        var week = new ActivationShortcut(false, true, false, false, 0x57);
        var calendar = new ActivationShortcut(false, true, true, false, 0x43);

        registry.Configure(week, calendar);
        Assert.True(registry.TryReplaceAll(calendar, week));
        Assert.Equal(0x43u, api.Active[0x4100].Key);
        Assert.Equal(0x57u, api.Active[0x4101].Key);

        var unavailable = new ActivationShortcut(true, false, false, false, 0x58);

        api.FailActual.Add((0x4008, 0x58));
        Assert.False(registry.TryReplaceAll(week, unavailable));
        Assert.Equal(0x43u, api.Active[0x4100].Key);
        Assert.Equal(0x57u, api.Active[0x4101].Key);
    }

    [Fact]
    public void StartupConflictIsToleratedAndRetriedWithoutDiscardingTheShortcut()
    {
        var api = new FakeHotKeyApi();
        using var registry = new ActivationShortcutRegistry(42, api);
        var shortcut = new ActivationShortcut(false, true, false, false, 0x57);

        api.FailActual.Add((0x4002, 0x57));
        registry.Configure(shortcut, null);
        Assert.False(registry.ProcessMessage(0x4100));

        api.FailActual.Clear();
        registry.Configure(shortcut, null);
        Assert.True(registry.ProcessMessage(0x4100));
    }

    private sealed class FakeHotKeyApi : IHotKeyApi
    {
        internal readonly List<Registration> Registered = [];
        internal readonly Dictionary<int, Registration> Active = [];
        internal readonly HashSet<(uint Modifiers, uint Key)> Blocked = [];
        internal readonly HashSet<(uint Modifiers, uint Key)> FailActual = [];

        public bool Register(nint window, int id, uint modifiers, uint virtualKey)
        {
            var call = new Registration(id, modifiers, virtualKey);

            Registered.Add(call);

            if (
                Blocked.Contains((modifiers, virtualKey))
                || (id != 0x4102 && FailActual.Contains((modifiers, virtualKey)))
                || Active.ContainsKey(id)
            )
            {
                return false;
            }

            Active[id] = call;

            return true;
        }

        public bool Unregister(nint window, int id) => Active.Remove(id);
    }

    private sealed record Registration(int Id, uint Modifiers, uint Key);
}
