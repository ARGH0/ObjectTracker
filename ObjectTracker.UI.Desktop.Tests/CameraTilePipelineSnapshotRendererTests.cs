using ObjectTracker.Core.Domain;
using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTilePipelineSnapshotRendererTests
{
    [Fact]
    public async Task RenderSnapshotAsync_WhenTileRouteUsesAnnotatedFrame_DisplaysAnnotatedFrameForCameraTile()
    {
        var renderer = new CameraTilePipelineSnapshotRenderer();
        var displayed = new List<CameraTileRawFrameSnapshot>();
        var snapshot = Snapshot(
            "cam-a",
            annotatedBytes: [7],
            debugFrames: []);
        var routing = new MainWindow.CameraTileFrameRouting([
            new MainWindow.CameraTileFrameRoute("cam-a", MainWindow.CameraTileFrameSource.PipelineSnapshotAnnotatedFrame)
        ]);

        await renderer.RenderSnapshotAsync(snapshot, routing, frame =>
        {
            displayed.Add(frame);
            return Task.CompletedTask;
        }, CancellationToken.None);

        var frame = Assert.Single(displayed);
        Assert.Equal("cam-a", frame.CameraId);
        Assert.Equal(snapshot.AnnotatedFrame.TimestampUtcMs, frame.FrameVersion);
        Assert.Equal(new byte[] { 7 }, frame.EncodedJpeg);
    }

    [Fact]
    public async Task RenderSnapshotAsync_WhenTileRouteUsesDebugFrames_DisplaysNamedDebugFramesForCameraTile()
    {
        var renderer = new CameraTilePipelineSnapshotRenderer();
        var annotatedFrames = new List<CameraTileRawFrameSnapshot>();
        var debugFrames = new List<CameraTileDebugFrameSnapshot>();
        var snapshot = Snapshot(
            "cam-a",
            annotatedBytes: [7],
            debugFrames:
            [
                new DebugFrame("motion", new FramePacket("cam-a", 30, 2, 2, [8])),
                new DebugFrame("train-tracking", new FramePacket("cam-a", 31, 2, 2, [9]))
            ]);
        var routing = new MainWindow.CameraTileFrameRouting([
            new MainWindow.CameraTileFrameRoute("cam-a", MainWindow.CameraTileFrameSource.PipelineSnapshotDebugFrames)
        ]);

        await renderer.RenderSnapshotAsync(snapshot, routing, frame =>
        {
            annotatedFrames.Add(frame);
            return Task.CompletedTask;
        }, debugFrame =>
        {
            debugFrames.Add(debugFrame);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Empty(annotatedFrames);
        Assert.Equal(new[] { "motion", "train-tracking" }, debugFrames.Select(frame => frame.Name).ToArray());
        Assert.Equal(new byte[] { 8 }, debugFrames[0].EncodedJpeg);
        Assert.Equal(new byte[] { 9 }, debugFrames[1].EncodedJpeg);
    }

    [Fact]
    public async Task RenderSnapshotAsync_WhenTileRouteUsesRawFeed_DoesNotDisplayPipelineSnapshot()
    {
        var renderer = new CameraTilePipelineSnapshotRenderer();
        var displayed = new List<CameraTileRawFrameSnapshot>();
        var snapshot = Snapshot(
            "cam-a",
            annotatedBytes: [7],
            debugFrames: []);
        var routing = new MainWindow.CameraTileFrameRouting([
            new MainWindow.CameraTileFrameRoute("cam-a", MainWindow.CameraTileFrameSource.RawCameraSourceFeed)
        ]);

        await renderer.RenderSnapshotAsync(snapshot, routing, frame =>
        {
            displayed.Add(frame);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Empty(displayed);
    }

    private static PipelineSnapshot Snapshot(string cameraSourceId, byte[] annotatedBytes, IReadOnlyList<DebugFrame> debugFrames)
    {
        var sourceFrame = new FramePacket(cameraSourceId, 10, 2, 2, [1]);
        var annotatedFrame = new FramePacket(cameraSourceId, 20, 2, 2, annotatedBytes);
        return new PipelineSnapshot(
            cameraSourceId,
            sourceFrame,
            annotatedFrame,
            [],
            [],
            [],
            debugFrames,
            DetectorMode.Color,
            new PipelineSnapshotTiming(20, 30, 29, 1.2));
    }
}
