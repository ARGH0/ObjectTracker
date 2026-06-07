using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileRawFeedConsumerTests
{
    /// <summary>
    /// <description>Feature: CameraTileRawFeedConsumer.RenderNextFrameAsync uses the same latest frame behavior for both USB and file feeds.
    /// 
    ///   Scenario: A raw feed consumer renders frames from both a USB camera source and a file camera source, returning correct version numbers.
    ///     Given a CameraTileRawFeedConsumer,
    ///      And a FakeRawFrameFeed for "usb:0:ANY" with frame version 1 and bytes [1],
    ///      And a FakeRawFrameFeed for "file-bridge" with frame version 1 and bytes [2],
    ///     When RenderNextFrameAsync is called for both feeds with previousVersion 0,
    ///     Then usbVersion should be 1,
    ///      And fileVersion should be 1,
    ///      And rendered should contain "usb:0:ANY:1:1" and "file-bridge:1:2".</description>
    /// </summary>
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
