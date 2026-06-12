using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ObjectTracker.Core.Domain;

namespace ObjectTracker.UI.Desktop;

internal sealed class TrainStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public TrainStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "trains.json");
    }

    public TrainStore(string filePath)
    {
        this.filePath = filePath;
    }

    public IReadOnlyList<ConfiguredTrain> Load()
    {
        if (!File.Exists(filePath))
        {
            return CreateDefaultTrains();
        }

        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<TrainFileDto>(json, JsonOptions);
        if (dto?.Items is null)
        {
            return CreateDefaultTrains();
        }

        var trains = new List<ConfiguredTrain>();
        foreach (var item in dto.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            trains.Add(new ConfiguredTrain(
                item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                item.Name.Trim(),
                item.PlcId?.Trim() ?? string.Empty,
                item.MinColor,
                item.MaxColor,
                Math.Clamp(item.MaxWidth, 1, 100000),
                Math.Clamp(item.MaxHeight, 1, 100000),
                NormalizeCalibration(new ColorCalibrationProfile(
                    item.Name.Trim(),
                    item.HueLower,
                    item.HueUpper,
                    item.SaturationLower,
                    item.SaturationUpper,
                    item.ValueLower,
                    item.ValueUpper))));
        }

        return trains.Count == 0 ? CreateDefaultTrains() : trains;
    }

    public void Save(IReadOnlyList<ConfiguredTrain> trains)
    {
        var dto = new TrainFileDto
        {
            Items = trains
                .Where(train => !string.IsNullOrWhiteSpace(train.Name))
                .Select(train =>
                {
                    var calibration = NormalizeCalibration(train.Calibration);
                    return new TrainDto
                    {
                        Id = train.Id == Guid.Empty ? Guid.NewGuid() : train.Id,
                        Name = train.Name.Trim(),
                        PlcId = train.PlcId.Trim(),
                        MinColor = train.MinColor,
                        MaxColor = train.MaxColor,
                        MaxWidth = Math.Clamp(train.MaxWidth, 1, 100000),
                        MaxHeight = Math.Clamp(train.MaxHeight, 1, 100000),
                        HueLower = calibration.HueLower,
                        HueUpper = calibration.HueUpper,
                        SaturationLower = calibration.SaturationLower,
                        SaturationUpper = calibration.SaturationUpper,
                        ValueLower = calibration.ValueLower,
                        ValueUpper = calibration.ValueUpper
                    };
                })
                .ToList()
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static IReadOnlyList<ConfiguredTrain> CreateDefaultTrains()
    {
        return new List<ConfiguredTrain>
        {
            new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Red Train", "RED", 0xFF8A0000, 0xFFFF6060, 160, 80, new ColorCalibrationProfile("Red Train", 170, 10, 120, 255, 70, 255)),
            new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Green Train", "GREEN", 0xFF006A20, 0xFF70FF70, 160, 80, new ColorCalibrationProfile("Green Train", 35, 85, 80, 255, 60, 255)),
            new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "Blue Train", "BLUE", 0xFF003C8F, 0xFF60B0FF, 160, 80, new ColorCalibrationProfile("Blue Train", 90, 130, 100, 255, 60, 255)),
            new(Guid.Parse("10000000-0000-0000-0000-000000000004"), "White Train", "WHITE", 0xFFC8C8C8, 0xFFFFFFFF, 160, 80, new ColorCalibrationProfile("White Train", 0, 180, 0, 50, 190, 255))
        };
    }

    private static ColorCalibrationProfile NormalizeCalibration(ColorCalibrationProfile profile)
    {
        return new ColorCalibrationProfile(
            profile.Name.Trim(),
            Math.Clamp(profile.HueLower, 0, 180),
            Math.Clamp(profile.HueUpper, 0, 180),
            Math.Clamp(profile.SaturationLower, 0, 255),
            Math.Clamp(profile.SaturationUpper, 0, 255),
            Math.Clamp(profile.ValueLower, 0, 255),
            Math.Clamp(profile.ValueUpper, 0, 255));
    }

    private sealed class TrainFileDto
    {
        public List<TrainDto> Items { get; set; } = new();
    }

    private sealed class TrainDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string PlcId { get; set; } = string.Empty;

        public uint MinColor { get; set; }

        public uint MaxColor { get; set; }

        public int MaxWidth { get; set; } = 160;

        public int MaxHeight { get; set; } = 80;

        public int HueLower { get; set; }

        public int HueUpper { get; set; }

        public int SaturationLower { get; set; }

        public int SaturationUpper { get; set; }

        public int ValueLower { get; set; }

        public int ValueUpper { get; set; }
    }
}
