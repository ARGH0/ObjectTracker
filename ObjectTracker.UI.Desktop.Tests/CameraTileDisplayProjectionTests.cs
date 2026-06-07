using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileDisplayProjectionTests
{
    [Fact]
    public void Build_WhenNoFrameHasArrived_UsesDefaultRepeatLastFrameBehaviorAndShowsPlaceholder()
    {
        var view = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: false);

        Assert.Equal(CameraTileMissingFrameBehavior.RepeatLastFrame, view.MissingFrameBehavior);
        Assert.Equal(CameraTileFrameDisplay.Placeholder, view.FrameDisplay);
        Assert.Equal("Waiting for first frame", view.PlaceholderText);
    }

    [Theory]
    [InlineData(CameraTileMissingFrameBehavior.RepeatLastFrame, CameraTileFrameDisplay.LastFrame)]
    [InlineData(CameraTileMissingFrameBehavior.BlackFrame, CameraTileFrameDisplay.BlackFrame)]
    public void Build_WhenFrameIsMissingAfterPreviousFrame_AppliesConfiguredMissingFrameBehavior(
        CameraTileMissingFrameBehavior missingFrameBehavior,
        CameraTileFrameDisplay expectedDisplay)
    {
        var view = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            missingFrameBehavior);

        Assert.Equal(expectedDisplay, view.FrameDisplay);
    }

    [Theory]
    [InlineData(CameraTileMissingFrameBehavior.RepeatLastFrame)]
    [InlineData(CameraTileMissingFrameBehavior.BlackFrame)]
    public void Build_WhenCameraSourceIsStaleOrFailed_ShowsCameraSourceStatusOverlayRegardlessOfMissingFrameBehavior(
        CameraTileMissingFrameBehavior missingFrameBehavior)
    {
        var stale = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            missingFrameBehavior,
            cameraSourceStatus: new CameraSourceStatus(CameraSourceStatusState.Stale, LatestFrameVersion: 4, FailureMessage: null));
        var failed = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            missingFrameBehavior,
            cameraSourceStatus: new CameraSourceStatus(CameraSourceStatusState.Failed, LatestFrameVersion: 4, FailureMessage: "camera unavailable"));

        Assert.True(stale.ShowCameraSourceStatusOverlay);
        Assert.True(failed.ShowCameraSourceStatusOverlay);
    }

    [Theory]
    [InlineData(MainWindow.CameraTileFrameSource.PipelineSnapshotAnnotatedFrame)]
    [InlineData(MainWindow.CameraTileFrameSource.PipelineSnapshotDebugFrames)]
    public void Build_WhenVisionPipelineLaneIsStaleOrFailed_ShowsLaneStatusOverlayForSnapshotTileModes(
        MainWindow.CameraTileFrameSource frameSource)
    {
        var stale = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            frameSource: frameSource,
            visionPipelineLaneStatus: new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion: 4, FailureMessage: null));
        var failed = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            frameSource: frameSource,
            visionPipelineLaneStatus: new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Failed, LatestSnapshotVersion: 4, FailureMessage: "mask missing"));

        Assert.True(stale.ShowVisionPipelineLaneStatusOverlay);
        Assert.True(failed.ShowVisionPipelineLaneStatusOverlay);
    }
}
