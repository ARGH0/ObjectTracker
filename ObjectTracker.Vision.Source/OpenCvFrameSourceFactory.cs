using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision.Source;

public sealed class OpenCvFrameSourceFactory : IFrameSourceFactory
{
    public IReadOnlyList<FrameSourceInfo> GetAvailableSources()
    {
        return
        [
            new FrameSourceInfo("mock", "Mock simulatie"),
            new FrameSourceInfo("usb:0:dshow", "USB camera 0 (DSHOW)"),
            new FrameSourceInfo("usb:0:msmf", "USB camera 0 (MSMF)")
        ];
    }

    public IFrameSource Create(string sourceId)
    {
        return sourceId switch
        {
            "mock" => new MockFrameSource(),
            "usb:0:dshow" => new OpenCvUsbFrameSource(0, VideoCaptureAPIs.DSHOW),
            "usb:0:msmf" => new OpenCvUsbFrameSource(0, VideoCaptureAPIs.MSMF),
            _ => throw new InvalidOperationException($"Onbekende frame source '{sourceId}'.")
        };
    }
}