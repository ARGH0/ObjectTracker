using VideoCaptureAPIs = OpenCvSharp.VideoCaptureAPIs;

namespace ObjectTracker.UI.Desktop;

internal enum CameraAddChoice
{
    VideoFiles,
    UsbCamera
}

public readonly record struct UsbCameraOption(
    string Id,
    string DisplayName,
    int CameraIndex,
    VideoCaptureAPIs Api,
    bool IsAvailable = true,
    string Status = "Available");
