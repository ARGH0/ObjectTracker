using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraProfileUsbSourceTests
{
    [Fact]
    public void CreateUsbCamera_CreatesCameraProfileForSelectedUsbCameraSource()
    {
        var device = new UsbCameraDevice(
            "usb:/dev/video2",
            "Logitech BRIO 2",
            "Logitech BRIO",
            2,
            "/dev/video2");

        var camera = MainWindow.CameraProfile.CreateUsbCamera(device);

        Assert.Equal("usb:/dev/video2", camera.Id);
        Assert.Equal("Logitech BRIO 2", camera.DisplayName);
        Assert.Equal(MainWindow.CameraSourceKind.UsbCamera, camera.SourceKind);
        Assert.Equal(2, camera.UsbDeviceIndex);
        Assert.Empty(camera.VideoPaths);
        Assert.Equal("Logitech BRIO 2", camera.CurrentSourceLabel);
    }
}
