using ObjectTracker;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbVideoSourceTests
{
    [Fact]
    public async Task AddConsumer_StartsOwner_AndGetLatestFrame_ReturnsFrame()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 3);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        manager.AddConsumer(key, UsbCaptureSettings.Default, CancellationToken.None);

        var frame = manager.GetLatestFrame(key);

        Assert.NotNull(frame);
    }

    [Fact]
    public async Task ReadLatestFrame_ReturnsLatestFrameFromManager()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 3);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        manager.AddConsumer(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using var source = new UsbVideoSource(manager, key);

        var frame = source.ReadLatestFrame();

        Assert.NotNull(frame);
        Assert.Equal("usb:0:ANY", frame.Value.SourceId);
        Assert.Equal(2, frame.Value.Width);
        Assert.Equal(2, frame.Value.Height);
    }

    [Fact]
    public async Task MultipleSourcesForSameCamera_ShareOnePhysicalCapture()
    {
        var backend = new CountingUsbCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        manager.AddConsumer(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using (var source1 = new UsbVideoSource(manager, key))
        {
            var frame1 = source1.ReadLatestFrame();
            Assert.NotNull(frame1);
        }

        manager.AddConsumer(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using (var source2 = new UsbVideoSource(manager, key))
        {
            var frame2 = source2.ReadLatestFrame();
            Assert.NotNull(frame2);
        }

        Assert.Equal(1, backend.GetOpenCount(key));
    }

    [Fact]
    public async Task ReadLatestFrame_ReturnsNull_WhenManagerHasNoOwner()
    {
        var manager = new UsbCameraOwnerManager(new FakeUsbCaptureBackend());
        var key = new UsbCameraKey(0, "ANY");

        await using var source = new UsbVideoSource(manager, key);

        Assert.Null(source.ReadLatestFrame());
    }

    [Fact]
    public async Task DisposeAsync_ReleasesConsumerFromManager_ButOwnerPersistsForReuse()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 1);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        manager.AddConsumer(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using (new UsbVideoSource(manager, key))
        {
            Assert.NotNull(manager.GetLatestFrame(key));
        }

        // Owner persists after last consumer releases — frame still available
        Assert.NotNull(manager.GetLatestFrame(key));
    }

    [Fact]
    public async Task SourceProperties_ReturnCorrectValues()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 1);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(3, "V4L2");

        manager.AddConsumer(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using var source = new UsbVideoSource(manager, key);

        Assert.Equal("USB camera 3", source.SourceLabel);
        Assert.Null(source.Fps);
    }

    private sealed class FakeUsbCaptureBackend(int framesPerSession = 1) : IUsbCaptureBackend
    {
        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IUsbCaptureSession>(new FakeSession(key, framesPerSession));
        }
    }

    private sealed class FakeSession(UsbCameraKey key, int framesPerSession) : IUsbCaptureSession
    {
        private int _returned;
        private readonly UsbCapturedFrame? _firstFrame = CreateFirstFrame();

        private static UsbCapturedFrame? CreateFirstFrame()
        {
            using var frame = new Mat(2, 2, MatType.CV_8UC3, Scalar.All(50));
            Cv2.ImEncode(".jpg", frame, out var jpeg);
            return new UsbCapturedFrame("usb:0:ANY", 1, 2, 2, jpeg);
        }

        public ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (_returned >= framesPerSession)
            {
                UsbCapturedFrame? result = null;
                return new ValueTask<UsbCapturedFrame?>(result);
            }

            _returned++;
            if (_firstFrame is not null && _returned == 1)
            {
                return new ValueTask<UsbCapturedFrame?>(_firstFrame);
            }

            using var frame = new Mat(2, 2, MatType.CV_8UC3, Scalar.All(_returned * 50));
            Cv2.ImEncode(".jpg", frame, out var jpeg);
            return new ValueTask<UsbCapturedFrame?>(new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                _returned * 100,
                2, 2, jpeg));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingUsbCaptureBackend : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> _openCounts = new();

        public int GetOpenCount(UsbCameraKey key) => _openCounts.TryGetValue(key, out var count) ? count : 0;

        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            _openCounts[key] = GetOpenCount(key) + 1;
            return ValueTask.FromResult<IUsbCaptureSession>(new CountingSession(key));
        }
    }

    private sealed class CountingSession(UsbCameraKey key) : IUsbCaptureSession
    {
        public ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            using var frame = new Mat(2, 2, MatType.CV_8UC3, Scalar.All(100));
            Cv2.ImEncode(".jpg", frame, out var jpeg);
            return ValueTask.FromResult<UsbCapturedFrame?>(new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                1, 2, 2, jpeg));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
