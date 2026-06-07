using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneBindingStoreTests
{
    /// <summary>
    /// <description>Feature: CameraZoneBindingStore persists and reloads camera zones and source bindings correctly.
    /// 
    ///   Scenario: Saving two camera zones with their source bindings and reloading produces the same data.
    ///     Given an AppSettingsStore initialized with a temporary file path,
    ///      And Save is called with zones "zone-a" (Bridge) and "zone-b" (Yard), and bindings for "video-a.mp4" to "zone-a" and "usb:1" to "zone-b",
    ///     When Load() is called,
    ///     Then loaded.Zones.Count should be 2,
    ///      And loaded.Bindings.Count should be 2,
    ///      And a zone with CameraZoneId "zone-a" and Name "Bridge" should exist,
    ///      And a binding with SourceId "usb:1" and CameraZoneId "zone-b" should exist.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraZoneBindingStore returns an empty snapshot when the file is missing.
    /// 
    ///   Scenario: Loading from a non-existent path produces an empty zones and bindings collection.
    ///     Given a CameraZoneBindingStore initialized with a temporary file path that does not exist,
    ///     When Load() is called,
    ///     Then loaded.Zones should be empty,
    ///      And loaded.Bindings should be empty.</description>
    /// </summary>
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
