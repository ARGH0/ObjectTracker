using VideoCaptureAPIs = OpenCvSharp.VideoCaptureAPIs;

namespace ObjectTracker.UI.Desktop;

internal enum CameraAddChoice
{
    VideoFiles,
    UsbCamera
}

internal readonly record struct UsbCameraOption(string Id, string DisplayName, int CameraIndex, VideoCaptureAPIs Api);
