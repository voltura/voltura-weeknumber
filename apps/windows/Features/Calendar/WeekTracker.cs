namespace VolturaWeekNumber.Features.Calendar;

public sealed class WeekTracker
{
    private DateOnly? _lastStart;
    public bool Observe(WeekResult result, bool notify)
    {
        ArgumentNullException.ThrowIfNull(result);
        var changed = _lastStart is { } previous && previous != result.WeekStart;
        _lastStart = result.WeekStart;
        return notify && changed;
    }
}
