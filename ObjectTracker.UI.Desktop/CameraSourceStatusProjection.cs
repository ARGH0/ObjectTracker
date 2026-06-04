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
}
