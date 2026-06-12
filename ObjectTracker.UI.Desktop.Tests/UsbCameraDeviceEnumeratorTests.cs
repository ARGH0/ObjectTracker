using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCameraDeviceEnumeratorTests
{
    [Fact]
    public void WithOperatorDisplayNames_WhenHardwareNamesRepeat_NumbersOnlyDuplicates()
    {
        var devices = new[]
        {
            new UsbCameraDevice("usb:index:0", "Logitech BRIO", "Logitech BRIO", 0, null),
            new UsbCameraDevice("usb:index:1", "Integrated Webcam", "Integrated Webcam", 1, null),
            new UsbCameraDevice("usb:index:2", "Logitech BRIO", "Logitech BRIO", 2, null)
        };

        var named = UsbCameraDeviceEnumerator.WithOperatorDisplayNames(devices);

        Assert.Collection(
            named,
            first => Assert.Equal("Logitech BRIO 1", first.DisplayName),
            second => Assert.Equal("Integrated Webcam", second.DisplayName),
            third => Assert.Equal("Logitech BRIO 2", third.DisplayName));
    }

    [Fact]
    public void EnumerateLinuxDevices_ReadsCameraNamesFromSysfs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ObjectTracker.UsbCameraDevices." + Guid.NewGuid());
        var devRoot = Path.Combine(tempRoot, "dev");
        var sysVideoRoot = Path.Combine(tempRoot, "sys", "class", "video4linux");

        try
        {
            Directory.CreateDirectory(devRoot);
            Directory.CreateDirectory(Path.Combine(sysVideoRoot, "video0"));
            File.WriteAllText(Path.Combine(devRoot, "video0"), string.Empty);
            File.WriteAllText(Path.Combine(sysVideoRoot, "video0", "name"), "HD Pro Webcam C920\n");

            var devices = UsbCameraDeviceEnumerator.EnumerateLinuxDevices(devRoot, sysVideoRoot);

            var device = Assert.Single(devices);
            Assert.Equal("usb:/dev/video0", device.Id);
            Assert.Equal("HD Pro Webcam C920", device.DisplayName);
            Assert.Equal("HD Pro Webcam C920", device.HardwareName);
            Assert.Equal(0, device.DeviceIndex);
            Assert.Equal("/dev/video0", device.DevicePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void EnumerateWindowsDevices_UsesProbeToDiscoverAvailableCameras()
    {
        var devices = UsbCameraDeviceEnumerator.EnumerateWindowsDevices(
            probeDevice: index => index is 0 or 2,
            candidateIndices: new[] { 0, 1, 2 });

        Assert.Collection(
            devices,
            first => Assert.Equal(0, first.DeviceIndex),
            second => Assert.Equal(2, second.DeviceIndex));
    }

    [Fact]
    public void Enumerate_WhenRunningOnLinux_ReturnsNamedLinuxDevices()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ObjectTracker.UsbCameraDevices." + Guid.NewGuid());
        var devRoot = Path.Combine(tempRoot, "dev");
        var sysVideoRoot = Path.Combine(tempRoot, "sys", "class", "video4linux");

        try
        {
            Directory.CreateDirectory(devRoot);
            Directory.CreateDirectory(Path.Combine(sysVideoRoot, "video3"));
            File.WriteAllText(Path.Combine(devRoot, "video3"), string.Empty);
            File.WriteAllText(Path.Combine(sysVideoRoot, "video3", "name"), "USB2.0 HD UVC WebCam");

            var devices = UsbCameraDeviceEnumerator.Enumerate(
                operatingSystem: UsbCameraOperatingSystem.Linux,
                linuxDevRoot: devRoot,
                linuxSysVideoRoot: sysVideoRoot);

            var device = Assert.Single(devices);
            Assert.Equal("USB2.0 HD UVC WebCam", device.DisplayName);
            Assert.Equal(3, device.DeviceIndex);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
