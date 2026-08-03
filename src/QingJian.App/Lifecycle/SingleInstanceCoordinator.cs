namespace QingJian.App.Lifecycle;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly ManualResetEvent _ownershipResolved = new(false);
    private readonly ManualResetEvent _listenerReady = new(false);
    private readonly ManualResetEvent _stopEvent = new(false);
    private readonly Thread _worker;
    private Action? _activationRequested;
    private bool _disposed;

    private SingleInstanceCoordinator(Mutex mutex, EventWaitHandle activationEvent)
    {
        _mutex = mutex;
        _activationEvent = activationEvent;
        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "QingJian single-instance coordinator"
        };
        _worker.Start();
        _ownershipResolved.WaitOne();
    }

    public bool IsPrimary { get; private set; }

    public static SingleInstanceCoordinator Create(string applicationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);

        var objectPrefix = $@"Local\{applicationKey}";
        var activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            $"{objectPrefix}.Activate");
        var mutex = new Mutex(
            initiallyOwned: false,
            $"{objectPrefix}.Primary");

        return new SingleInstanceCoordinator(mutex, activationEvent);
    }

    public bool NotifyPrimary()
    {
        if (_disposed || IsPrimary)
        {
            return false;
        }

        try
        {
            return _activationEvent.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void StartListening(Action activationRequested)
    {
        ArgumentNullException.ThrowIfNull(activationRequested);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        }

        if (Interlocked.CompareExchange(ref _activationRequested, activationRequested, null) is not null)
        {
            throw new InvalidOperationException("The activation listener has already started.");
        }

        _listenerReady.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopEvent.Set();
        _worker.Join();
        _stopEvent.Dispose();
        _listenerReady.Dispose();
        _ownershipResolved.Dispose();
        _activationEvent.Dispose();
        _mutex.Dispose();
    }

    private void Run()
    {
        var ownsMutex = false;
        try
        {
            ownsMutex = _mutex.WaitOne(0);
            IsPrimary = ownsMutex;
            _ownershipResolved.Set();
            if (!ownsMutex || WaitHandle.WaitAny(new WaitHandle[] { _listenerReady, _stopEvent }) != 0)
            {
                return;
            }

            var activationHandles = new WaitHandle[] { _activationEvent, _stopEvent };
            while (WaitHandle.WaitAny(activationHandles) == 0)
            {
                try
                {
                    _activationRequested?.Invoke();
                }
                catch
                {
                    // Activation is best-effort and must not terminate the listener.
                }
            }
        }
        finally
        {
            if (ownsMutex)
            {
                _mutex.ReleaseMutex();
            }
        }
    }
}
