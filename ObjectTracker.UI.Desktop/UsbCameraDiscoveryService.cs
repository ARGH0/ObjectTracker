using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

public readonly record struct UsbCameraProbeResult(bool IsAvailable, int Width, int Height)
{
    public static UsbCameraProbeResult Unavailable { get; } = new(false, 0, 0);
}

public sealed class UsbCameraDiscoveryService
{
    private readonly int maxUsbCameraIndex;
    private readonly VideoCaptureAPIs api;
    private readonly Func<int, UsbCameraProbeResult> probe;

    public UsbCameraDiscoveryService(int maxUsbCameraIndex, VideoCaptureAPIs api, Func<int, UsbCameraProbeResult> probe)
    {
        this.maxUsbCameraIndex = maxUsbCameraIndex;
        this.api = api;
        this.probe = probe;
    }

    public IReadOnlyList<UsbCameraOption> DiscoverUsbCameraOptions(IEnumerable<string> alreadyAddedSourceIds)
    {
        var alreadyAdded = alreadyAddedSourceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var options = new List<UsbCameraOption>();

        for (var cameraIndex = 0; cameraIndex <= maxUsbCameraIndex; cameraIndex++)
        {
            var id = BuildSourceId(cameraIndex);
            if (alreadyAdded.Contains(id))
            {
                options.Add(new UsbCameraOption(
                    id,
                    $"USB camera {cameraIndex} (already added)",
                    cameraIndex,
                    api,
                    IsAvailable: false,
                    Status: "Already added"));
                continue;
            }

            var result = probe(cameraIndex);
            if (!result.IsAvailable)
            {
                continue;
            }

            var sizeSuffix = result.Width > 0 && result.Height > 0
                ? $" ({result.Width}x{result.Height})"
                : string.Empty;

            options.Add(new UsbCameraOption(
                id,
                $"USB camera {cameraIndex}{sizeSuffix}",
                cameraIndex,
                api));
        }

        return options;
    }

    private string BuildSourceId(int cameraIndex) => $"usb:{cameraIndex}:{api.ToString().ToUpperInvariant()}";
}
