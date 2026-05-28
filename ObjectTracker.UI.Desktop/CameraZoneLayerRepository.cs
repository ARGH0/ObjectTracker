using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ObjectTracker.UI.Desktop;

public sealed class CameraZoneLayerRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public CameraZoneLayerRepository(string? filePathOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(filePathOverride))
        {
            filePath = filePathOverride;
            return;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFolder = Path.Combine(appDataPath, "ObjectTracker");
        filePath = Path.Combine(settingsFolder, "camera-zone-layers.json");
    }

    public IReadOnlyList<CameraZoneLayer> Load()
    {
        if (!File.Exists(filePath))
        {
            return new List<CameraZoneLayer>();
        }

        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<CameraZoneLayerFileDto>(json, JsonOptions);
        if (dto?.Layers is null)
        {
            return new List<CameraZoneLayer>();
        }

        var layers = new List<CameraZoneLayer>();
        foreach (var layer in dto.Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.LayerId)
                || string.IsNullOrWhiteSpace(layer.CameraZoneId)
                || string.IsNullOrWhiteSpace(layer.LayerTypeId))
            {
                continue;
            }

            var regions = new List<CameraZoneRegion>();
            foreach (var region in layer.Regions)
            {
                if (string.IsNullOrWhiteSpace(region.RegionId))
                {
                    continue;
                }

                var cells = region.Cells
                    .Select(cell => new GridCell(cell.Column, cell.Row))
                    .Distinct()
                    .ToList();

                regions.Add(new CameraZoneRegion(
                    region.RegionId.Trim(),
                    string.IsNullOrWhiteSpace(region.Name) ? region.RegionId.Trim() : region.Name.Trim(),
                    region.Code,
                    cells));
            }

            layers.Add(new CameraZoneLayer(
                layer.LayerId.Trim(),
                layer.CameraZoneId.Trim(),
                layer.LayerTypeId.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(layer.Name) ? layer.LayerId.Trim() : layer.Name.Trim(),
                regions));
        }

        return layers;
    }

    public void Save(IReadOnlyCollection<CameraZoneLayer> layers)
    {
        var dto = new CameraZoneLayerFileDto();
        foreach (var layer in layers)
        {
            if (string.IsNullOrWhiteSpace(layer.LayerId)
                || string.IsNullOrWhiteSpace(layer.CameraZoneId)
                || string.IsNullOrWhiteSpace(layer.LayerTypeId))
            {
                continue;
            }

            var layerDto = new CameraZoneLayerDto
            {
                LayerId = layer.LayerId.Trim(),
                CameraZoneId = layer.CameraZoneId.Trim(),
                LayerTypeId = layer.LayerTypeId.Trim().ToUpperInvariant(),
                Name = string.IsNullOrWhiteSpace(layer.Name) ? layer.LayerId.Trim() : layer.Name.Trim()
            };

            foreach (var region in layer.Regions)
            {
                if (string.IsNullOrWhiteSpace(region.RegionId))
                {
                    continue;
                }

                var regionDto = new CameraZoneRegionDto
                {
                    RegionId = region.RegionId.Trim(),
                    Name = string.IsNullOrWhiteSpace(region.Name) ? region.RegionId.Trim() : region.Name.Trim(),
                    Code = region.Code
                };

                foreach (var cell in region.Cells.Distinct())
                {
                    regionDto.Cells.Add(new GridCellDto { Column = cell.Column, Row = cell.Row });
                }

                layerDto.Regions.Add(regionDto);
            }

            dto.Layers.Add(layerDto);
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private sealed class CameraZoneLayerFileDto
    {
        public List<CameraZoneLayerDto> Layers { get; set; } = new();
    }

    private sealed class CameraZoneLayerDto
    {
        public string LayerId { get; set; } = string.Empty;

        public string CameraZoneId { get; set; } = string.Empty;

        public string LayerTypeId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<CameraZoneRegionDto> Regions { get; set; } = new();
    }

    private sealed class CameraZoneRegionDto
    {
        public string RegionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int? Code { get; set; }

        public List<GridCellDto> Cells { get; set; } = new();
    }

    private sealed class GridCellDto
    {
        public int Column { get; set; }

        public int Row { get; set; }
    }
}

public readonly record struct CameraZoneLayer(
    string LayerId,
    string CameraZoneId,
    string LayerTypeId,
    string Name,
    IReadOnlyCollection<CameraZoneRegion> Regions);

public readonly record struct CameraZoneRegion(
    string RegionId,
    string Name,
    int? Code,
    IReadOnlyCollection<GridCell> Cells);

public readonly record struct GridCell(int Column, int Row);
