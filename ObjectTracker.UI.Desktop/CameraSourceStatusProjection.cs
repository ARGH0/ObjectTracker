using System.Collections.Generic;
using ObjectTracker.UI.Desktop.Enums;
using ObjectTracker.Vision.Source;

namespace ObjectTracker.UI.Desktop;

public readonly record struct CameraSourceStatusView(
    string StatusText,
    bool ShowFrameAge,
    bool ShowPlaceholder,
    bool RestartEnabled,
    string RestartDisabledReason,
    bool RaisesAmbiguityAlert);

public static class CameraSourceStatusProjection
{
    public static string GetStateName(CameraSourceStatusState state)
    {
        return TypedStatusStateNames.GetName(state);
    }

    public static IReadOnlyList<string> GetCanonicalStateNames()
    {
        return TypedStatusStateNames.CanonicalStateNames;
    }

    public static CameraSourceStatusView BuildUsbStatus(
        bool isUsbCameraSource,
        bool isActivelyProcessedByVisionPipeline,
        UsbCameraRuntimeStatus status)
    {
        if (!isUsbCameraSource)
        {
            return new CameraSourceStatusView(string.Empty, false, false, false, string.Empty, false);
        }

        var restartEnabled = !isActivelyProcessedByVisionPipeline;
        var restartDisabledReason = restartEnabled
            ? string.Empty
            : "Stop Vision Pipeline to restart this camera source.";

        return status.State switch
        {
            UsbCameraOwnerState.Failed => new CameraSourceStatusView(
                $"USB Camera Source: failed - {status.FailureMessage ?? "unknown error"}",
                ShowFrameAge: status.FrameAgeMs is not null,
                ShowPlaceholder: true,
                restartEnabled,
                restartDisabledReason,
                RaisesAmbiguityAlert: false),
            UsbCameraOwnerState.Starting => new CameraSourceStatusView(
                "USB Camera Source: starting",
                ShowFrameAge: false,
                ShowPlaceholder: true,
                restartEnabled,
                restartDisabledReason,
                RaisesAmbiguityAlert: false),
            UsbCameraOwnerState.Running when status.IsStale => new CameraSourceStatusView(
                $"USB Camera Source: stale ({status.FrameAgeMs ?? 0} ms since last frame)",
                ShowFrameAge: true,
                ShowPlaceholder: false,
                restartEnabled,
                restartDisabledReason,
                RaisesAmbiguityAlert: false),
            UsbCameraOwnerState.Running => new CameraSourceStatusView(
                "USB Camera Source: running",
                ShowFrameAge: false,
                ShowPlaceholder: false,
                restartEnabled,
                restartDisabledReason,
                RaisesAmbiguityAlert: false),
            _ => new CameraSourceStatusView(
                "USB Camera Source: stopped",
                ShowFrameAge: false,
                ShowPlaceholder: false,
                restartEnabled,
                restartDisabledReason,
                RaisesAmbiguityAlert: false)
        };
    }

    public static CameraSourceStatusView BuildFileStatus(
        bool isFileCameraSource,
        FileCameraSourceRuntimeStatus status)
    {
        if (!isFileCameraSource)
        {
            return new CameraSourceStatusView(string.Empty, false, false, false, string.Empty, false);
        }

        return status.State switch
        {
            FileCameraSourceFeedState.Failed => new CameraSourceStatusView(
                $"File Camera Source: failed - {status.FailureMessage ?? "unknown error"}",
                ShowFrameAge: status.FrameAgeMs is not null,
                ShowPlaceholder: true,
                RestartEnabled: false,
                RestartDisabledReason: string.Empty,
                RaisesAmbiguityAlert: false),
            FileCameraSourceFeedState.Starting => new CameraSourceStatusView(
                "File Camera Source: starting",
                ShowFrameAge: false,
                ShowPlaceholder: true,
                RestartEnabled: false,
                RestartDisabledReason: string.Empty,
                RaisesAmbiguityAlert: false),
            FileCameraSourceFeedState.Running when status.IsStale => new CameraSourceStatusView(
                $"File Camera Source: stale ({status.FrameAgeMs ?? 0} ms since last frame)",
                ShowFrameAge: true,
                ShowPlaceholder: false,
                RestartEnabled: false,
                RestartDisabledReason: string.Empty,
                RaisesAmbiguityAlert: false),
            FileCameraSourceFeedState.Running => new CameraSourceStatusView(
                "File Camera Source: running",
                ShowFrameAge: false,
                ShowPlaceholder: false,
                RestartEnabled: false,
                RestartDisabledReason: string.Empty,
                RaisesAmbiguityAlert: false),
            _ => new CameraSourceStatusView(
                "File Camera Source: stopped",
                ShowFrameAge: false,
                ShowPlaceholder: false,
                RestartEnabled: false,
                RestartDisabledReason: string.Empty,
                RaisesAmbiguityAlert: false)
        };
    }
}
