using System.Runtime.InteropServices;

namespace VolturaWeekNumber.Platform;

internal interface IClipboardWriter
{
    bool TryWrite(string text);
}

internal sealed class ClipboardWriter : IClipboardWriter
{
    public bool TryWrite(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            System.Windows.Clipboard.SetDataObject(text, true);

            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }
}
