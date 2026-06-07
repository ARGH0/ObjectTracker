using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class TypedRuntimeStatusProjectionTests
{
    /// <summary>
    /// <description>Feature: Typed runtime statuses use distinct types but share the same canonical state names.
    /// 
    ///   Scenario: Creating a CameraSourceStatus and VisionPipelineLaneStatus both in Stale state should be of different types, each with matching GetStateName output and identical GetCanonicalStateNames arrays.
    ///     Given new CameraSourceStatus(CameraSourceStatusState.Stale, LatestFrameVersion:4) and new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion:7),
    ///     Then cameraSourceStatus should be of type CameraSourceStatus, laneStatus should be of type VisionPipelineLaneStatus, GetStateName for both should return "stale", and GetCanonicalStateNames for both should return ["starting","running","stale","failed","stopped"].</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraPanelStatusProjection.Build shows camera source and Vision Pipeline lane status separately.
    /// 
    ///   Scenario: Building a panel status projection with a running camera source and a failed lane should produce correct separate status text for each component.
    ///     Given Build is called with CameraSourceStatus(Running, LatestFrameVersion:4) and VisionPipelineLaneStatus(Failed, LatestSnapshotVersion:2, FailureMessage:"mask missing"),
    ///     Then projection.CameraSourceStatus.State should be Running, projection.VisionPipelineLaneStatus.State should be Failed, projection.CameraSourceStatusText should be "Camera Source Status: running", and projection.VisionPipelineLaneStatusText should be "Vision Pipeline Lane Status: failed - mask missing".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraTileStatusProjection.Build shows compact camera source and Vision Pipeline lane status separately.
    /// 
    ///   Scenario: Building a tile status projection with a stale camera source and running lane should produce correct separate badge text for each component.
    ///     Given Build is called with CameraSourceStatus(Stale, LatestFrameVersion:4) and VisionPipelineLaneStatus(Running, LatestSnapshotVersion:8),
    ///     Then projection.CameraSourceState should be Stale, projection.VisionPipelineLaneState should be Running, projection.CameraSourceBadgeText should be "FEED STALE", and projection.VisionPipelineLaneBadgeText should be "LANE RUNNING".</description>
    /// </summary>
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
