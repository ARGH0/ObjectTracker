using System;

namespace ObjectTracker.UI.Desktop;

internal static class CameraSourceFactory
{
    public static IVideoSource Create(MainWindow.CameraProfile camera, string? videoPath = null)
    {
        return camera.SourceKind switch
        {
            MainWindow.CameraSourceKind.UsbCamera => new UsbCameraSource(
                camera.UsbDeviceIndex ?? throw new InvalidOperationException($"USB camera {camera.DisplayName} has no device index."),
                camera.DisplayName),
            _ => new VideoFileSource(ResolveVideoPath(camera, videoPath), camera.DisplayName)
        };
    }

    private static string ResolveVideoPath(MainWindow.CameraProfile camera, string? videoPath)
    {
        if (!string.IsNullOrWhiteSpace(videoPath))
        {
            return videoPath;
        }

        if (!string.IsNullOrWhiteSpace(camera.PrimaryVideoPath))
        {
            return camera.PrimaryVideoPath;
        }

        throw new InvalidOperationException($"Video camera {camera.DisplayName} has no video path.");
    }
}
