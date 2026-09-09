using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;
using VolturaWeekNumber.Features.Icon;
using Forms = System.Windows.Forms;

namespace VolturaWeekNumber.Platform;

internal sealed partial class NativeTray : IDisposable
{
    private const int Callback = 0x8001;
    private readonly HwndSource _source;
    private readonly uint _taskbarCreated;
    private readonly TrayIconVisibilityPromoter? _visibilityPromoter;
    private readonly Forms.ContextMenuStrip _menu = new();
    private SafeIconHandle? _icon;
    private bool _added;
    private bool _disposed;
    private string _tooltip = "Voltura WeekNumber";
    private (int? Week, int Size, IconAppearance Appearance)? _rendered;
    private (string Language, bool Dark)? _menuAppearance;
    public event Action? OpenRequested;
    public event Action? PreferencesRequested;
    public event Action? ExitRequested;
    public event Action? DisplayChanged;
    public event Action? NotificationClicked;
    internal bool IsPerMonitorV2 =>
        AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(_source.Handle), new nint(-4));
    internal uint CurrentDpi => GetDpiForWindow(_source.Handle);
    internal int RenderCount { get; private set; }
    public NativeTray(bool promoteVisibility = true)
    {
        _source = new HwndSource(
            new HwndSourceParameters("VolturaWeekNumber.Tray")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
            }
        );
        _source.AddHook(WndProc);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");

        if (promoteVisibility)
        {
            _visibilityPromoter = new TrayIconVisibilityPromoter(
                _source.Dispatcher,
                RefreshVisibility
            );
        }

        RebuildMenu();
    }

    public void RebuildMenu()
    {
        var text = Ui.Strings.Current;
        var dark = Ui.ThemeManager.IsTaskbarDark();
        var appearance = (text.Culture.Name, dark);

        if (_menuAppearance == appearance)
        {
            return;
        }

        foreach (Forms.ToolStripItem item in _menu.Items.Cast<Forms.ToolStripItem>().ToArray())
        {
            item.Dispose();
        }

        _menu.Items.Clear();
        _menu.Items.Add(text["WeekTab"], null, (_, _) => OpenRequested?.Invoke());
        _menu.Items.Add(text["Preferences"], null, (_, _) => PreferencesRequested?.Invoke());
        _menu.Items.Add(new Forms.ToolStripSeparator());

        var exit = text.Culture.TwoLetterISOLanguageName == "en"
            ? text["Exit"]
            : $"{text["Exit"]} / Exit";

        _menu.Items.Add(exit, null, (_, _) => ExitRequested?.Invoke());

        var renderer = new TrayMenuRenderer(dark);

        _menu.BackColor = renderer.Surface;
        _menu.ForeColor = renderer.Text;
        _menu.Renderer = renderer;
        _menu.ShowImageMargin = false;

        foreach (Forms.ToolStripItem item in _menu.Items)
        {
            item.BackColor = renderer.Surface;
            item.ForeColor = renderer.Text;
        }

        _menuAppearance = appearance;
    }

    public void Update(int? week, IconAppearance appearance, string tooltip)
    {
        if (_disposed)
        {
            return;
        }

        var size = IconSize();
        var key = (week, size, appearance);

        if (_rendered != key)
        {
            RenderCount++;

            var bytes = CalendarIconRenderer.EncodeIco(week, appearance, new[] { size });
            // .ico directory entry points to an independently encoded PNG image.
            var pngOffset = BitConverter.ToInt32(bytes, 18);
            var png = bytes.AsSpan(pngOffset).ToArray();
            var handle = CreateIconFromResourceEx(
                png,
                (uint)png.Length,
                true,
                0x30000,
                size,
                size,
                0
            );

            if (handle == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var replacement = new SafeIconHandle(handle);
            var old = _icon;

            _icon = replacement;
            _rendered = key;

            try
            {
                Publish(tooltip, true);
            }
            finally
            {
                old?.Dispose();
            }
        }
        else if (_tooltip != tooltip || !_added)
        {
            Publish(tooltip, false);
        }
    }

    public void Notify(string title, string body, bool silent)
    {
        var data = Data();

        data.Flags = 0x10;
        data.InfoTitle = Limit(title, 63);
        data.Info = Limit(body, 255);
        data.InfoFlags = 0x80u | (silent
            ? 0x10u
            : 0u);
        _ = ShellNotifyIcon(1, ref data);
    }

    private void Publish(string tooltip, bool imageChanged, bool promoteVisibility = true)
    {
        _tooltip = Limit(tooltip, 127);

        var data = Data();

        data.Flags = 1 | 4 | 0x80u | (imageChanged || !_added
            ? 2u
            : 0u);

        if (!ShellNotifyIcon(_added
            ? 1u
            : 0u, ref data))
        {
            _added = false;

            return; // Explorer may be unavailable; TaskbarCreated restores it.
        }

        if (!_added)
        {
            data.TimeoutOrVersion = 4;
            _ = ShellNotifyIcon(4, ref data);

            if (promoteVisibility)
            {
                _visibilityPromoter?.Start();
            }
        }

        _added = true;
    }

    private void RefreshVisibility()
    {
        if (_disposed || !_added)
        {
            return;
        }

        var data = Data();

        _ = ShellNotifyIcon(2, ref data);
        _added = false;
        Publish(_tooltip, true, promoteVisibility: false);
    }

    private NotifyIconData Data() =>
        new()
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = _source.Handle,
            Id = 1,
            CallbackMessage = Callback,
            Icon = _icon?.DangerousGetHandle() ?? 0,
            Tip = _tooltip,
            Info = string.Empty,
            InfoTitle = string.Empty,
        };

    private int IconSize()
    {
        var identity = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            Window = _source.Handle,
            Id = 1,
        };
        var dpi = GetDpiForWindow(_source.Handle);

        if (ShellNotifyIconGetRect(ref identity, out var rect) == 0)
        {
            var monitor = MonitorFromRect(in rect, 2);

            if (GetDpiForMonitor(monitor, 0, out var x, out _) == 0)
            {
                dpi = x;
            }
        }

        var pixels = Math.Max(16, (int)Math.Round(16 * dpi / 96d));

        return CalendarIconRenderer.Sizes.FirstOrDefault(size => size >= pixels, 256);
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == _taskbarCreated)
        {
            _added = false;
            _rendered = null;
            DisplayChanged?.Invoke();
        }
        else if (message is 0x7E or 0x2E0 or 0x1A)
        {
            DisplayChanged?.Invoke();
        }
        else if (message == Callback)
        {
            var code = (int)(lParam.ToInt64() & 0xffff);

            if (code is 0x203 or 0x401)
            {
                OpenRequested?.Invoke();
            }
            else if (code == 0x405)
            {
                NotificationClicked?.Invoke();
            }
            else if (code == 0x7B)
            {
                RebuildMenu();
                _ = SetForegroundWindow(_source.Handle);

                var packed = wParam.ToInt64();

                _menu.Show(
                    new System.Drawing.Point(
                        (short)(packed & 0xffff),
                        (short)((packed >> 16) & 0xffff)
                    )
                );
            }
        }

        return 0;
    }

    private static string Limit(string value, int length) =>
        value.Length <= length
            ? value
            : value[..(length - 1)] + "…";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _visibilityPromoter?.Dispose();

        var data = Data();

        _ = ShellNotifyIcon(2, ref data);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _menu.Dispose();
        _icon?.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;
        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;
        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public Guid Guid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Fixed inline UTF-16 fields need runtime marshalling for NOTIFYICONDATAW.
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
    private static partial int ShellNotifyIconGetRect(
        ref NotifyIconIdentifier id,
        out NativeRect rectangle
    );

    [LibraryImport(
        "user32.dll",
        EntryPoint = "RegisterWindowMessageW",
        StringMarshalling = StringMarshalling.Utf16
    )]
    private static partial uint RegisterWindowMessage(string message);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowDpiAwarenessContext(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AreDpiAwarenessContextsEqual(nint first, nint second);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromRect(in NativeRect rectangle, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int kind, out uint x, out uint y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint CreateIconFromResourceEx(
        byte[] data,
        uint size,
        [MarshalAs(UnmanagedType.Bool)] bool icon,
        uint version,
        int width,
        int height,
        uint flags
    );
}

internal sealed partial class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeIconHandle(nint handle)
        : base(true) => SetHandle(handle);

    protected override bool ReleaseHandle() => DestroyIcon(handle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);
}
