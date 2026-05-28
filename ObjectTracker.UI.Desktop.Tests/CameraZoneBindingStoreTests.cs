using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneBindingStoreTests
{
    [Fact]
    public void SaveThenLoad_PreservesZonesAndBindings()
    {
        var path = BuildTempFilePath();
        var store = new CameraZoneBindingStore(path);

        store.Save(
            zones: new[]
            {
                new CameraZoneDefinition("zone-a", "Bridge"),
                new CameraZoneDefinition("zone-b", "Yard")
            },
            bindings: new[]
            {
                new CameraZoneBinding("video-a.mp4", "zone-a"),
                new CameraZoneBinding("usb:1", "zone-b")
            });

        var loaded = store.Load();

        Assert.Equal(2, loaded.Zones.Count);
        Assert.Equal(2, loaded.Bindings.Count);
        Assert.Contains(loaded.Zones, zone => zone.CameraZoneId == "zone-a" && zone.Name == "Bridge");
        Assert.Contains(loaded.Bindings, binding => binding.SourceId == "usb:1" && binding.CameraZoneId == "zone-b");
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptySnapshot()
    {
        var store = new CameraZoneBindingStore(BuildTempFilePath());

        var loaded = store.Load();

        Assert.Empty(loaded.Zones);
        Assert.Empty(loaded.Bindings);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "camera-zones.json");
    }
}
