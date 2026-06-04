using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCaptureSettingsServiceTests
{
    [Fact]
    public void GetSettings_WhenSourceHasNoSavedRequest_ReturnsDefaultCaptureSettings()
    {
        var service = new UsbCaptureSettingsService(new Dictionary<string, UsbCaptureSettingsRequest>());

        var settings = service.GetRequestedSettings("usb:0:ANY");

        Assert.Equal(new UsbCaptureSettingsRequest(640, 480, 30), settings);
    }

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

    [Fact]
    public void ApplyDraft_PersistsRequestedSettingsForCameraSource()
    {
        var service = new UsbCaptureSettingsService(new Dictionary<string, UsbCaptureSettingsRequest>());

        service.UpdateDraft("usb:0:ANY", new UsbCaptureSettingsRequest(1280, 720, 60));
        var result = service.ApplyDraft("usb:0:ANY");

        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), result.SettingsByCameraSourceId["usb:0:ANY"]);
        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), service.GetRequestedSettings("usb:0:ANY"));
    }

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
