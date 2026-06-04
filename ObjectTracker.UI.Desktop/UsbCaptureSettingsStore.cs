using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ObjectTracker.UI.Desktop;

public readonly record struct UsbCaptureSettingsRequest(int Width, int Height, int TargetFps)
{
    public static UsbCaptureSettingsRequest Default { get; } = new(640, 480, 20);
}

public sealed class UsbCaptureSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public UsbCaptureSettingsStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "usb-capture-settings.json");
    }

    public UsbCaptureSettingsStore(string filePath)
    {
        this.filePath = filePath;
    }

    public Dictionary<string, UsbCaptureSettingsRequest> Load()
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, UsbCaptureSettingsRequest>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<UsbCaptureSettingsFileDto>(json, JsonOptions);
        var result = new Dictionary<string, UsbCaptureSettingsRequest>(StringComparer.OrdinalIgnoreCase);
        if (dto?.Items is null)
        {
            return result;
        }

        foreach (var item in dto.Items)
        {
            if (string.IsNullOrWhiteSpace(item.CameraSourceId))
            {
                continue;
            }

            result[item.CameraSourceId] = new UsbCaptureSettingsRequest(item.Width, item.Height, item.TargetFps);
        }

        return result;
    }

    public void Save(IReadOnlyDictionary<string, UsbCaptureSettingsRequest> settingsByCameraSourceId)
    {
        var dto = new UsbCaptureSettingsFileDto();
        foreach (var (cameraSourceId, settings) in settingsByCameraSourceId)
        {
            dto.Items.Add(new UsbCaptureSettingsItemDto
            {
                CameraSourceId = cameraSourceId,
                Width = settings.Width,
                Height = settings.Height,
                TargetFps = settings.TargetFps
            });
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(dto, JsonOptions));
    }

    private sealed class UsbCaptureSettingsFileDto
    {
        public List<UsbCaptureSettingsItemDto> Items { get; set; } = new();
    }

    private sealed class UsbCaptureSettingsItemDto
    {
        public string CameraSourceId { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public int TargetFps { get; set; }
    }
}
