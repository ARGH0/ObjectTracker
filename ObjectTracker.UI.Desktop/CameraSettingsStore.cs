using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ObjectTracker.Core.Domain;

namespace ObjectTracker.UI.Desktop;

internal sealed class CameraSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public CameraSettingsStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "camera-settings.json");
    }

    public Dictionary<string, MainWindow.RuntimeProcessingSettings> Load()
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, MainWindow.RuntimeProcessingSettings>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(filePath);
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
                ProcessMaxWidth: Math.Clamp(item.ProcessMaxWidth, 160, 1920),
                BakeSourceMode: ParseBakeSourceMode(item.BakeSourceMode),
                BakeImagePath: item.BakeImagePath ?? string.Empty,
                ColorCalibrations: ReadColorCalibrations(item.ColorCalibrations));
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
                ProcessMaxWidth = settings.ProcessMaxWidth,
                BakeSourceMode = settings.BakeSourceMode.ToString(),
                BakeImagePath = settings.BakeImagePath,
                ColorCalibrations = settings.ColorCalibrations
                    .Select(calibration => new ColorCalibrationDto
                    {
                        Name = calibration.Name,
                        HueLower = calibration.HueLower,
                        HueUpper = calibration.HueUpper,
                        SaturationLower = calibration.SaturationLower,
                        SaturationUpper = calibration.SaturationUpper,
                        ValueLower = calibration.ValueLower,
                        ValueUpper = calibration.ValueUpper
                    })
                    .ToList()
            });
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static int EnsureOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }

    private static MainWindow.BakeSourceMode ParseBakeSourceMode(string? value)
    {
        return Enum.TryParse<MainWindow.BakeSourceMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : MainWindow.BakeSourceMode.Samples;
    }

    private static IReadOnlyList<ColorCalibrationProfile> ReadColorCalibrations(List<ColorCalibrationDto>? dtos)
    {
        var defaults = MainWindow.CreateDefaultColorCalibrations()
            .ToDictionary(profile => profile.Name, profile => profile, StringComparer.OrdinalIgnoreCase);

        if (dtos is not null)
        {
            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    continue;
                }

                var normalizedName = dto.Name.Trim().ToUpperInvariant();
                defaults[normalizedName] = new ColorCalibrationProfile(
                    normalizedName,
                    Math.Clamp(dto.HueLower, 0, 180),
                    Math.Clamp(dto.HueUpper, 0, 180),
                    Math.Clamp(dto.SaturationLower, 0, 255),
                    Math.Clamp(dto.SaturationUpper, 0, 255),
                    Math.Clamp(dto.ValueLower, 0, 255),
                    Math.Clamp(dto.ValueUpper, 0, 255));
            }
        }

        return defaults.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToList();
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

        public string BakeSourceMode { get; set; } = nameof(MainWindow.BakeSourceMode.Samples);

        public string BakeImagePath { get; set; } = string.Empty;

        public List<ColorCalibrationDto> ColorCalibrations { get; set; } = new();
    }

    private sealed class ColorCalibrationDto
    {
        public string Name { get; set; } = string.Empty;

        public int HueLower { get; set; }

        public int HueUpper { get; set; }

        public int SaturationLower { get; set; }

        public int SaturationUpper { get; set; }

        public int ValueLower { get; set; }

        public int ValueUpper { get; set; }
    }
}
