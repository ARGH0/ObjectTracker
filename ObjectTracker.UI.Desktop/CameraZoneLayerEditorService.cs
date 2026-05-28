using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ObjectTracker.UI.Desktop;

public sealed class CameraZoneLayerEditorService
{
    public IReadOnlyList<CameraZoneLayer> AddLayer(
        IReadOnlyCollection<CameraZoneLayer> source,
        string cameraZoneId,
        string layerTypeId,
        string? name)
    {
        var layer = new CameraZoneLayer(
            LayerId: Guid.NewGuid().ToString("N"),
            CameraZoneId: cameraZoneId,
            LayerTypeId: layerTypeId,
            Name: string.IsNullOrWhiteSpace(name) ? "New Layer" : name.Trim(),
            Regions: new List<CameraZoneRegion>());

        return source.Concat(new[] { layer }).ToList();
    }

    public IReadOnlyList<CameraZoneLayer> RemoveLayer(IReadOnlyCollection<CameraZoneLayer> source, string layerId)
    {
        return source.Where(layer => !string.Equals(layer.LayerId, layerId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public IReadOnlyList<CameraZoneLayer> UpsertRegion(
        IReadOnlyCollection<CameraZoneLayer> source,
        string layerId,
        string? regionId,
        string name,
        int? code,
        string cellsText,
        AppSettings gridSettings)
    {
        var cells = ParseCells(cellsText, gridSettings);
        var resolvedRegionId = string.IsNullOrWhiteSpace(regionId) ? Guid.NewGuid().ToString("N") : regionId.Trim();

        var updated = new List<CameraZoneLayer>(source.Count);
        foreach (var layer in source)
        {
            if (!string.Equals(layer.LayerId, layerId, StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(layer);
                continue;
            }

            var nextRegions = layer.Regions
                .Where(region => !string.Equals(region.RegionId, resolvedRegionId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            nextRegions.Add(new CameraZoneRegion(
                RegionId: resolvedRegionId,
                Name: string.IsNullOrWhiteSpace(name) ? resolvedRegionId : name.Trim(),
                Code: code,
                Cells: cells));

            updated.Add(layer with { Regions = nextRegions });
        }

        return updated;
    }

    public IReadOnlyList<CameraZoneLayer> RemoveRegion(IReadOnlyCollection<CameraZoneLayer> source, string layerId, string regionId)
    {
        var updated = new List<CameraZoneLayer>(source.Count);
        foreach (var layer in source)
        {
            if (!string.Equals(layer.LayerId, layerId, StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(layer);
                continue;
            }

            var nextRegions = layer.Regions
                .Where(region => !string.Equals(region.RegionId, regionId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            updated.Add(layer with { Regions = nextRegions });
        }

        return updated;
    }

    public static string ToCellsText(IReadOnlyCollection<GridCell> cells)
    {
        return string.Join(";", cells.Select(cell => $"{cell.Column},{cell.Row}"));
    }

    public static IReadOnlyList<GridCell> ParseCells(string cellsText, AppSettings gridSettings)
    {
        var parts = (cellsText ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw new InvalidOperationException("At least one grid cell is required.");
        }

        var cells = new List<GridCell>();
        foreach (var part in parts)
        {
            var point = part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (point.Length != 2
                || !int.TryParse(point[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var column)
                || !int.TryParse(point[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
            {
                throw new InvalidOperationException($"Invalid grid cell '{part}'. Use 'column,row'.");
            }

            if (column < 0 || column >= gridSettings.GridColumns || row < 0 || row >= gridSettings.GridRows)
            {
                throw new InvalidOperationException($"Grid cell '{column},{row}' is outside configured grid {gridSettings.GridColumns}x{gridSettings.GridRows}.");
            }

            cells.Add(new GridCell(column, row));
        }

        return cells.Distinct().ToList();
    }
}
