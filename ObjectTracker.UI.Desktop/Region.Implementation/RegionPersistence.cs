using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

    private sealed record RegionWrapper(IList<RegionDefinition> Regions);
}
