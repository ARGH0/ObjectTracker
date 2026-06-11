using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class BackgroundEstimationEngineUnifiedSourceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ObjectTracker.Test.Engine." + Guid.NewGuid());

    [Fact]
    public async Task ProcessAsync_WithVideoFileSource_ProcessesFramesAndStopsOnShouldStopEarly()
    {
        var videoPath = CreateTestVideoWithMotion(_tempDir, frameCount: 10);

        await using var source = new VideoFileSource(videoPath, "test-zone");
        var engine = new BackgroundEstimationEngine();
        var processedFrames = 0;

        var result = await engine.ProcessAsync(
            source,
            sampleCount: 3,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onFrame: _ =>
            {
                processedFrames++;
                return Task.CompletedTask;
            },
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldStopEarly: () => processedFrames >= 2,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.StoppedEarly);
        Assert.True(processedFrames >= 2);
    }

    [Fact]
    public async Task ProcessAsync_WithVideoFileSource_StopsEarlyWhenRequested()
    {
        var videoPath = CreateTestVideoWithMotion(_tempDir, frameCount: 20);

        await using var source = new VideoFileSource(videoPath, "early-stop-zone");
        var engine = new BackgroundEstimationEngine();
        var processedFrames = 0;

        var result = await engine.ProcessAsync(
            source,
            sampleCount: 3,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onFrame: _ =>
            {
                processedFrames++;
                return Task.CompletedTask;
            },
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldStopEarly: () => processedFrames >= 3,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.StoppedEarly);
    }

    [Fact]
    public async Task ProcessAsync_WithUsbVideoSource_ProcessesFramesIdentically()
    {
        var backend = new FakeUsbCaptureBackend(framesPerSession: 10);
        await using var manager = new UsbCameraOwnerManager(backend);
        var key = new UsbCameraKey(0, "ANY");

        manager.AddConsumer(key, UsbCaptureSettings.Default, CancellationToken.None);
        await using var source = new UsbVideoSource(manager, key);

        var engine = new BackgroundEstimationEngine();
        var processedFrames = 0;

        var result = await engine.ProcessAsync(
            source,
            sampleCount: 3,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onFrame: _ =>
            {
                processedFrames++;
                return Task.CompletedTask;
            },
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldStopEarly: () => processedFrames >= 2,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(processedFrames >= 2);
    }

    [Fact]
    public async Task ProcessAsync_WithNoFrames_ReturnsFailure()
    {
        var engine = new BackgroundEstimationEngine();
        var source = new EmptyVideoSource("empty");

        var result = await engine.ProcessAsync(
            source,
            sampleCount: 3,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onFrame: _ => Task.CompletedTask,
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldStopEarly: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("empty", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTestVideoWithMotion(string dir, int frameCount)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "motion_video.avi");

        using var writer = new VideoWriter(path, FourCC.MJPG, 5, new Size(64, 48));

        for (int i = 0; i < frameCount; i++)
        {
            // Uniform background with a moving white rectangle that creates motion
            using var frame = new Mat(48, 64, MatType.CV_8UC3, Scalar.All(128));
            var rectX = (i * 10) % 40;
            Cv2.Rectangle(frame, new Rect(rectX, 10, 20, 20), Scalar.White, -1);
            writer.Write(frame);
        }

        return path;
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, true); }
        catch { }
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

    private sealed class EmptyVideoSource(string label) : IVideoSource
    {
        public UsbFrameSnapshot? ReadLatestFrame() => null;
        public string SourceLabel => label;
        public int? Fps => null;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
