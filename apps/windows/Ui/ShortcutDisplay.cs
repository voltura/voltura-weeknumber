using System.Runtime.InteropServices;
using System.Windows.Input;
using VolturaWeekNumber.Features.Settings;

namespace VolturaWeekNumber.Ui;

public sealed record ShortcutKeyPart(
    string Text,
    bool Glyph = false,
    string? AccessibleText = null,
    bool Large = false,
    bool WindowsLogo = false
);

internal static partial class ShortcutDisplay
{
    internal static IReadOnlyList<ShortcutKeyPart> Parts(ActivationShortcut? shortcut)
    {
        if (shortcut is null)
        {
            return [];
        }

        return Parts(
            shortcut.Windows,
            shortcut.Control,
            shortcut.Alt,
            shortcut.Shift,
            shortcut.VirtualKey
        );
    }

    internal static IReadOnlyList<ShortcutKeyPart> Parts(
        bool windows,
        bool control,
        bool alt,
        bool shift,
        int virtualKey = 0
    )
    {

        var parts = new List<ShortcutKeyPart>(5);

        if (windows)
        {
            parts.Add(new(string.Empty, AccessibleText: "Windows", WindowsLogo: true));
        }

        if (control)
        {
            parts.Add(new("Ctrl"));
        }

        if (alt)
        {
            parts.Add(new("Alt"));
        }

        if (shift)
        {
            parts.Add(new("\uE752", true, "Shift", true));
        }

        if (virtualKey > 0)
        {
            parts.Add(new(KeyName(virtualKey)));
        }

        return parts;
    }

    internal static string Text(ActivationShortcut? shortcut) => Text(Parts(shortcut));

    internal static string Text(IReadOnlyList<ShortcutKeyPart> parts) => string.Join(
        " + ",
        parts.Select(part => part.AccessibleText ?? part.Text)
    );

    internal static string KeyName(int virtualKey)
    {
        if (virtualKey == 0x08)
        {
            return Strings.Current["ShortcutKeyBackspace"];
        }

        if (virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        var scanCode = MapVirtualKey((uint)virtualKey, 0);

        if (scanCode != 0)
        {
            Span<char> name = stackalloc char[64];
            var keyData = (int)(scanCode << 16);

            if (IsExtendedKey(virtualKey))
            {
                keyData |= 1 << 24;
            }

            var length = GetKeyNameText(keyData, name);

            if (length > 0)
            {
                return name[..length].ToString();
            }
        }

        var key = KeyInterop.KeyFromVirtualKey(virtualKey);

        return key == Key.None
            ? $"0x{virtualKey:X2}"
            : key.ToString();
    }

    private static bool IsExtendedKey(int virtualKey) => virtualKey is
        0x21 or 0x22 or 0x23 or 0x24
        or 0x25 or 0x26 or 0x27 or 0x28
        or 0x2D or 0x2E
        or 0x5B or 0x5C or 0x5D
        or 0x6F or 0x90;

    [LibraryImport("user32.dll", EntryPoint = "GetKeyNameTextW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetKeyNameText(
        int keyData,
        Span<char> text,
        int size = 64
    );

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);
}
