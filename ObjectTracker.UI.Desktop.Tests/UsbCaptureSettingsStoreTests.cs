using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCaptureSettingsStoreTests
{
    [Fact]
    public void SaveThenLoad_PreservesPerCameraSourceRequestedSettings()
    {
        var path = BuildTempFilePath();
        var store = new UsbCaptureSettingsStore(path);

        store.Save(new Dictionary<string, UsbCaptureSettingsRequest>(StringComparer.OrdinalIgnoreCase)
        {
            ["usb:0:ANY"] = new(1280, 720, 60),
            ["usb:1:ANY"] = new(640, 480, 30)
        });

        var loaded = store.Load();

        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), loaded["usb:0:ANY"]);
        Assert.Equal(new UsbCaptureSettingsRequest(640, 480, 30), loaded["usb:1:ANY"]);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "usb-capture-settings.json");
    }
}
