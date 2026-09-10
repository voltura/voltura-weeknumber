namespace VolturaWeekNumber.Features.Settings;

public sealed record ActivationShortcut(
    bool Windows,
    bool Control,
    bool Alt,
    bool Shift,
    int VirtualKey
)
{
    private const int F12 = 0x7B;
    private const int Tab = 0x09;

    public bool IsValid =>
        (Windows || Control || Alt || Shift)
        && VirtualKey is >= 1 and <= 0xFE
        && VirtualKey != F12
        && !IsModifier(VirtualKey)
        && !(Shift && !Windows && !Control && !Alt && VirtualKey == Tab);

    public void Validate()
    {
        if (!IsValid)
        {
            throw new InvalidDataException("Invalid activation shortcut.");
        }
    }

    internal static bool IsModifier(int virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12
        or 0x5B or 0x5C
        or 0xA0 or 0xA1
        or 0xA2 or 0xA3
        or 0xA4 or 0xA5;
}

public enum ActivationTarget
{
    WeekNumber,
    Calendar,
}

public sealed record ActivationShortcutChange(
    ActivationTarget Target,
    ActivationShortcut? Shortcut
);
