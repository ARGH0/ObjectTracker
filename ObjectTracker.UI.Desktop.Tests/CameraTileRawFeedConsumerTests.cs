using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileRawFeedConsumerTests
{
    [Fact]
    public async Task RenderNextFrameAsync_UsesSameLatestFrameBehaviorForUsbAndFileFeeds()
    {
        var consumer = new CameraTileRawFeedConsumer();
        var rendered = new List<string>();
        var usbFeed = new FakeRawFrameFeed(new CameraTileRawFrameSnapshot("usb:0:ANY", 1, 640, 480, [1]));
        var fileFeed = new FakeRawFrameFeed(new CameraTileRawFrameSnapshot("file-bridge", 1, 640, 480, [2]));

        var usbVersion = await consumer.RenderNextFrameAsync(
            usbFeed,
            previousVersion: 0,
            onFrame: frame =>
            {
                rendered.Add($"{frame.CameraId}:{frame.FrameVersion}:{frame.EncodedJpeg[0]}");
                return Task.CompletedTask;
            },
            CancellationToken.None);
        var fileVersion = await consumer.RenderNextFrameAsync(
            fileFeed,
            previousVersion: 0,
            onFrame: frame =>
            {
                rendered.Add($"{frame.CameraId}:{frame.FrameVersion}:{frame.EncodedJpeg[0]}");
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(1, usbVersion);
        Assert.Equal(1, fileVersion);
        Assert.Equal(new[]
        {
            "usb:0:ANY:1:1",
            "file-bridge:1:2"
        }, rendered);
    }

    private sealed class FakeRawFrameFeed(CameraTileRawFrameSnapshot snapshot) : ICameraTileRawFrameFeed
    {
        public CameraTileRawFrameSnapshot? LatestFrame => snapshot;

        public Task<CameraTileRawFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult<CameraTileRawFrameSnapshot?>(snapshot.FrameVersion > previousVersion ? snapshot : null);
        }
    }
}
