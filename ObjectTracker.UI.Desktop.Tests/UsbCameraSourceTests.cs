using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCameraSourceTests
{
    [Fact]
    public async Task ReadLatestFrame_WhenDeviceCannotOpen_ReturnsNull()
    {
        var source = new UsbCameraSource(9999, "usb-test");

        Assert.Equal("usb-test", source.SourceLabel);
        Assert.Null(source.ReadLatestFrame());

        await source.DisposeAsync();

        Assert.Null(source.ReadLatestFrame());
    }
}
