namespace ObjectTracker.Vision.Source;

public readonly record struct UsbCameraOption(
    string Id,
    string DisplayName,
    int CameraIndex,
    string ApiId);