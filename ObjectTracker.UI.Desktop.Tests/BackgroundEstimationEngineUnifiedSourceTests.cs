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

    [Fact]
    public async Task ProcessAsync_WhenAdaptiveBackgroundRefreshes_KeepsProcessingFrameSizeConsistent()
    {
        var videoPath = CreateUniformVideo(_tempDir, frameCount: 10, width: 96, height: 64);

        await using var source = new VideoFileSource(videoPath, "adaptive-size-zone");
        var engine = new BackgroundEstimationEngine();
        var processedFrames = 0;
        var options = BackgroundEstimationEngine.ProcessingOptions.Default with
        {
            ProcessMaxWidth = 48,
            AdaptiveBackgroundSampleCount = 1,
            AdaptiveBackgroundUpdateIntervalFrames = 1
        };

        var result = await engine.ProcessAsync(
            source,
            sampleCount: 3,
            threshold: 25,
            options,
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
        Assert.Equal(3, processedFrames);
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

    private static string CreateUniformVideo(string dir, int frameCount, int width, int height)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "uniform_video.avi");

        using var writer = new VideoWriter(path, FourCC.MJPG, 5, new Size(width, height));

        for (int i = 0; i < frameCount; i++)
        {
            using var frame = new Mat(height, width, MatType.CV_8UC3, Scalar.All(128));
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

    private sealed class EmptyVideoSource(string label) : IVideoSource
    {
        public VideoFrameSnapshot? ReadLatestFrame() => null;
        public string SourceLabel => label;
        public int? Fps => null;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
