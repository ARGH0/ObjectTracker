using System;
using System.Collections.Generic;

namespace ObjectTracker.UI.Desktop;

public readonly record struct UsbResolutionPreset(int Width, int Height)
{
    public override string ToString() => $"{Width}x{Height}";
}

public sealed class UsbCaptureSettingsService
{
    private readonly Dictionary<string, UsbCaptureSettingsRequest> savedSettings;
    private readonly Dictionary<string, UsbCaptureSettingsRequest> draftSettings;

    public UsbCaptureSettingsService(IReadOnlyDictionary<string, UsbCaptureSettingsRequest> savedSettings)
    {
        this.savedSettings = new Dictionary<string, UsbCaptureSettingsRequest>(savedSettings, StringComparer.OrdinalIgnoreCase);
        draftSettings = new Dictionary<string, UsbCaptureSettingsRequest>(this.savedSettings, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<UsbResolutionPreset> ResolutionPresets { get; } =
    [
        new UsbResolutionPreset(640, 480),
        new UsbResolutionPreset(1280, 720),
        new UsbResolutionPreset(1920, 1080)
    ];

    public static IReadOnlyList<int> TargetFpsPresets { get; } = [30, 60];

    public UsbCaptureSettingsRequest GetRequestedSettings(string cameraSourceId)
    {
        return savedSettings.TryGetValue(cameraSourceId, out var settings)
            ? settings
            : UsbCaptureSettingsRequest.Default;
    }

    public UsbCaptureSettingsRequest GetDraftSettings(string cameraSourceId)
    {
        return draftSettings.TryGetValue(cameraSourceId, out var settings)
            ? settings
            : GetRequestedSettings(cameraSourceId);
    }

    public void UpdateDraft(string cameraSourceId, UsbCaptureSettingsRequest settings)
    {
        draftSettings[cameraSourceId] = settings;
    }

    public UsbCaptureSettingsApplyResult ApplyDraft(string cameraSourceId)
    {
        var settings = GetDraftSettings(cameraSourceId);
        savedSettings[cameraSourceId] = settings;
        return new UsbCaptureSettingsApplyResult(new Dictionary<string, UsbCaptureSettingsRequest>(savedSettings, StringComparer.OrdinalIgnoreCase));
    }

    public UsbCaptureSettingsRequest RevertDraft(string cameraSourceId)
    {
        var settings = GetRequestedSettings(cameraSourceId);
        draftSettings[cameraSourceId] = settings;
        return settings;
    }
}

public readonly record struct UsbCaptureSettingsApplyResult(IReadOnlyDictionary<string, UsbCaptureSettingsRequest> SettingsByCameraSourceId);
