using ObjectTracker.Vision.Source;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class BackgroundEstimationEngineUsbSourceTests
{
    [Fact]
    public async Task ProcessUsbCameraSourceAsync_WithExistingTileLease_UsesSharedOwnerAndFreshFrameVersions()
    {
        var key = new UsbCameraKey(0, "ANY");
        var backend = new CountingUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        await using var tileLease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        var engine = new BackgroundEstimationEngine();
        var processedFrames = 0;

        var result = await engine.ProcessUsbCameraSourceAsync(
            manager,
            key,
            UsbCaptureSettings.Default,
            sourceLabel: "USB Camera 0",
            sampleCount: 5,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onDebugFrames: _ =>
            {
                processedFrames++;
                return Task.CompletedTask;
            },
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldPublishDebugFrames: () => true,
            shouldStopEarly: () => processedFrames >= 2,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, backend.GetOpenCount(key));
        Assert.True(backend.LastReturnedFrameVersion >= 7);
        Assert.True(processedFrames >= 2);
    }

    [Fact]
    public async Task ProcessUsbCameraSourceAsync_WhenTileLeaseEnds_ContinuesProcessingFromSharedOwner()
    {
        var key = new UsbCameraKey(0, "ANY");
        var backend = new CountingUsbCaptureBackend(frameDelay: TimeSpan.FromMilliseconds(5));
        await using var manager = new UsbCameraOwnerManager(backend);
        var tileLease = await manager.AcquireAsync(key, UsbCaptureSettings.Default, CancellationToken.None);
        var engine = new BackgroundEstimationEngine();
        var processedFrames = 0;

        var processingTask = engine.ProcessUsbCameraSourceAsync(
            manager,
            key,
            UsbCaptureSettings.Default,
            sourceLabel: "USB Camera 0",
            sampleCount: 5,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onDebugFrames: _ =>
            {
                Interlocked.Increment(ref processedFrames);
                return Task.CompletedTask;
            },
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldPublishDebugFrames: () => true,
            shouldStopEarly: () => Volatile.Read(ref processedFrames) >= 3,
            CancellationToken.None);

        while (Volatile.Read(ref processedFrames) == 0)
        {
            await Task.Delay(5);
        }

        await tileLease.DisposeAsync();
        var result = await processingTask;

        Assert.True(result.Success);
        Assert.Equal(1, backend.GetOpenCount(key));
        Assert.True(processedFrames >= 3);
    }

    private sealed class CountingUsbCaptureBackend(TimeSpan? frameDelay = null) : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> openCounts = new();

        public int GetOpenCount(UsbCameraKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public long LastReturnedFrameVersion { get; private set; }

        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            openCounts[key] = GetOpenCount(key) + 1;
            return ValueTask.FromResult<IUsbCaptureSession>(new CountingUsbCaptureSession(key, frameDelay, version => LastReturnedFrameVersion = version));
        }
    }

    private sealed class CountingUsbCaptureSession(UsbCameraKey key, TimeSpan? frameDelay, Action<long> onFrameReturned) : IUsbCaptureSession
    {
        private long frameVersion;

        public async ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (frameDelay is not null)
            {
                await Task.Delay(frameDelay.Value, cancellationToken);
            }

            frameVersion++;
            onFrameReturned(frameVersion);
            return new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                frameVersion,
                Width: 32,
                Height: 24,
                EncodedJpeg: CreateFrameJpeg(frameVersion));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static byte[] CreateFrameJpeg(long frameVersion)
        {
            using var frame = new Mat(24, 32, MatType.CV_8UC3, new Scalar(frameVersion % 255, 20, 40));
            Cv2.Rectangle(frame, new Rect((int)(frameVersion % 12), 4, 8, 8), new Scalar(240, 240, 240), -1);
            Cv2.ImEncode(".jpg", frame, out var jpeg);
            return jpeg;
        }
    }
}
