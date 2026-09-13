using System.Runtime.InteropServices;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace VolturaWeekNumber.Platform;

internal sealed partial class NativeTray
{
    internal event Action<Drawing.Rectangle>? CalendarToggleRequested;

    internal event Action? CalendarPointerIdle;

    internal Drawing.Rectangle CalendarAnchor(Drawing.Point? activation = null) =>
        TryCalendarAnchor(out var bounds)
            ? bounds
            : new Drawing.Rectangle(activation ?? Forms.Cursor.Position, new Drawing.Size(1, 1));

    private bool TryCalendarAnchor(out Drawing.Rectangle bounds)
    {
        var identity = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            Window = _source.Handle,
            Id = 1,
        };

        if (ShellNotifyIconGetRect(ref identity, out var rect) == 0)
        {
            bounds = Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);

            return true;
        }

        bounds = default;

        return false;
    }

    internal bool IsCalendarTrayClick => GetAsyncKeyState(1) < 0 && TryCalendarAnchor(out var bounds) && bounds.Contains(Forms.Cursor.Position);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int key);
}
