using ObjectTracker.Vision.Source;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class UsbCameraOwnerManagerTests
{
    [Fact]
    public async Task AcquireAsync_SameCameraSourceTwice_UsesOnePhysicalCaptureOwner()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await using var firstLease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);

        var firstFrame = await firstLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await secondLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, backend.GetOpenCount(key));
        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);
        Assert.Equal(firstFrame.Value.FrameVersion, secondFrame.Value.FrameVersion);
    }

    [Fact]
    public async Task AcquireAsync_DifferentCameraSources_StartsIndependentPhysicalOwners()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var firstKey = new UsbCameraKey(0, "ANY");
        var secondKey = new UsbCameraKey(1, "ANY");

        await using var firstLease = await manager.AcquireAsync(firstKey, UsbCaptureSettings.Default, CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(secondKey, UsbCaptureSettings.Default, CancellationToken.None);

        var firstFrame = await firstLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await secondLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, backend.GetOpenCount(firstKey));
        Assert.Equal(1, backend.GetOpenCount(secondKey));
        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);
        Assert.Equal("usb:0:ANY", firstFrame.Value.SourceId);
        Assert.Equal("usb:1:ANY", secondFrame.Value.SourceId);
    }

    [Fact]
    public async Task WaitForNextFrameAsync_ReturnsIncreasingFrameVersions()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 2, frameDelay: TimeSpan.FromMilliseconds(25));
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        await using var lease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);

        var firstFrame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await lease.WaitForNextFrameAsync(firstFrame?.FrameVersion ?? 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);
        Assert.True(secondFrame.Value.FrameVersion > firstFrame.Value.FrameVersion);
        Assert.Equal(2, secondFrame.Value.Width);
        Assert.Equal(2, secondFrame.Value.Height);
        Assert.NotEmpty(secondFrame.Value.EncodedJpeg);
    }

    [Fact]
    public async Task WaitForNextFrameAsync_WhenCallerCancels_ReturnsNoFrameWithoutThrowing()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 0);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");
        await using var lease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();
        var frame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), cts.Token);

        Assert.Null(frame);
    }

    [Fact]
    public async Task StopAllAsync_DisposesActivePhysicalOwners()
    {
        var backend = new FakeUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var firstKey = new UsbCameraKey(0, "ANY");
        var secondKey = new UsbCameraKey(1, "ANY");

        await using var firstLease = await manager.AcquireAsync(firstKey, UsbCaptureSettings.Default, CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(secondKey, UsbCaptureSettings.Default, CancellationToken.None);

        await manager.StopAllAsync(CancellationToken.None);

        Assert.Equal(1, backend.GetDisposeCount(firstKey));
        Assert.Equal(1, backend.GetDisposeCount(secondKey));
    }

    [Fact]
    public async Task AcquireAsync_ConcurrentCameraSourceStartup_SerializesPhysicalOpen()
    {
        var backend = new FakeUsbCaptureBackend(openDelay: TimeSpan.FromMilliseconds(25));
        await using var manager = new UsbCameraOwnerManager(backend);

        await Task.WhenAll(
            manager.AcquireAsync(new UsbCameraKey(0, "ANY"), UsbCaptureSettings.Default, CancellationToken.None).AsTask(),
            manager.AcquireAsync(new UsbCameraKey(1, "ANY"), UsbCaptureSettings.Default, CancellationToken.None).AsTask());

        Assert.Equal(1, backend.MaxConcurrentOpens);
    }

    private sealed class FakeUsbCaptureBackend(TimeSpan? openDelay = null, int framesPerSession = 1, TimeSpan? frameDelay = null) : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> openCounts = new();
        private readonly Dictionary<UsbCameraKey, int> disposeCounts = new();
        private int activeOpens;

        public int GetOpenCount(UsbCameraKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public int GetDisposeCount(UsbCameraKey key) => disposeCounts.TryGetValue(key, out var count) ? count : 0;

        public int MaxConcurrentOpens { get; private set; }

        public async ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            activeOpens++;
            MaxConcurrentOpens = Math.Max(MaxConcurrentOpens, activeOpens);
            if (openDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            openCounts[key] = GetOpenCount(key) + 1;
            activeOpens--;
            return new FakeUsbCaptureSession(key, framesPerSession, frameDelay, () => disposeCounts[key] = GetDisposeCount(key) + 1);
        }
    }

    private sealed class FakeUsbCaptureSession(UsbCameraKey key, int framesPerSession, TimeSpan? frameDelay, Action onDispose) : IUsbCaptureSession
    {
        private long timestampUtcMs = 1;
        private int returnedFrames;

        public async ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (returnedFrames >= framesPerSession)
            {
                return null;
            }

            if (returnedFrames > 0 && frameDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            returnedFrames++;
            return new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                timestampUtcMs++,
                Width: 2,
                Height: 2,
                EncodedJpeg: [1, 2, 3]);
        }

        public ValueTask DisposeAsync()
        {
            onDispose();
            return ValueTask.CompletedTask;
        }
    }
}
