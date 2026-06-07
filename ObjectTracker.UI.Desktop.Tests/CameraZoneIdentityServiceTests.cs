using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneIdentityServiceTests
{
    /// <summary>
    /// <description>Feature: CameraZoneIdentityService.AssignSourceToZone auto-creates a camera zone and binding when the source is new.
    /// 
    ///   Scenario: Assigning a new video file source to a requested zone name that does not exist yet creates both a zone and a binding.
    ///     Given a CameraZoneIdentityService with a zone ID factory returning "zone-001",
    ///     When AssignSourceToZone("video-a.mp4", requestedZoneName: "North Loop") is called,
    ///     Then CreatedNewZone should be true,
    ///      And result.CameraZone.CameraZoneId should be "zone-001",
    ///      And result.CameraZone.Name should be "North Loop",
    ///      And result.Binding.SourceId should be "video-a.mp4",
    ///      And result.Binding.CameraZoneId should be "zone-001",
    ///      And service.CameraZones should contain exactly one entry,
    ///      And service.SourceBindings should contain exactly one entry.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraZoneIdentityService.AssignSourceToZone rebinds to an existing camera zone without creating a new one.
    /// 
    ///   Scenario: Assigning a source to a requested zone ID that already exists creates only a binding, not a new zone.
    ///     Given a CameraZoneIdentityService with an existing zone "zone-100" (Bridge Camera Zone) and an existing binding for "video-a.mp4",
    ///      And AssignSourceToZone("usb:1", requestedCameraZoneId: "zone-100") is called,
    ///     Then CreatedNewZone should be false,
    ///      And result.Binding.CameraZoneId should be "zone-100",
    ///      And result.Binding.SourceId should be "usb:1",
    ///      And service.CameraZones should contain exactly one entry,
    ///      And service.SourceBindings.Count should be 2.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraZoneIdentityService.AssignSourceToZone throws when the requested camera zone does not exist.
    /// 
    ///   Scenario: Assigning a source to a non-existent zone ID should fail with an appropriate error message.
    ///     Given a CameraZoneIdentityService with no existing zones,
    ///     When AssignSourceToZone("video-a.mp4", requestedCameraZoneId: "zone-missing") is called,
    ///     Then an InvalidOperationException should be thrown containing "does not exist".</description>
    /// </summary>
    [Fact]
    public void AssignSourceToZone_WhenRequestedCameraZoneMissing_Throws()
    {
        var service = new CameraZoneIdentityService();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.AssignSourceToZone("video-a.mp4", requestedCameraZoneId: "zone-missing"));

        Assert.Contains("does not exist", exception.Message, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// <description>Feature: CameraZoneIdentityService.AssignSourceToZone returns the existing binding when a source is already bound.
    /// 
    ///   Scenario: Assigning a source that is already bound to a zone should not create new zones or bindings.
    ///     Given a CameraZoneIdentityService with an existing zone "zone-200" (Yard) and an existing binding for "video-a.mp4",
    ///      And AssignSourceToZone("video-a.mp4") is called,
    ///     Then CreatedNewZone should be false,
    ///      And result.CameraZone.CameraZoneId should be "zone-200",
    ///      And result.Binding.CameraZoneId should be "zone-200",
    ///      And service.CameraZones should contain exactly one entry,
    ///      And service.SourceBindings should contain exactly one entry.</description>
    /// </summary>
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
