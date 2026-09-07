using Microsoft.Win32;
using System.Windows.Threading;

namespace VolturaWeekNumber.Platform;

// Registry matching follows Voltura Air's TrayIconVisibilityPromoter.
internal sealed class TrayIconVisibilityPromoter : IDisposable
{
    private const string NotifyIconSettingsSubKey = @"Control Panel\NotifyIconSettings";
    private readonly DispatcherTimer _timer;
    private readonly Action _refreshIcon;
    private int _attempts;
    private bool _disposed;

    internal TrayIconVisibilityPromoter(Dispatcher dispatcher, Action refreshIcon)
    {
        _refreshIcon = refreshIcon;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += OnTick;
    }

    internal void Start()
    {
        if (_disposed) return;
        _attempts = 0;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs args)
    {
        _attempts++;
        if (TryPromoteCurrentProcess(out var changed))
        {
            _timer.Stop();
            if (changed) _refreshIcon();
        }
        else if (_attempts >= 20) _timer.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
    private static bool TryPromoteCurrentProcess(out bool changed)
    {
        changed = false;

        if (Environment.ProcessPath is not { Length: > 0 } executablePath)
        {
            return true;
        }

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(NotifyIconSettingsSubKey, writable: true);
            if (root is null)
            {
                return true;
            }

            return TryPromoteEntries(root, executablePath, out changed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return true;
        }
    }

    internal static bool TryPromoteEntries(RegistryKey root, string executablePath, out bool changed)
    {
        changed = false;
        var matchedEntry = false;
        var normalizedExecutablePath = NormalizePath(executablePath);

        foreach (var subKeyName in root.GetSubKeyNames())
        {
            using var entry = root.OpenSubKey(subKeyName, writable: true);
            var entryExecutablePath = entry?.GetValue("ExecutablePath") as string;
            if (!PathsEqual(normalizedExecutablePath, entryExecutablePath))
            {
                continue;
            }

            matchedEntry = true;
            if (!Equals(entry!.GetValue("IsPromoted"), 1))
            {
                entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                changed = true;
            }
        }

        return matchedEntry;
    }

    private static bool PathsEqual(string path, string? candidate)
    {
        return candidate is { Length: > 0 } &&
            string.Equals(path, NormalizePath(candidate), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
        catch (PathTooLongException)
        {
            return path;
        }
    }
}
