using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraSourceFactoryTests
{
    [Fact]
    public async Task Create_ForVideoFileCameraProfile_ReturnsVideoFileSource()
    {
        var camera = MainWindow.CameraProfile.CreateVideo("video-1", "Video Camera", new List<string> { "test.mp4" });

        await using var source = CameraSourceFactory.Create(camera, "test.mp4");

        Assert.IsType<VideoFileSource>(source);
        Assert.Equal("Video Camera", source.SourceLabel);
    }

    [Fact]
    public async Task Create_ForUsbCameraProfile_ReturnsUsbCameraSource()
    {
        var device = new UsbCameraDevice("usb:/dev/video4", "USB Camera", "USB Camera", 4, "/dev/video4");
        var camera = MainWindow.CameraProfile.CreateUsbCamera(device);

        await using var source = CameraSourceFactory.Create(camera);

        Assert.IsType<UsbCameraSource>(source);
        Assert.Equal("USB Camera", source.SourceLabel);
    }
}
