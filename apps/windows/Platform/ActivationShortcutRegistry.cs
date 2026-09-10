using System.Runtime.InteropServices;
using VolturaWeekNumber.Features.Settings;

namespace VolturaWeekNumber.Platform;

internal interface IHotKeyApi
{
    bool Register(nint window, int id, uint modifiers, uint virtualKey);
    bool Unregister(nint window, int id);
}

internal sealed partial class Win32HotKeyApi : IHotKeyApi
{
    public bool Register(nint window, int id, uint modifiers, uint virtualKey) =>
        RegisterHotKey(window, id, modifiers, virtualKey);

    public bool Unregister(nint window, int id) => UnregisterHotKey(window, id);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(
        nint window,
        int id,
        uint modifiers,
        uint virtualKey
    );

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint window, int id);
}

internal sealed class InMemoryHotKeyApi : IHotKeyApi
{
    private readonly HashSet<int> _registered = [];

    public bool Register(nint window, int id, uint modifiers, uint virtualKey) =>
        _registered.Add(id);

    public bool Unregister(nint window, int id) => _registered.Remove(id);
}

internal sealed class ActivationShortcutRegistry(
    nint window,
    IHotKeyApi hotKeys
) : IDisposable
{
    internal const int HotKeyMessage = 0x0312;
    internal const uint NoRepeat = 0x4000;
    private const int WeekNumberId = 0x4100;
    private const int CalendarId = 0x4101;
    private const int ProbeId = 0x4102;
    private readonly Dictionary<ActivationTarget, Registration> _registrations = new()
    {
        [ActivationTarget.WeekNumber] = new(),
        [ActivationTarget.Calendar] = new(),
    };
    private bool _disposed;

    internal event Action<ActivationTarget>? Activated;

    internal void Configure(ActivationShortcut? weekNumber, ActivationShortcut? calendar)
    {
        if (
            _registrations[ActivationTarget.WeekNumber].Shortcut == weekNumber
            && _registrations[ActivationTarget.Calendar].Shortcut == calendar
        )
        {
            RetryUnregistered(ActivationTarget.WeekNumber);
            RetryUnregistered(ActivationTarget.Calendar);

            return;
        }

        foreach (var (target, registration) in _registrations)
        {
            Unregister(target, registration);
        }

        _registrations[ActivationTarget.WeekNumber] = new(weekNumber);
        _registrations[ActivationTarget.Calendar] = new(calendar);
        RetryUnregistered(ActivationTarget.WeekNumber);
        RetryUnregistered(ActivationTarget.Calendar);
    }

    internal bool IsAvailable(ActivationTarget target, ActivationShortcut? shortcut)
    {
        if (shortcut is null)
        {
            return true;
        }

        if (!shortcut.IsValid || Other(target).Shortcut == shortcut)
        {
            return false;
        }

        var current = _registrations[target];

        if (current.Shortcut == shortcut && current.Registered)
        {
            return true;
        }

        if (!hotKeys.Register(window, ProbeId, Modifiers(shortcut), (uint)shortcut.VirtualKey))
        {
            return false;
        }

        _ = hotKeys.Unregister(window, ProbeId);

        return true;
    }

    internal bool TryReplace(ActivationTarget target, ActivationShortcut? shortcut)
    {
        var current = _registrations[target];

        if (current.Shortcut == shortcut)
        {
            if (shortcut is null || current.Registered)
            {
                return true;
            }

            var registered = Register(target, shortcut);

            _registrations[target] = current with { Registered = registered };

            return registered;
        }

        if (!IsAvailable(target, shortcut))
        {
            return false;
        }

        Unregister(target, current);

        if (shortcut is not null && !Register(target, shortcut))
        {
            var restored = current.Shortcut is not null && Register(target, current.Shortcut);

            _registrations[target] = current with { Registered = restored };

            return false;
        }

        _registrations[target] = new(shortcut, shortcut is not null);

        return true;
    }

    internal bool TryReplaceAll(
        ActivationShortcut? weekNumber,
        ActivationShortcut? calendar
    )
    {
        if (
            weekNumber is { IsValid: false }
            || calendar is { IsValid: false }
            || (weekNumber is not null && weekNumber == calendar)
        )
        {
            return false;
        }

        var previousWeek = _registrations[ActivationTarget.WeekNumber];
        var previousCalendar = _registrations[ActivationTarget.Calendar];

        if (previousWeek.Shortcut == weekNumber && previousCalendar.Shortcut == calendar)
        {
            return true;
        }

        Unregister(ActivationTarget.WeekNumber, previousWeek);
        Unregister(ActivationTarget.Calendar, previousCalendar);

        var weekRegistered = weekNumber is not null
            && Register(ActivationTarget.WeekNumber, weekNumber);
        var calendarRegistered = calendar is not null
            && Register(ActivationTarget.Calendar, calendar);
        var success = (weekNumber is null || weekRegistered)
            && (calendar is null || calendarRegistered);

        if (success)
        {
            _registrations[ActivationTarget.WeekNumber] = new(weekNumber, weekRegistered);
            _registrations[ActivationTarget.Calendar] = new(calendar, calendarRegistered);

            return true;
        }

        if (weekRegistered)
        {
            _ = hotKeys.Unregister(window, WeekNumberId);
        }

        if (calendarRegistered)
        {
            _ = hotKeys.Unregister(window, CalendarId);
        }

        var restoredWeek = previousWeek.Shortcut is not null
            && Register(ActivationTarget.WeekNumber, previousWeek.Shortcut);
        var restoredCalendar = previousCalendar.Shortcut is not null
            && Register(ActivationTarget.Calendar, previousCalendar.Shortcut);

        _registrations[ActivationTarget.WeekNumber] = previousWeek with
        {
            Registered = restoredWeek,
        };

        _registrations[ActivationTarget.Calendar] = previousCalendar with
        {
            Registered = restoredCalendar,
        };

        return false;
    }

    internal bool ProcessMessage(nint hotKeyId)
    {
        var target = hotKeyId.ToInt64() switch
        {
            WeekNumberId => ActivationTarget.WeekNumber,
            CalendarId => ActivationTarget.Calendar,
            _ => (ActivationTarget?)null,
        };

        if (target is not { } value || !_registrations[value].Registered)
        {
            return false;
        }

        Activated?.Invoke(value);

        return true;
    }

    private void RetryUnregistered(ActivationTarget target)
    {
        var current = _registrations[target];

        if (current is { Shortcut: { } shortcut, Registered: false })
        {
            _registrations[target] = current with { Registered = Register(target, shortcut) };
        }
    }

    private Registration Other(ActivationTarget target) => _registrations[
        target == ActivationTarget.WeekNumber
            ? ActivationTarget.Calendar
            : ActivationTarget.WeekNumber
    ];

    private bool Register(ActivationTarget target, ActivationShortcut shortcut) =>
        hotKeys.Register(
            window,
            Id(target),
            Modifiers(shortcut),
            (uint)shortcut.VirtualKey
        );

    private void Unregister(ActivationTarget target, Registration registration)
    {
        if (registration.Registered)
        {
            _ = hotKeys.Unregister(window, Id(target));
        }
    }

    private static int Id(ActivationTarget target) => target == ActivationTarget.WeekNumber
        ? WeekNumberId
        : CalendarId;

    private static uint Modifiers(ActivationShortcut shortcut) =>
        NoRepeat
        | (shortcut.Alt
            ? 0x0001u
            : 0u)
        | (shortcut.Control
            ? 0x0002u
            : 0u)
        | (shortcut.Shift
            ? 0x0004u
            : 0u)
        | (shortcut.Windows
            ? 0x0008u
            : 0u);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var (target, registration) in _registrations)
        {
            Unregister(target, registration);
        }
    }

    private sealed record Registration(
        ActivationShortcut? Shortcut = null,
        bool Registered = false
    );
}
