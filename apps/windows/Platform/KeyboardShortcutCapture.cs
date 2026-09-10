using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VolturaWeekNumber.Platform;

internal interface IShortcutCaptureHook : IDisposable
{
    bool Active { get; set; }
    bool IsDisposed { get; }
}

internal sealed class PassiveShortcutCaptureHook : IShortcutCaptureHook
{
    public bool Active { get; set; }
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

internal sealed partial class KeyboardShortcutCapture : IShortcutCaptureHook
{
    private const int LowLevelKeyboard = 13;
    private const int KeyDown = 0x0100;
    private const int KeyUp = 0x0101;
    private const int SystemKeyDown = 0x0104;
    private const int SystemKeyUp = 0x0105;
    private readonly Func<int, bool, bool> _keyChanged;
    private readonly HookProcedure _procedure;
    private nint _hook;

    internal KeyboardShortcutCapture(Func<int, bool, bool> keyChanged)
    {
        _keyChanged = keyChanged;
        _procedure = HookCallback;
        _hook = SetWindowsHookEx(LowLevelKeyboard, _procedure, GetModuleHandle(null), 0);

        if (_hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public bool Active { get; set; } = true;
    public bool IsDisposed => _hook == 0;

    private nint HookCallback(int code, nint message, nint data)
    {
        try
        {
            if (code < 0 || !Active)
            {
                return CallNextHookEx(_hook, code, message, data);
            }

            var value = message.ToInt32();
            var pressed = value is KeyDown or SystemKeyDown;

            if (!pressed && value is not KeyUp and not SystemKeyUp)
            {
                return CallNextHookEx(_hook, code, message, data);
            }

            var input = Marshal.PtrToStructure<LowLevelKeyboardInput>(data);

            return _keyChanged((int)input.VirtualKey, pressed)
                ? 1
                : CallNextHookEx(_hook, code, message, data);
        }
        catch
        {
            // Exceptions must never cross a native hook callback boundary.

            return CallNextHookEx(_hook, code, message, data);
        }
    }

    public void Dispose()
    {
        var hook = Interlocked.Exchange(ref _hook, 0);

        if (hook != 0)
        {
            _ = UnhookWindowsHookEx(hook);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelKeyboardInput
    {
        internal readonly uint VirtualKey;
        internal readonly uint ScanCode;
        internal readonly uint Flags;
        internal readonly uint Time;
        internal readonly nuint ExtraInfo;
    }

    private delegate nint HookProcedure(int code, nint message, nint data);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static partial nint SetWindowsHookEx(
        int hook,
        HookProcedure procedure,
        nint module,
        uint thread
    );

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string? moduleName);
}
