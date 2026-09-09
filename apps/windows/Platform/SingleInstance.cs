using System.Security.Cryptography;
using System.Text;

namespace VolturaWeekNumber.Platform;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _wait;
    public bool IsFirst { get; }
    public SingleInstance(string dataDirectory)
    {
        var identity = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(dataDirectory.ToUpperInvariant()))
        )[..24];

        _mutex = new Mutex(true, @"Local\VolturaWeekNumber-" + identity, out var created);
        IsFirst = created;
        _activation = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            @"Local\VolturaWeekNumber-Activate-" + identity
        );

        if (!created)
        {
            _activation.Set();
        }
    }

    public void Listen(Action activate) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(
            _activation,
            (_, _) => activate(),
            null,
            Timeout.Infinite,
            false
        );

    public void Dispose()
    {
        _wait?.Unregister(null);
        _activation.Dispose();

        if (IsFirst)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
