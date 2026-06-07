using System.Collections.Generic;
using ObjectTracker.UI.Desktop.Enums;

namespace ObjectTracker.UI.Desktop;

public readonly record struct CameraSourceStatus(
    CameraSourceStatusState State,
    long? LatestFrameVersion,
    string? FailureMessage);

public readonly record struct VisionPipelineLaneStatus(
    VisionPipelineLaneStatusState State,
    long? LatestSnapshotVersion,
    string? FailureMessage);

public static class TypedStatusStateNames
{
    public static IReadOnlyList<string> CanonicalStateNames { get; } =
    [
        "starting",
        "running",
        "stale",
        "failed",
        "stopped"
    ];

    public static string GetName(CameraSourceStatusState state)
    {
        return state switch
        {
            CameraSourceStatusState.Starting => "starting",
            CameraSourceStatusState.Running => "running",
            CameraSourceStatusState.Stale => "stale",
            CameraSourceStatusState.Failed => "failed",
            CameraSourceStatusState.Stopped => "stopped",
            _ => "stopped"
        };
    }

    public static string GetName(VisionPipelineLaneStatusState state)
    {
        return state switch
        {
            VisionPipelineLaneStatusState.Starting => "starting",
            VisionPipelineLaneStatusState.Running => "running",
            VisionPipelineLaneStatusState.Stale => "stale",
            VisionPipelineLaneStatusState.Failed => "failed",
            VisionPipelineLaneStatusState.Stopped => "stopped",
            _ => "stopped"
        };
    }
}

public static class VisionPipelineLaneStatusProjection
{
    public static string GetStateName(VisionPipelineLaneStatusState state)
    {
        return TypedStatusStateNames.GetName(state);
    }

    public static IReadOnlyList<string> GetCanonicalStateNames()
    {
        return TypedStatusStateNames.CanonicalStateNames;
    }

    public static string BuildStatusText(VisionPipelineLaneStatus status)
    {
        var stateName = GetStateName(status.State);
        return status.State == VisionPipelineLaneStatusState.Failed
            ? $"Vision Pipeline Lane Status: {stateName} - {status.FailureMessage ?? "unknown error"}"
            : $"Vision Pipeline Lane Status: {stateName}";
    }
}

public readonly record struct CameraPanelStatusView(
    CameraSourceStatus CameraSourceStatus,
    VisionPipelineLaneStatus VisionPipelineLaneStatus,
    string CameraSourceStatusText,
    string VisionPipelineLaneStatusText);

public static class CameraPanelStatusProjection
{
    public static CameraPanelStatusView Build(CameraSourceStatus cameraSourceStatus, VisionPipelineLaneStatus visionPipelineLaneStatus)
    {
        return new CameraPanelStatusView(
            cameraSourceStatus,
            visionPipelineLaneStatus,
            BuildCameraSourceStatusText(cameraSourceStatus),
            VisionPipelineLaneStatusProjection.BuildStatusText(visionPipelineLaneStatus));
    }

    private static string BuildCameraSourceStatusText(CameraSourceStatus status)
    {
        var stateName = CameraSourceStatusProjection.GetStateName(status.State);
        return status.State == CameraSourceStatusState.Failed
            ? $"Camera Source Status: {stateName} - {status.FailureMessage ?? "unknown error"}"
            : $"Camera Source Status: {stateName}";
    }
}

public readonly record struct CameraTileStatusView(
    CameraSourceStatusState CameraSourceState,
    VisionPipelineLaneStatusState VisionPipelineLaneState,
    string CameraSourceBadgeText,
    string VisionPipelineLaneBadgeText);

public static class CameraTileStatusProjection
{
    public static CameraTileStatusView Build(CameraSourceStatus cameraSourceStatus, VisionPipelineLaneStatus visionPipelineLaneStatus)
    {
        return new CameraTileStatusView(
            cameraSourceStatus.State,
            visionPipelineLaneStatus.State,
            $"FEED {CameraSourceStatusProjection.GetStateName(cameraSourceStatus.State).ToUpperInvariant()}",
            $"LANE {VisionPipelineLaneStatusProjection.GetStateName(visionPipelineLaneStatus.State).ToUpperInvariant()}");
    }
}
