using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCaptureSettingsServiceTests
{
    /// <summary>
    /// <description>Feature: UsbCaptureSettingsService.GetRequestedSettings returns default capture settings when a source has no saved request.
    /// 
    ///   Scenario: Getting requested settings for "usb:0:ANY" from an empty dictionary should return the default 640x480@30.
    ///     Given a UsbCaptureSettingsService initialized with an empty Dictionary,
    ///     When GetRequestedSettings("usb:0:ANY") is called,
    ///     Then the result should be new UsbCaptureSettingsRequest(640, 480, 30).</description>
    /// </summary>
    [Fact]
    public void GetSettings_WhenSourceHasNoSavedRequest_ReturnsDefaultCaptureSettings()
    {
        var service = new UsbCaptureSettingsService(new Dictionary<string, UsbCaptureSettingsRequest>());

        var settings = service.GetRequestedSettings("usb:0:ANY");

        Assert.Equal(new UsbCaptureSettingsRequest(640, 480, 30), settings);
    }

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsService exposes supported resolution and FPS presets.
    /// 
    ///   Scenario: The ResolutionPresets should contain 640x480, 1280x720, and 1920x1080, and TargetFpsPresets should contain 30 and 60.
    ///     Given UsbCaptureSettingsService.ResolutionPresets and UsbCaptureSettingsService.TargetFpsPresets are accessed,
    ///     Then ResolutionPresets should equal [640x480, 1280x720, 1920x1080] and TargetFpsPresets should equal [30, 60].</description>
    /// </summary>
    [Fact]
    public void Presets_ExposeSupportedResolutionAndFpsChoices()
    {
        Assert.Equal(new[]
        {
            new UsbResolutionPreset(640, 480),
            new UsbResolutionPreset(1280, 720),
            new UsbResolutionPreset(1920, 1080)
        }, UsbCaptureSettingsService.ResolutionPresets);
        Assert.Equal(new[] { 30, 60 }, UsbCaptureSettingsService.TargetFpsPresets);
    }

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsService.ApplyDraft persists requested settings for a camera source.
    /// 
    ///   Scenario: Updating a draft to 1280x720@60 and then applying it should persist those settings and make them available via GetRequestedSettings.
    ///     Given a UsbCaptureSettingsService initialized with an empty Dictionary,
    ///      And UpdateDraft("usb:0:ANY", 1280x720@60) is called,
    ///      And ApplyDraft("usb:0:ANY") is called,
    ///     Then result.SettingsByCameraSourceId["usb:0:ANY"] should be 1280x720@60 and GetRequestedSettings("usb:0:ANY") should also be 1280x720@60.</description>
    /// </summary>
    [Fact]
    public void ApplyDraft_PersistsRequestedSettingsForCameraSource()
    {
        var service = new UsbCaptureSettingsService(new Dictionary<string, UsbCaptureSettingsRequest>());

        service.UpdateDraft("usb:0:ANY", new UsbCaptureSettingsRequest(1280, 720, 60));
        var result = service.ApplyDraft("usb:0:ANY");

        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), result.SettingsByCameraSourceId["usb:0:ANY"]);
        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), service.GetRequestedSettings("usb:0:ANY"));
    }

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsService.RevertDraft restores persisted requested settings.
    /// 
    ///   Scenario: Updating a draft to 1920x1080@60 and then reverting it should restore the previously persisted value of 640x480@30.
    ///     Given a UsbCaptureSettingsService initialized with ["usb:0:ANY"] = 640x480@30,
    ///      And UpdateDraft("usb:0:ANY", 1920x1080@60) is called,
    ///      And RevertDraft("usb:0:ANY") is called,
    ///     Then the reverted result should be 640x480@30 and GetDraftSettings("usb:0:ANY") should also be 640x480@30.</description>
    /// </summary>
    [Fact]
    public void RevertDraft_RestoresPersistedRequestedSettings()
    {
        var service = new UsbCaptureSettingsService(new Dictionary<string, UsbCaptureSettingsRequest>
        {
            ["usb:0:ANY"] = new(640, 480, 30)
        });

        service.UpdateDraft("usb:0:ANY", new UsbCaptureSettingsRequest(1920, 1080, 60));
        var reverted = service.RevertDraft("usb:0:ANY");

        Assert.Equal(new UsbCaptureSettingsRequest(640, 480, 30), reverted);
        Assert.Equal(new UsbCaptureSettingsRequest(640, 480, 30), service.GetDraftSettings("usb:0:ANY"));
    }
}
