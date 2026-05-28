using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneIdentityServiceTests
{
    [Fact]
    public void AssignSourceToZone_WhenSourceIsNew_AutoCreatesCameraZoneAndBinding()
    {
        var service = new CameraZoneIdentityService(zoneIdFactory: () => "zone-001");

        var result = service.AssignSourceToZone("video-a.mp4", requestedZoneName: "North Loop");

        Assert.True(result.CreatedNewZone);
        Assert.Equal("zone-001", result.CameraZone.CameraZoneId);
        Assert.Equal("North Loop", result.CameraZone.Name);
        Assert.Equal("video-a.mp4", result.Binding.SourceId);
        Assert.Equal("zone-001", result.Binding.CameraZoneId);
        Assert.Single(service.CameraZones);
        Assert.Single(service.SourceBindings);
    }

    [Fact]
    public void AssignSourceToZone_WhenRequestedCameraZoneExists_RebindsWithoutCreatingZone()
    {
        var existingZone = new CameraZoneDefinition("zone-100", "Bridge Camera Zone");
        var service = new CameraZoneIdentityService(
            existingZones: new[] { existingZone },
            existingBindings: new[] { new CameraZoneBinding("video-a.mp4", "zone-100") },
            zoneIdFactory: () => "zone-999");

        var result = service.AssignSourceToZone("usb:1", requestedCameraZoneId: "zone-100");

        Assert.False(result.CreatedNewZone);
        Assert.Equal("zone-100", result.Binding.CameraZoneId);
        Assert.Equal("usb:1", result.Binding.SourceId);
        Assert.Single(service.CameraZones);
        Assert.Equal(2, service.SourceBindings.Count);
    }

    [Fact]
    public void AssignSourceToZone_WhenRequestedCameraZoneMissing_Throws()
    {
        var service = new CameraZoneIdentityService();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.AssignSourceToZone("video-a.mp4", requestedCameraZoneId: "zone-missing"));

        Assert.Contains("does not exist", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AssignSourceToZone_WhenSourceAlreadyBound_ReturnsExistingBinding()
    {
        var service = new CameraZoneIdentityService(
            existingZones: new[] { new CameraZoneDefinition("zone-200", "Yard") },
            existingBindings: new[] { new CameraZoneBinding("video-a.mp4", "zone-200") },
            zoneIdFactory: () => "zone-new");

        var result = service.AssignSourceToZone("video-a.mp4");

        Assert.False(result.CreatedNewZone);
        Assert.Equal("zone-200", result.CameraZone.CameraZoneId);
        Assert.Equal("zone-200", result.Binding.CameraZoneId);
        Assert.Single(service.CameraZones);
        Assert.Single(service.SourceBindings);
    }
}
