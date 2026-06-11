using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionPersistence : ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence
{
    private readonly string _filePath;

    public RegionPersistence(string filePath)
    {
        _filePath = filePath;
    }

    public async ValueTask<IEnumerable<RegionDefinition>> LoadAsync()
    {
        if (!System.IO.File.Exists(_filePath))
        {
            return Enumerable.Empty<RegionDefinition>();
        }

        var json = await System.IO.File.ReadAllTextAsync(_filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        var wrapper = JsonSerializer.Deserialize<RegionWrapper>(json, options) ?? new RegionWrapper([]);
        return wrapper.Regions;
    }

    public async ValueTask SaveAsync(IEnumerable<RegionDefinition> regions)
    {
        var list = regions.ToList();
        var wrapper = new RegionWrapper(list);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(wrapper, options);
        var directory = System.IO.Path.GetDirectoryName(_filePath)!;
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        await System.IO.File.WriteAllTextAsync(_filePath, json);
    }

    public async ValueTask ExportAsync(CameraZoneId cameraZoneId, string filePath)
    {
        var allRegions = await LoadAsync();
        var zoneRegions = allRegions.Where(r => r.CameraZoneId.Equals(cameraZoneId)).ToList();
        
        var exportWrapper = new ExportWrapper(zoneRegions);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(exportWrapper, options);
        
        var directory = System.IO.Path.GetDirectoryName(filePath)!;
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        await System.IO.File.WriteAllTextAsync(filePath, json);
    }

    public async ValueTask ImportAsync(string filePath, CameraZoneId targetCameraZoneId)
    {
        if (!System.IO.File.Exists(filePath))
        {
            return;
        }

        var json = await System.IO.File.ReadAllTextAsync(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        
        var exportWrapper = JsonSerializer.Deserialize<ExportWrapper>(json, options);
        if (exportWrapper == null)
        {
            return;
        }

        var existingRegions = await LoadAsync();
        var updatedRegions = new List<RegionDefinition>();
        
        foreach (var region in existingRegions)
        {
            if (region.CameraZoneId.Equals(targetCameraZoneId))
            {
                var matchingImported = exportWrapper.Regions.FirstOrDefault(r => r.Name == region.Name);
                if (matchingImported != null)
                {
                    updatedRegions.Add(region with { Cells = matchingImported.Cells, UpdatedAt = System.DateTime.UtcNow });
                }
                else
                {
                    updatedRegions.Add(region);
                }
            }
            else
            {
                updatedRegions.Add(region);
            }
        }

        foreach (var importedRegion in exportWrapper.Regions)
        {
            var existing = updatedRegions.FirstOrDefault(r => r.CameraZoneId.Equals(targetCameraZoneId) && r.Name == importedRegion.Name);
            if (existing.Id == default)
            {
                var newRegion = new RegionDefinition(
                    Id: System.Guid.NewGuid(),
                    Name: importedRegion.Name,
                    Type: importedRegion.Type,
                    CameraZoneId: targetCameraZoneId,
                    Cells: importedRegion.Cells,
                    CreatedAt: System.DateTime.UtcNow,
                    UpdatedAt: System.DateTime.UtcNow,
                    OverlappingZoneIds: null
                );
                updatedRegions.Add(newRegion);
            }
        }

        await SaveAsync(updatedRegions);
    }

    private sealed record RegionWrapper(IList<RegionDefinition> Regions);
    
    private sealed class ExportWrapper
    {
        public List<ExportRegionData> Regions { get; set; } = new();
        
        public ExportWrapper()
        {
        }
        
        public ExportWrapper(IEnumerable<RegionDefinition> regions)
            : this()
        {
            Regions = regions.Select(r => new ExportRegionData(r.Name, r.Type, r.Cells.ToList())).ToList();
        }
    }

    private sealed class ExportRegionData
    {
        public string Name { get; set; } = "";
        public ObjectTracker.UI.Desktop.Region.Model.RegionType Type { get; set; }
        public List<ObjectTracker.UI.Desktop.Region.Model.GridCell> Cells { get; set; } = new();
        
        public ExportRegionData()
        {
        }
        
        public ExportRegionData(string name, ObjectTracker.UI.Desktop.Region.Model.RegionType type, IReadOnlyCollection<ObjectTracker.UI.Desktop.Region.Model.GridCell> cells)
        {
            Name = name;
            Type = type;
            Cells = cells.ToList();
        }
    }
}
