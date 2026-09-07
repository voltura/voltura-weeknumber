using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.Json;

namespace VolturaWeekNumber.Platform;

internal static partial class WindowDpiDiagnostics
{
    internal static void Attach(Window window, Action<string> record)
    {
        HwndSource? source = null;
        window.SourceInitialized += SourceInitialized;
        window.Activated += Activated;
        window.IsVisibleChanged += VisibilityChanged;
        window.Closed += Closed;
        void Snapshot(string reason) => record("DPI " + reason + " " + JsonSerializer.Serialize(Capture(window)));
        void Activated(object? sender, EventArgs args) => Snapshot("activated");
        void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) => Snapshot("visibility");
        void SourceInitialized(object? sender, EventArgs args)
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook(Message);
            Snapshot("source-initialized");
        }
        nint Message(nint handle, int message, nint word, nint data, ref bool handled)
        {
            if (message is 0x007E or 0x02E0 or 0x001A or 0x0218)
            {
                Snapshot($"message-{message:X4}-wparam-{word}");
                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    if (source is { IsDisposed: false }) Snapshot($"settled-{message:X4}");
                }, DispatcherPriority.ContextIdle);
            }
            return 0;
        }
        void Closed(object? sender, EventArgs args)
        {
            source?.RemoveHook(Message);
            window.SourceInitialized -= SourceInitialized;
            window.Activated -= Activated;
            window.IsVisibleChanged -= VisibilityChanged;
            window.Closed -= Closed;
        }
    }

    internal static object Capture(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var dpi = VisualTreeHelper.GetDpi(window);
        var monitor = MonitorFromWindow(handle, 2);
        _ = GetDpiForMonitor(monitor, 0, out var monitorX, out var monitorY);
        _ = GetWindowRect(handle, out var rect);
        return new
        {
            handle = handle.ToInt64(), nativeDpi = GetDpiForWindow(handle),
            perMonitorV2 = AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(handle), new nint(-4)),
            monitor = monitor.ToInt64(), monitorDpiX = monitorX, monitorDpiY = monitorY,
            wpfDpiX = dpi.PixelsPerInchX, wpfDpiY = dpi.PixelsPerInchY,
            left = rect.Left, top = rect.Top, pixelWidth = rect.Right - rect.Left, pixelHeight = rect.Bottom - rect.Top,
            logicalWidth = window.ActualWidth, logicalHeight = window.ActualHeight,
            visible = window.IsVisible, state = window.WindowState.ToString()
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [LibraryImport("user32.dll")] private static partial uint GetDpiForWindow(nint window);
    [LibraryImport("user32.dll")] private static partial nint GetWindowDpiAwarenessContext(nint window);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool AreDpiAwarenessContextsEqual(nint first, nint second);
    [LibraryImport("user32.dll")] private static partial nint MonitorFromWindow(nint window, uint flags);
    [LibraryImport("shcore.dll")] private static partial int GetDpiForMonitor(nint monitor, int kind, out uint x, out uint y);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetWindowRect(nint window, out NativeRect rectangle);
}
