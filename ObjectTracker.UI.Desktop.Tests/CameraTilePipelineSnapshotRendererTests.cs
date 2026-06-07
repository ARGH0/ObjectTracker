using ObjectTracker.Core.Domain;
using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTilePipelineSnapshotRendererTests
{
    /// <summary>
    /// <description>Feature: CameraTilePipelineSnapshotRenderer.RenderSnapshotAsync routes annotated frames to the correct tile when the route uses PipelineSnapshotAnnotatedFrame.
    /// 
    ///   Scenario: A pipeline snapshot with an annotated frame is rendered for a camera tile that requests annotated frames.
    ///     Given a CameraTilePipelineSnapshotRenderer,
    ///      And a PipelineSnapshot for "cam-a" with annotated bytes [7] and no debug frames,
    ///      And a CameraTileFrameRouting that maps "cam-a" to PipelineSnapshotAnnotatedFrame,
    ///     When RenderSnapshotAsync is called with the snapshot and routing,
    ///     Then exactly one frame should be displayed for camera "cam-a",
    ///      And the FrameVersion should match the annotated frame TimestampUtcMs,
    ///      And the EncodedJpeg should be [7].</description>
    /// </summary>
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
            new MainWindow.CameraTileFrameRoute("cam-a", CameraTileFrameSource.PipelineSnapshotAnnotatedFrame)
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

    /// <summary>
    /// <description>Feature: CameraTilePipelineSnapshotRenderer.RenderSnapshotAsync routes debug frames to the correct tile when the route uses PipelineSnapshotDebugFrames.
    /// 
    ///   Scenario: A pipeline snapshot with named debug frames is rendered for a camera tile that requests debug frames, no annotated frame should be displayed.
    ///     Given a CameraTilePipelineSnapshotRenderer,
    ///      And a PipelineSnapshot for "cam-a" with annotated bytes [7] and two debug frames ("motion" with bytes [8], "train-tracking" with bytes [9]),
    ///      And a CameraTileFrameRouting that maps "cam-a" to PipelineSnapshotDebugFrames,
    ///     When RenderSnapshotAsync is called with the snapshot and routing,
    ///     Then no annotated frames should be displayed,
    ///      And two debug frames should be displayed named "motion" and "train-tracking",
    ///      And the first debug frame EncodedJpeg should be [8],
    ///      And the second debug frame EncodedJpeg should be [9].</description>
    /// </summary>
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
            new MainWindow.CameraTileFrameRoute("cam-a", CameraTileFrameSource.PipelineSnapshotDebugFrames)
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

    /// <summary>
    /// <description>Feature: CameraTilePipelineSnapshotRenderer.RenderSnapshotAsync routes frames to distinct debug view slots when using current debug frame names.
    /// 
    ///   Scenario: A pipeline snapshot contains the standard set of named debug frames, each should be routed to its corresponding slot.
    ///     Given a CameraTilePipelineSnapshotRenderer,
    ///      And a PipelineSnapshot for "cam-a" with four debug frames ("source", "moving-object-observation", "train-observation", "train-tracking"),
    ///      And a CameraTileFrameRouting that maps "cam-a" to PipelineSnapshotDebugFrames,
    ///     When RenderSnapshotAsync is called with the snapshot and routing,
    ///     Then each debug frame should be routed to its correct slot: Source, MovingObjectObservation, TrainObservation, and TrainTracking.</description>
    /// </summary>
    [Fact]
    public async Task RenderSnapshotAsync_WhenSnapshotUsesCurrentDebugFrameNames_RoutesFramesToDistinctDebugViewSlots()
    {
        var renderer = new CameraTilePipelineSnapshotRenderer();
        var debugFrames = new List<CameraTileDebugFrameSnapshot>();
        var snapshot = Snapshot(
            "cam-a",
            annotatedBytes: [7],
            debugFrames:
            [
                new DebugFrame("source", new FramePacket("cam-a", 30, 2, 2, [8])),
                new DebugFrame("moving-object-observation", new FramePacket("cam-a", 31, 2, 2, [9])),
                new DebugFrame("train-observation", new FramePacket("cam-a", 32, 2, 2, [10])),
                new DebugFrame("train-tracking", new FramePacket("cam-a", 33, 2, 2, [11]))
            ]);
        var routing = new MainWindow.CameraTileFrameRouting([
            new MainWindow.CameraTileFrameRoute("cam-a", CameraTileFrameSource.PipelineSnapshotDebugFrames)
        ]);

        await renderer.RenderSnapshotAsync(snapshot, routing, _ => Task.CompletedTask, debugFrame =>
        {
            debugFrames.Add(debugFrame);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(new[]
        {
            CameraTileDebugFrameSlot.Source,
            CameraTileDebugFrameSlot.MovingObjectObservation,
            CameraTileDebugFrameSlot.TrainObservation,
            CameraTileDebugFrameSlot.TrainTracking
        }, debugFrames.Select(frame => frame.Slot).ToArray());
    }

    /// <summary>
    /// <description>Feature: CameraTilePipelineSnapshotRenderer.RenderSnapshotAsync routes new debug frame names to their corresponding slots.
    /// 
    ///   Scenario: A pipeline snapshot contains non-standard debug frame names that map to existing slots (moving-object-evidence and train-color-evidence).
    ///     Given a CameraTilePipelineSnapshotRenderer,
    ///      And a PipelineSnapshot for "cam-a" with two debug frames ("moving-object-evidence" with bytes [9], "train-color-evidence" with bytes [10]),
    ///      And a CameraTileFrameRouting that maps "cam-a" to PipelineSnapshotDebugFrames,
    ///     When RenderSnapshotAsync is called with the snapshot and routing,
    ///     Then the first debug frame should be routed to MovingObjectObservation slot,
    ///      And the second debug frame should be routed to TrainObservation slot.</description>
    /// </summary>
    [Fact]
    public async Task RenderSnapshotAsync_NewDebugFrameNames_RouteToCorrectSlots()
    {
        var renderer = new CameraTilePipelineSnapshotRenderer();
        var debugFrames = new List<CameraTileDebugFrameSnapshot>();
        var snapshot = Snapshot(
            "cam-a",
            annotatedBytes: [7],
            debugFrames:
            [
                new DebugFrame("moving-object-evidence", new FramePacket("cam-a", 31, 2, 2, [9])),
                new DebugFrame("train-color-evidence", new FramePacket("cam-a", 32, 2, 2, [10]))
            ]);
        var routing = new MainWindow.CameraTileFrameRouting([
            new MainWindow.CameraTileFrameRoute("cam-a", CameraTileFrameSource.PipelineSnapshotDebugFrames)
        ]);

        await renderer.RenderSnapshotAsync(snapshot, routing, _ => Task.CompletedTask, debugFrame =>
        {
            debugFrames.Add(debugFrame);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(new[]
        {
            CameraTileDebugFrameSlot.MovingObjectObservation,
            CameraTileDebugFrameSlot.TrainObservation
        }, debugFrames.Select(frame => frame.Slot).ToArray());
    }

    /// <summary>
    /// <description>Feature: CameraTilePipelineSnapshotRenderer.RenderSnapshotAsync does not display pipeline snapshot frames when the route uses RawCameraSourceFeed.
    /// 
    ///   Scenario: A pipeline snapshot with annotated and debug frames is rendered for a camera tile that requests raw feed, no frames should be displayed.
    ///     Given a CameraTilePipelineSnapshotRenderer,
    ///      And a PipelineSnapshot for "cam-a" with annotated bytes [7],
    ///      And a CameraTileFrameRouting that maps "cam-a" to RawCameraSourceFeed,
    ///     When RenderSnapshotAsync is called with the snapshot and routing,
    ///     Then no frames should be displayed.</description>
    /// </summary>
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
            new MainWindow.CameraTileFrameRoute("cam-a", CameraTileFrameSource.RawCameraSourceFeed)
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
            new PipelineSnapshotTiming(20, 30, 29, 1.2));
    }
}
