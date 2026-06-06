namespace ObjectTracker.Vision.Source;

public readonly record struct FileCameraSourceKey(string CameraId, string VideoPath);

public readonly record struct FileCameraSourcePlaybackSettings(bool LoopVideo);

public readonly record struct FileCameraSourceFrame(
    string SourceId,
    long TimestampUtcMs,
    int Width,
    int Height,
    byte[] EncodedJpeg);

public readonly record struct FileCameraSourceFrameSnapshot(
    string SourceId,
    long TimestampUtcMs,
    int Width,
    int Height,
    byte[] EncodedJpeg,
    long FrameVersion);

public interface IFileCameraSourcePlaybackBackend
{
    ValueTask<IFileCameraSourcePlaybackSession> OpenAsync(
        FileCameraSourceKey key,
        FileCameraSourcePlaybackSettings settings,
        CancellationToken cancellationToken);
}

public interface IFileCameraSourcePlaybackSession : IAsyncDisposable
{
    ValueTask<FileCameraSourceFrame?> ReadFrameAsync(CancellationToken cancellationToken);
}

public sealed class FileCameraSourceFeedManager : IAsyncDisposable
{
    private readonly IFileCameraSourcePlaybackBackend backend;
    private readonly Lock sync = new();
    private readonly Dictionary<FileCameraSourceKey, FileCameraSourceFeedOwner> owners = new();

    public FileCameraSourceFeedManager(IFileCameraSourcePlaybackBackend backend)
    {
        this.backend = backend;
    }

    public async ValueTask<FileCameraSourceFeedLease> AcquireAsync(
        FileCameraSourceKey key,
        FileCameraSourcePlaybackSettings settings,
        CancellationToken cancellationToken)
    {
        FileCameraSourceFeedOwner owner;
        var shouldStart = false;

        lock (sync)
        {
            if (!owners.TryGetValue(key, out owner!))
            {
                owner = new FileCameraSourceFeedOwner(key, settings, backend);
                owners[key] = owner;
                shouldStart = true;
            }

            owner.AddLease();
        }

        if (shouldStart)
        {
            await owner.StartAsync(cancellationToken);
        }

        return new FileCameraSourceFeedLease(owner);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        List<FileCameraSourceFeedOwner> snapshot;
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

    public FileCameraSourceRuntimeStatus GetStatus(FileCameraSourceKey key, long nowUtcMs)
    {
        lock (sync)
        {
            return owners.TryGetValue(key, out var owner)
                ? owner.GetStatus(nowUtcMs)
                : new FileCameraSourceRuntimeStatus(FileCameraSourceFeedState.Stopped, false, null, null, null, null, null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync(CancellationToken.None);
    }
}

public sealed class FileCameraSourceFeedLease : IAsyncDisposable
{
    private readonly FileCameraSourceFeedOwner owner;
    private bool disposed;

    internal FileCameraSourceFeedLease(FileCameraSourceFeedOwner owner)
    {
        this.owner = owner;
    }

    public FileCameraSourceFrameSnapshot? LatestFrame => owner.LatestFrame;

    public Task<FileCameraSourceFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken)
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

public enum FileCameraSourceFeedState
{
    Stopped,
    Starting,
    Running,
    Failed
}

public readonly record struct FileCameraSourceRuntimeStatus(
    FileCameraSourceFeedState State,
    bool IsStale,
    long? LatestFrameVersion,
    int? Width,
    int? Height,
    long? FrameAgeMs,
    string? FailureMessage);

internal sealed class FileCameraSourceFeedOwner
{
    private readonly FileCameraSourceKey key;
    private readonly FileCameraSourcePlaybackSettings settings;
    private readonly IFileCameraSourcePlaybackBackend backend;
    private readonly Lock sync = new();
    private TaskCompletionSource<object?> nextFrameAvailable = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? cts;
    private Task? readTask;
    private IFileCameraSourcePlaybackSession? session;
    private FileCameraSourceFrameSnapshot? latestFrame;
    private long frameVersion;
    private int leaseCount;
    private FileCameraSourceFeedState state = FileCameraSourceFeedState.Stopped;
    private string? failureMessage;

    public FileCameraSourceFeedOwner(FileCameraSourceKey key, FileCameraSourcePlaybackSettings settings, IFileCameraSourcePlaybackBackend backend)
    {
        this.key = key;
        this.settings = settings;
        this.backend = backend;
    }

    public FileCameraSourceFrameSnapshot? LatestFrame
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

    public FileCameraSourceRuntimeStatus GetStatus(long nowUtcMs)
    {
        lock (sync)
        {
            if (latestFrame is null)
            {
                return new FileCameraSourceRuntimeStatus(state, false, null, null, null, null, failureMessage);
            }

            var frameAgeMs = Math.Max(0, nowUtcMs - latestFrame.Value.TimestampUtcMs);
            return new FileCameraSourceRuntimeStatus(
                state,
                IsStale: frameAgeMs > 1000,
                latestFrame.Value.FrameVersion,
                latestFrame.Value.Width,
                latestFrame.Value.Height,
                frameAgeMs,
                failureMessage);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            state = FileCameraSourceFeedState.Starting;
        }

        try
        {
            session = await backend.OpenAsync(key, settings, cancellationToken);
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTask = Task.Run(() => ReadLoopAsync(cts.Token), cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (sync)
            {
                state = FileCameraSourceFeedState.Failed;
                failureMessage = ex.Message;
            }

            throw;
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
        lock (sync)
        {
            state = FileCameraSourceFeedState.Stopped;
        }
    }

    public async Task<FileCameraSourceFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken)
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

            var snapshot = new FileCameraSourceFrameSnapshot(
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
                state = FileCameraSourceFeedState.Running;
                failureMessage = null;
                completedSignal = nextFrameAvailable;
                nextFrameAvailable = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            completedSignal.TrySetResult(null);
        }
    }
}
