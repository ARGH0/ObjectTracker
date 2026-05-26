using OpenCvSharp;

namespace ObjectTracker.Vision.Source;

public static class OpenCvUsbCameraDiscovery
{
    public static IReadOnlyList<UsbCameraOption> DiscoverOptions(int maxUsbCameraProbeIndex)
    {
        var api = GetDefaultUsbCaptureApi();
        var apiId = api.ToString().ToLowerInvariant();
        var options = new List<UsbCameraOption>();

        for (var cameraIndex = 0; cameraIndex <= maxUsbCameraProbeIndex; cameraIndex++)
        {
            using var capture = new VideoCapture(cameraIndex, api);
            capture.Set(VideoCaptureProperties.BufferSize, 1);
            if (!capture.IsOpened())
            {
                continue;
            }

            var width = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameWidth));
            var height = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameHeight));
            var sizeSuffix = width > 0 && height > 0
                ? $" ({width}x{height})"
                : string.Empty;

            options.Add(new UsbCameraOption(
                $"usb:{cameraIndex}:{apiId}",
                $"USB camera {cameraIndex}{sizeSuffix}",
                cameraIndex,
                apiId));
        }

        return options;
    }

    private static VideoCaptureAPIs GetDefaultUsbCaptureApi()
    {
        return OperatingSystem.IsWindows()
            ? VideoCaptureAPIs.DSHOW
            : VideoCaptureAPIs.ANY;
    }
}
