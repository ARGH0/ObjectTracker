using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ObjectTracker.UI.Desktop;

public sealed class CameraZoneBindingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public CameraZoneBindingStore(string? filePathOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(filePathOverride))
        {
            filePath = filePathOverride;
            return;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "camera-zones.json");
    }

    public CameraZoneBindingSnapshot Load()
    {
        if (!File.Exists(filePath))
        {
            return new CameraZoneBindingSnapshot(new List<CameraZoneDefinition>(), new List<CameraZoneBinding>());
        }

        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<SnapshotDto>(json, JsonOptions);
        if (dto is null)
        {
            return new CameraZoneBindingSnapshot(new List<CameraZoneDefinition>(), new List<CameraZoneBinding>());
        }

        var zones = new List<CameraZoneDefinition>();
        foreach (var zone in dto.Zones)
        {
            if (string.IsNullOrWhiteSpace(zone.CameraZoneId))
            {
                continue;
            }

            zones.Add(new CameraZoneDefinition(zone.CameraZoneId.Trim(), zone.Name?.Trim() ?? string.Empty));
        }

        var bindings = new List<CameraZoneBinding>();
        foreach (var binding in dto.Bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.SourceId) || string.IsNullOrWhiteSpace(binding.CameraZoneId))
            {
                continue;
            }

            bindings.Add(new CameraZoneBinding(binding.SourceId.Trim(), binding.CameraZoneId.Trim()));
        }

        return new CameraZoneBindingSnapshot(zones, bindings);
    }

    public void Save(IReadOnlyCollection<CameraZoneDefinition> zones, IReadOnlyCollection<CameraZoneBinding> bindings)
    {
        var dto = new SnapshotDto();

        foreach (var zone in zones)
        {
            dto.Zones.Add(new ZoneDto { CameraZoneId = zone.CameraZoneId, Name = zone.Name });
        }

        foreach (var binding in bindings)
        {
            dto.Bindings.Add(new BindingDto { SourceId = binding.SourceId, CameraZoneId = binding.CameraZoneId });
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private sealed class SnapshotDto
    {
        public List<ZoneDto> Zones { get; set; } = new();

        public List<BindingDto> Bindings { get; set; } = new();
    }

    private sealed class ZoneDto
    {
        public string CameraZoneId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class BindingDto
    {
        public string SourceId { get; set; } = string.Empty;

        public string CameraZoneId { get; set; } = string.Empty;
    }
}

public readonly record struct CameraZoneBindingSnapshot(
    IReadOnlyCollection<CameraZoneDefinition> Zones,
    IReadOnlyCollection<CameraZoneBinding> Bindings);
