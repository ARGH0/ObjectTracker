using ObjectTracker.Vision.Source;

namespace ObjectTracker.UI.Desktop;

public readonly record struct UsbCaptureSettingsView(bool IsVisible, string ModeStatusText);

public readonly record struct UsbCaptureSettingsApplyDecision(bool ShouldRestartCameraSource, string Message);

public static class UsbCaptureSettingsProjection
{
    public static UsbCaptureSettingsView Build(
        bool isUsbCameraSource,
        bool isVisible,
        UsbCameraRuntimeStatus status,
        UsbCaptureSettingsRequest requested)
    {
        if (!isUsbCameraSource)
        {
            return new UsbCaptureSettingsView(false, string.Empty);
        }

        var requestedText = $"Requested: {requested.Width}x{requested.Height}@{requested.TargetFps}";
        if (status.Width is { } width && status.Height is { } height && status.ActualFps is { } fps)
        {
            return new UsbCaptureSettingsView(true, $"{requestedText}; running: {width}x{height}@{fps:0}");
        }

        return new UsbCaptureSettingsView(true, requestedText);
    }

    public static UsbCaptureSettingsApplyDecision BuildApplyDecision(
        bool isUsbCameraSource,
        bool isVisible,
        UsbCameraRuntimeStatus status)
    {
        if (!isUsbCameraSource)
        {
            return new UsbCaptureSettingsApplyDecision(false, string.Empty);
        }

        if (status.State == UsbCameraOwnerState.Failed)
        {
            return new UsbCaptureSettingsApplyDecision(false, "Use Restart Camera Source to retry failed USB hardware.");
        }

        return new UsbCaptureSettingsApplyDecision(
            ShouldRestartCameraSource: isVisible && status.State == UsbCameraOwnerState.Running,
            Message: string.Empty);
    }
}
