using ObjectTracker.UI.Desktop;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCameraSelectionDialogTests
{
    [Fact]
    public void ResolveSelectableOption_DisabledAlreadyAddedOption_CannotBeConfirmed()
    {
        var options = new[]
        {
            new UsbCameraOption("usb:0:ANY", "USB camera 0 (already added)", 0, VideoCaptureAPIs.ANY, IsAvailable: false, Status: "Already added")
        };

        var selected = UsbCameraSelectionPolicy.ResolveSelectableOption(options, selectedIndex: 0);

        Assert.Null(selected);
    }

    [Fact]
    public void ResolveSelectableOption_EnabledOption_CanBeConfirmed()
    {
        var options = new[]
        {
            new UsbCameraOption("usb:0:ANY", "USB camera 0", 0, VideoCaptureAPIs.ANY)
        };

        var selected = UsbCameraSelectionPolicy.ResolveSelectableOption(options, selectedIndex: 0);

        Assert.Equal("usb:0:ANY", selected?.Id);
    }
}
