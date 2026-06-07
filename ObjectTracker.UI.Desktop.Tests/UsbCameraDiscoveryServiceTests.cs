using ObjectTracker.UI.Desktop;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCameraDiscoveryServiceTests
{
    /// <summary>
    /// <description>Feature: UsbCameraDiscoveryService.DiscoverUsbCameraOptions disables and skips probing already-added sources.
    /// 
    ///   Scenario: Discovering options with "usb:0:ANY" already in the list should return it as unavailable with "already added" status without probing index 0 again, but still probe index 1.
    ///     Given a UsbCameraDiscoveryService with maxUsbCameraIndex=1 that always returns Unavailable for probes,
    ///      And DiscoverUsbCameraOptions is called with ["usb:0:ANY"],
    ///     Then exactly one option should be returned with Id "usb:0:ANY", IsAvailable false, Status containing "already added", and probed should contain [1].</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCameraDiscoveryService.DiscoverUsbCameraOptions probes and enables unknown available sources.
    /// 
    ///   Scenario: Discovering options with an empty list should probe index 0, find it available at 1280x720, and return it as enabled.
    ///     Given a UsbCameraDiscoveryService with maxUsbCameraIndex=0 that always returns Available(1280x720) for probes,
    ///      And DiscoverUsbCameraOptions is called with Array.Empty<string>(),
    ///     Then exactly one option should be returned with Id "usb:0:ANY", IsAvailable true, Status "Available", and DisplayName containing "1280x720".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCameraDiscoveryService.DiscoverUsbCameraOptions omits unknown unavailable sources after probing.
    /// 
    ///   Scenario: Discovering options with an empty list should probe index 0, find it unavailable, and return no options.
    ///     Given a UsbCameraDiscoveryService with maxUsbCameraIndex=0 that always returns Unavailable for probes,
    ///      And DiscoverUsbCameraOptions is called with Array.Empty<string>(),
    ///     Then options should be empty and probed should contain [0].</description>
    /// </summary>
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
