using OpenCvSharp;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision.Source;

public static class FrameSourceCatalog
{
    public static IReadOnlyList<IFrameSource> DiscoverDefaultSources(
        int maxUsbCameraIndex = 0,
        bool includeMock = true)
    {
        var api = OperatingSystem.IsWindows()
            ? VideoCaptureAPIs.DSHOW
            : VideoCaptureAPIs.ANY;

        return DiscoverDefaultSources(maxUsbCameraIndex, includeMock, api);
    }

    public static IReadOnlyList<IFrameSource> DiscoverDefaultSources(
        int maxUsbCameraIndex = 0,
        bool includeMock = true,
        VideoCaptureAPIs api = VideoCaptureAPIs.ANY)
    {
        var sources = new List<IFrameSource>();

        if (includeMock)
        {
            sources.Add(new MockFrameSource());
        }

        for (var cameraIndex = 0; cameraIndex <= maxUsbCameraIndex; cameraIndex++)
        {
            sources.Add(new OpenCvUsbFrameSource(cameraIndex, api));
        }

        return sources;
    }
}