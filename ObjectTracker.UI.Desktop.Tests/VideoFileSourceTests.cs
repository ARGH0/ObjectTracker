using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class VideoFileSourceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ObjectTracker.Test." + Guid.NewGuid());

    [Fact]
    public async Task ReturnsFramesUntilExhausted_ThenReturnsNull()
    {
        var videoPath = CreateTestVideo(_tempDir, frameCount: 5);

        await using var source = new VideoFileSource(videoPath, "test");

        Assert.Equal(5, source.Fps);
        Assert.Equal("test", source.SourceLabel);

        for (int i = 1; i <= 5; i++)
        {
            var frame = source.ReadLatestFrame();
            Assert.NotNull(frame);
            Assert.Equal(64, frame.Value.Width);
            Assert.Equal(48, frame.Value.Height);
            Assert.NotEmpty(frame.Value.EncodedJpeg);
        }

        var exhausted = source.ReadLatestFrame();
        Assert.Null(exhausted);
    }

    [Fact]
    public async Task ReadLatestFrame_ReturnsEachFrameOnlyOnce_Sequentially()
    {
        var videoPath = CreateTestVideo(_tempDir, frameCount: 3);

        await using var source = new VideoFileSource(videoPath, "sequential");

        var first = source.ReadLatestFrame();
        var second = source.ReadLatestFrame();
        var third = source.ReadLatestFrame();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);
        Assert.True(first.Value.EncodedJpeg.SequenceEqual(second.Value.EncodedJpeg) == false);
    }

    [Fact]
    public async Task DisposeAsync_ReleasesUnderlyingCapture()
    {
        var videoPath = CreateTestVideo(_tempDir, frameCount: 1);

        var source = new VideoFileSource(videoPath, "dispose");
        await source.DisposeAsync();

        var afterDispose = source.ReadLatestFrame();
        Assert.Null(afterDispose);
    }

    private static string CreateTestVideo(string dir, int frameCount)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "test_video.avi");

        using var writer = new VideoWriter(path, FourCC.MJPG, 5, new Size(64, 48));

        for (int i = 0; i < frameCount; i++)
        {
            using var frame = new Mat(48, 64, MatType.CV_8UC3, Scalar.All((byte)(i * 50)));
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
}
