using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectTracker;

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
    byte[] EncodedJpeg,
    double? ActualFps = null);

public readonly record struct UsbFrameSnapshot(
    string SourceId,
    long TimestampUtcMs,
    int Width,
    int Height,
    byte[] EncodedJpeg,
    long FrameVersion);

public enum UsbCameraOwnerState
{
    Stopped,
    Starting,
    Running,
    Failed
}

public readonly record struct UsbCameraRuntimeStatus(
    UsbCameraOwnerState State,
    bool IsStale,
    long? LatestFrameVersion,
    int? Width,
    int? Height,
    long? FrameAgeMs,
    string? FailureMessage,
    double? ActualFps = null);

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

    public void AddConsumer(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
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
            owner.StartAsync(cancellationToken).Wait(cancellationToken);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (owner.LatestFrame is null && DateTimeOffset.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }
        }
    }

    public async ValueTask ReleaseAsync(UsbCameraKey key)
    {
        lock (sync)
        {
            if (!owners.TryGetValue(key, out var owner)) return;
            owner.ReleaseLease();
        }
    }

    public UsbFrameSnapshot? GetLatestFrame(UsbCameraKey key)
    {
        lock (sync)
        {
            return owners.TryGetValue(key, out var owner) ? owner.LatestFrame : null;
        }
    }

    public double? GetActualFps(UsbCameraKey key)
    {
        lock (sync)
        {
            return owners.TryGetValue(key, out var owner) ? owner.ActualFps : null;
        }
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

    public async Task RestartAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
    {
        UsbCameraOwner owner;
        lock (sync)
        {
            if (!owners.TryGetValue(key, out owner!))
            {
                owner = new UsbCameraOwner(key, settings, backend, startupLock);
                owners[key] = owner;
            }
        }

        await owner.StopAsync(cancellationToken);
        await owner.StartAsync(cancellationToken);
        await owner.WaitForNextFrameAsync(0, TimeSpan.FromMilliseconds(250), cancellationToken);
    }

    public UsbCameraRuntimeStatus GetStatus(UsbCameraKey key, long nowUtcMs)
    {
        lock (sync)
        {
            return owners.TryGetValue(key, out var owner)
                ? owner.GetStatus(nowUtcMs)
                : new UsbCameraRuntimeStatus(UsbCameraOwnerState.Stopped, false, null, null, null, null, null);
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
    private UsbCameraOwnerState state = UsbCameraOwnerState.Stopped;
    private string? failureMessage;
    private double? actualFps;
    private UsbCaptureSettings activeSettings;
    private bool fallbackAttempted;
    private int emptyFrameReads;

    public UsbCameraOwner(UsbCameraKey key, UsbCaptureSettings settings, IUsbCaptureBackend backend, SemaphoreSlim startupLock)
    {
        this.key = key;
        this.settings = settings;
        activeSettings = settings;
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

    public double? ActualFps => actualFps;

    public UsbCameraRuntimeStatus GetStatus(long nowUtcMs)
    {
        lock (sync)
        {
            if (latestFrame is null)
            {
                return new UsbCameraRuntimeStatus(state, false, null, null, null, null, failureMessage);
            }

            var frameAgeMs = Math.Max(0, nowUtcMs - latestFrame.Value.TimestampUtcMs);
            return new UsbCameraRuntimeStatus(
                state,
                IsStale: frameAgeMs > 1000,
                latestFrame.Value.FrameVersion,
                latestFrame.Value.Width,
                latestFrame.Value.Height,
                frameAgeMs,
                failureMessage,
                actualFps);
        }
    }

    public void AddLease() => leaseCount++;

    public int LeaseCount => leaseCount;

    public void ReleaseLease() => leaseCount--;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await startupLock.WaitAsync(cancellationToken);
        try
        {
            lock (sync)
            {
                state = UsbCameraOwnerState.Starting;
            }

            try
            {
                activeSettings = settings;
                fallbackAttempted = false;
                emptyFrameReads = 0;
                session = await backend.OpenAsync(key, activeSettings, cancellationToken);
                cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readTask = Task.Run(() => ReadLoopAsync(cts.Token), cts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (sync)
                {
                    state = UsbCameraOwnerState.Failed;
                    failureMessage = ex.Message;
                }

                throw;
            }
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
        lock (sync)
        {
            state = UsbCameraOwnerState.Stopped;
        }
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
                emptyFrameReads++;
                if (emptyFrameReads >= 10 && !fallbackAttempted && activeSettings != UsbCaptureSettings.Default)
                {
                    await ReopenWithStableBaselineAsync(cancellationToken);
                    continue;
                }

                await Task.Delay(10, cancellationToken);
                continue;
            }

            emptyFrameReads = 0;

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
                state = UsbCameraOwnerState.Running;
                failureMessage = null;
                actualFps = frame.Value.ActualFps;
                completedSignal = nextFrameAvailable;
                nextFrameAvailable = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            completedSignal.TrySetResult(null);
        }
    }

    private async Task ReopenWithStableBaselineAsync(CancellationToken cancellationToken)
    {
        fallbackAttempted = true;
        emptyFrameReads = 0;
        if (session is not null)
        {
            await session.DisposeAsync();
        }

        activeSettings = UsbCaptureSettings.Default;
        session = await backend.OpenAsync(key, activeSettings, cancellationToken);
    }
}
