namespace ObjectTracker.Vision.Source;

public readonly record struct UsbCameraKey(int CameraIndex, string Api);

public readonly record struct UsbCaptureSettings(int Width, int Height, int TargetFps)
{
    public static UsbCaptureSettings Default { get; } = new(640, 480, 20);
}

public readonly record struct UsbCapturedFrame(
    string SourceId,
    long TimestampUtcMs,
    int Width,
    int Height,
    byte[] EncodedJpeg);

public readonly record struct UsbFrameSnapshot(
    string SourceId,
    long TimestampUtcMs,
    int Width,
    int Height,
    byte[] EncodedJpeg,
    long FrameVersion);

public interface IUsbCaptureBackend
{
    ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken);
}

public interface IUsbCaptureSession : IAsyncDisposable
{
    ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken);
}

public static class UsbCameraSourceId
{
    public static string Format(UsbCameraKey key) => $"usb:{key.CameraIndex}:{key.Api.ToUpperInvariant()}";
}

public sealed class UsbCameraOwnerManager : IAsyncDisposable
{
    private readonly IUsbCaptureBackend backend;
    private readonly SemaphoreSlim startupLock = new(1, 1);
    private readonly Lock sync = new();
    private readonly Dictionary<UsbCameraKey, UsbCameraOwner> owners = new();

    public UsbCameraOwnerManager(IUsbCaptureBackend backend)
    {
        this.backend = backend;
    }

    public async ValueTask<UsbCameraLease> AcquireAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
    {
        UsbCameraOwner owner;
        var shouldStart = false;

        lock (sync)
        {
            if (!owners.TryGetValue(key, out owner!))
            {
                owner = new UsbCameraOwner(key, settings, backend, startupLock);
                owners[key] = owner;
                shouldStart = true;
            }

            owner.AddLease();
        }

        if (shouldStart)
        {
            await owner.StartAsync(cancellationToken);
        }

        return new UsbCameraLease(owner);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        List<UsbCameraOwner> snapshot;
        lock (sync)
        {
            snapshot = owners.Values.ToList();
            owners.Clear();
        }

        foreach (var owner in snapshot)
        {
            await owner.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync(CancellationToken.None);
        startupLock.Dispose();
    }
}

public sealed class UsbCameraLease : IAsyncDisposable
{
    private readonly UsbCameraOwner owner;
    private bool disposed;

    internal UsbCameraLease(UsbCameraOwner owner)
    {
        this.owner = owner;
    }

    public UsbFrameSnapshot? LatestFrame => owner.LatestFrame;

    public Task<UsbFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return owner.WaitForNextFrameAsync(previousVersion, timeout, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            owner.ReleaseLease();
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class UsbCameraOwner
{
    private readonly UsbCameraKey key;
    private readonly UsbCaptureSettings settings;
    private readonly IUsbCaptureBackend backend;
    private readonly SemaphoreSlim startupLock;
    private readonly Lock sync = new();
    private TaskCompletionSource<object?> nextFrameAvailable = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? cts;
    private Task? readTask;
    private IUsbCaptureSession? session;
    private int leaseCount;
    private UsbFrameSnapshot? latestFrame;
    private long frameVersion;

    public UsbCameraOwner(UsbCameraKey key, UsbCaptureSettings settings, IUsbCaptureBackend backend, SemaphoreSlim startupLock)
    {
        this.key = key;
        this.settings = settings;
        this.backend = backend;
        this.startupLock = startupLock;
    }

    public UsbFrameSnapshot? LatestFrame
    {
        get
        {
            lock (sync)
            {
                return latestFrame;
            }
        }
    }

    public void AddLease() => leaseCount++;

    public void ReleaseLease() => leaseCount--;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await startupLock.WaitAsync(cancellationToken);
        try
        {
            session = await backend.OpenAsync(key, settings, cancellationToken);
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTask = Task.Run(() => ReadLoopAsync(cts.Token), cts.Token);
        }
        finally
        {
            startupLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cts?.Cancel();
        if (readTask is not null)
        {
            try
            {
                await readTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (session is not null)
        {
            await session.DisposeAsync();
            session = null;
        }

        cts?.Dispose();
        cts = null;
    }

    public async Task<UsbFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var current = LatestFrame;
        if (current is not null && current.Value.FrameVersion > previousVersion)
        {
            return current;
        }

        Task waitForFrame;
        lock (sync)
        {
            current = latestFrame;
            if (current is not null && current.Value.FrameVersion > previousVersion)
            {
                return current;
            }

            waitForFrame = nextFrameAvailable.Task;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var cancellationSignal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var cancellationRegistration = cancellationToken.UnsafeRegister(
            static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
            cancellationSignal);
        var completed = await Task.WhenAny(waitForFrame, Task.Delay(timeout), cancellationSignal.Task);
        if (completed != waitForFrame)
        {
            return null;
        }

        current = LatestFrame;
        return current is not null && current.Value.FrameVersion > previousVersion ? current : null;
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (session is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await session.ReadFrameAsync(cancellationToken);
            if (frame is null)
            {
                await Task.Delay(10, cancellationToken);
                continue;
            }

            var snapshot = new UsbFrameSnapshot(
                frame.Value.SourceId,
                frame.Value.TimestampUtcMs,
                frame.Value.Width,
                frame.Value.Height,
                frame.Value.EncodedJpeg,
                Interlocked.Increment(ref frameVersion));

            TaskCompletionSource<object?> completedSignal;
            lock (sync)
            {
                latestFrame = snapshot;
                completedSignal = nextFrameAvailable;
                nextFrameAvailable = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            completedSignal.TrySetResult(null);
        }
    }
}
