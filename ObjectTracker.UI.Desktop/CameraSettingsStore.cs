using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ObjectTracker.UI.Desktop;

internal sealed class CameraSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public CameraSettingsStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        _filePath = Path.Combine(settingsFolder, "camera-settings.json");
    }

    public Dictionary<string, MainWindow.RuntimeProcessingSettings> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, MainWindow.RuntimeProcessingSettings>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(_filePath);
        var dto = JsonSerializer.Deserialize<CameraSettingsFileDto>(json, JsonOptions);
        if (dto?.Items is null)
        {
            return new Dictionary<string, MainWindow.RuntimeProcessingSettings>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, MainWindow.RuntimeProcessingSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in dto.Items)
        {
            if (string.IsNullOrWhiteSpace(item.CameraId))
            {
                continue;
            }

            result[item.CameraId] = new MainWindow.RuntimeProcessingSettings(
                SampleCount: Math.Clamp(item.SampleCount, 5, 200),
                Threshold: Math.Clamp(item.Threshold, 1, 255),
                MotionArea: Math.Clamp(item.MotionArea, 20, 100000),
                ColorMinPixels: Math.Clamp(item.ColorMinPixels, 1, 100000),
                MorphKernelSize: EnsureOdd(Math.Clamp(item.MorphKernelSize, 1, 31)),
                ProcessMaxWidth: Math.Clamp(item.ProcessMaxWidth, 160, 1920));
        }

        return result;
    }

    public void Save(IReadOnlyDictionary<string, MainWindow.RuntimeProcessingSettings> settingsByCameraId)
    {
        var dto = new CameraSettingsFileDto
        {
            Items = new List<CameraSettingsItemDto>()
        };

        foreach (var (cameraId, settings) in settingsByCameraId)
        {
            dto.Items.Add(new CameraSettingsItemDto
            {
                CameraId = cameraId,
                SampleCount = settings.SampleCount,
                Threshold = settings.Threshold,
                MotionArea = settings.MotionArea,
                ColorMinPixels = settings.ColorMinPixels,
                MorphKernelSize = settings.MorphKernelSize,
                ProcessMaxWidth = settings.ProcessMaxWidth
            });
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private static int EnsureOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }

    private sealed class CameraSettingsFileDto
    {
        public List<CameraSettingsItemDto> Items { get; set; } = new();
    }

    private sealed class CameraSettingsItemDto
    {
        public string CameraId { get; set; } = string.Empty;
        public int SampleCount { get; set; } = 20;
        public int Threshold { get; set; } = 100;
        public int MotionArea { get; set; } = 220;
        public int ColorMinPixels { get; set; } = 40;
        public int MorphKernelSize { get; set; } = 3;
        public int ProcessMaxWidth { get; set; } = 640;
    }
}
