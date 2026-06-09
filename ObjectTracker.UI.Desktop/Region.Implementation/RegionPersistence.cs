using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionPersistence : ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence
{
    private readonly string _filePath;

    public RegionPersistence(string filePath)
    {
        _filePath = filePath;
    }

    public IEnumerable<RegionDefinition> Load()
    {
        if (!System.IO.File.Exists(_filePath))
        {
            return Enumerable.Empty<RegionDefinition>();
        }

        var json = System.IO.File.ReadAllText(_filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var wrapper = JsonSerializer.Deserialize<RegionWrapper>(json, options) ?? new RegionWrapper([]);
        return wrapper.Regions;
    }

    public void Save(IEnumerable<RegionDefinition> regions)
    {
        var wrapper = new RegionWrapper(regions.ToList());
        var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(_filePath, json);
    }

    private sealed record RegionWrapper(IList<RegionDefinition> Regions);
}
