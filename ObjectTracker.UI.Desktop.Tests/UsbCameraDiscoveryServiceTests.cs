using ObjectTracker.UI.Desktop;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCameraDiscoveryServiceTests
{
    [Fact]
    public void DiscoverUsbCameraOptions_AlreadyAddedSource_IsDisabledAndNotProbed()
    {
        var probed = new List<int>();
        var service = new UsbCameraDiscoveryService(
            maxUsbCameraIndex: 1,
            api: VideoCaptureAPIs.ANY,
            probe: index =>
            {
                probed.Add(index);
                return UsbCameraProbeResult.Unavailable;
            });

        var options = service.DiscoverUsbCameraOptions(new[] { "usb:0:ANY" });

        var option = Assert.Single(options);
        Assert.Equal("usb:0:ANY", option.Id);
        Assert.False(option.IsAvailable);
        Assert.Contains("already added", option.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { 1 }, probed);
    }

    [Fact]
    public void DiscoverUsbCameraOptions_UnknownAvailableSource_IsProbedAndEnabled()
    {
        var service = new UsbCameraDiscoveryService(
            maxUsbCameraIndex: 0,
            api: VideoCaptureAPIs.ANY,
            probe: _ => new UsbCameraProbeResult(IsAvailable: true, Width: 1280, Height: 720));

        var options = service.DiscoverUsbCameraOptions(Array.Empty<string>());

        var option = Assert.Single(options);
        Assert.Equal("usb:0:ANY", option.Id);
        Assert.True(option.IsAvailable);
        Assert.Equal("Available", option.Status);
        Assert.Contains("1280x720", option.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoverUsbCameraOptions_UnknownUnavailableSource_IsProbedAndOmitted()
    {
        var probed = new List<int>();
        var service = new UsbCameraDiscoveryService(
            maxUsbCameraIndex: 0,
            api: VideoCaptureAPIs.ANY,
            probe: index =>
            {
                probed.Add(index);
                return UsbCameraProbeResult.Unavailable;
            });

        var options = service.DiscoverUsbCameraOptions(Array.Empty<string>());

        Assert.Empty(options);
        Assert.Equal(new[] { 0 }, probed);
    }
}
