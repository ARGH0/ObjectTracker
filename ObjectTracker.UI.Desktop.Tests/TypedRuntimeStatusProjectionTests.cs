using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class TypedRuntimeStatusProjectionTests
{
    [Fact]
    public void RuntimeStatuses_UseDistinctTypesWithSameCanonicalStates()
    {
        var cameraSourceStatus = new CameraSourceStatus(CameraSourceStatusState.Stale, LatestFrameVersion: 4, FailureMessage: null);
        var laneStatus = new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion: 7, FailureMessage: null);

        Assert.IsType<CameraSourceStatus>(cameraSourceStatus);
        Assert.IsType<VisionPipelineLaneStatus>(laneStatus);
        Assert.Equal("stale", CameraSourceStatusProjection.GetStateName(cameraSourceStatus.State));
        Assert.Equal("stale", VisionPipelineLaneStatusProjection.GetStateName(laneStatus.State));
        Assert.Equal(new[] { "starting", "running", "stale", "failed", "stopped" }, CameraSourceStatusProjection.GetCanonicalStateNames());
        Assert.Equal(new[] { "starting", "running", "stale", "failed", "stopped" }, VisionPipelineLaneStatusProjection.GetCanonicalStateNames());
    }

    [Fact]
    public void BuildCameraPanelStatusProjection_ShowsCameraSourceAndVisionPipelineLaneStatusSeparately()
    {
        var projection = CameraPanelStatusProjection.Build(
            new CameraSourceStatus(CameraSourceStatusState.Running, LatestFrameVersion: 4, FailureMessage: null),
            new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Failed, LatestSnapshotVersion: 2, FailureMessage: "mask missing"));

        Assert.Equal(CameraSourceStatusState.Running, projection.CameraSourceStatus.State);
        Assert.Equal(VisionPipelineLaneStatusState.Failed, projection.VisionPipelineLaneStatus.State);
        Assert.Equal("Camera Source Status: running", projection.CameraSourceStatusText);
        Assert.Equal("Vision Pipeline Lane Status: failed - mask missing", projection.VisionPipelineLaneStatusText);
    }

    [Fact]
    public void BuildTileStatusProjection_ShowsCompactCameraSourceAndVisionPipelineLaneStatusSeparately()
    {
        var projection = CameraTileStatusProjection.Build(
            new CameraSourceStatus(CameraSourceStatusState.Stale, LatestFrameVersion: 4, FailureMessage: null),
            new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Running, LatestSnapshotVersion: 8, FailureMessage: null));

        Assert.Equal(CameraSourceStatusState.Stale, projection.CameraSourceState);
        Assert.Equal(VisionPipelineLaneStatusState.Running, projection.VisionPipelineLaneState);
        Assert.Equal("FEED STALE", projection.CameraSourceBadgeText);
        Assert.Equal("LANE RUNNING", projection.VisionPipelineLaneBadgeText);
    }
}
