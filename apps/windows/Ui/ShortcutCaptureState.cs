using VolturaWeekNumber.Features.Settings;

namespace VolturaWeekNumber.Ui;

internal sealed class ShortcutCaptureState(ActivationShortcut? initial)
{
    private readonly HashSet<int> _pressedModifiers = [];
    private int _virtualKey;

    internal ActivationShortcut? Candidate { get; private set; } = initial;
    internal IReadOnlyList<ShortcutKeyPart> Preview { get; private set; } =
        ShortcutDisplay.Parts(initial);

    internal void KeyDown(int virtualKey)
    {
        if (ActivationShortcut.IsModifier(virtualKey))
        {
            if (_pressedModifiers.Count == 0)
            {
                _virtualKey = 0;
                Candidate = null;
            }

            _pressedModifiers.Add(virtualKey);
        }
        else
        {
            _virtualKey = virtualKey;
            Candidate = new(Windows, Control, Alt, Shift, virtualKey);
        }

        RefreshPreview();
    }

    internal void KeyUp(int virtualKey)
    {
        if (!ActivationShortcut.IsModifier(virtualKey))
        {
            return;
        }

        _pressedModifiers.Remove(virtualKey);

        if (_virtualKey == 0)
        {
            RefreshPreview();
        }
    }

    internal void Clear()
    {
        _pressedModifiers.Clear();
        _virtualKey = 0;
        Candidate = null;
        Preview = [];
    }

    internal void Reset(ActivationShortcut? shortcut)
    {
        Clear();
        Candidate = shortcut;
        Preview = ShortcutDisplay.Parts(shortcut);
    }

    internal void ReleaseModifiers()
    {
        _pressedModifiers.Clear();

        if (_virtualKey == 0)
        {
            RefreshPreview();
        }
    }

    internal bool AltDown => Alt;
    internal bool HasNonShiftModifier => Windows || Control || Alt;
    internal bool ShiftDown => Shift;

    private void RefreshPreview() => Preview = ShortcutDisplay.Parts(
        Windows,
        Control,
        Alt,
        Shift,
        _virtualKey
    );

    private bool Windows => ContainsAny(0x5B, 0x5C);
    private bool Control => ContainsAny(0x11, 0xA2, 0xA3);
    private bool Alt => ContainsAny(0x12, 0xA4, 0xA5);
    private bool Shift => ContainsAny(0x10, 0xA0, 0xA1);

    private bool ContainsAny(params int[] keys) => keys.Any(_pressedModifiers.Contains);
}
