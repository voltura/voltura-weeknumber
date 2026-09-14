namespace VolturaWeekNumber.Features.Calendar;

internal sealed partial class ImportedCalendarStore
{
    private readonly object _lifetime = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _operations;
    private bool _stopping;
    private Task? _disposal;
    internal CancellationToken ShutdownToken => _shutdown.Token;
    internal bool IsStopping
    {
        get
        {
            lock (_lifetime)
            {
                return _stopping;
            }
        }
    }

    internal IDisposable BeginOperation()
    {
        lock (_lifetime)
        {
            if (_stopping)
            {
                throw new OperationCanceledException();
            }

            _operations++;

            return new Operation(this);
        }
    }

    private sealed class Operation(ImportedCalendarStore owner) : IDisposable
    {
        private ImportedCalendarStore? _owner = owner;
        public void Dispose()
        {
            var store = Interlocked.Exchange(ref _owner, null);

            if (store is null)
            {
                return;
            }

            lock (store._lifetime)
            {
                if (--store._operations == 0 && store._stopping)
                {
                    store._idle.TrySetResult();
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime)
        {
            if (_disposal is null)
            {
                _stopping = true;

                if (_operations == 0)
                {
                    _idle.TrySetResult();
                }

                _disposal = DisposeCoreAsync();
            }

            return new ValueTask(_disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        _shutdown.Cancel();
        await _idle.Task.ConfigureAwait(false);
        _gate.Dispose();
        _queryGate.Dispose();
        _shutdown.Dispose();
    }

    // Synchronous owners must not block a dispatcher needed by pending operations.
    public void Dispose() => _ = DisposeAsync().AsTask();
}
