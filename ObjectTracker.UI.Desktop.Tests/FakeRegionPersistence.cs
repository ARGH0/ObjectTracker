using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Model;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class FakeRegionPersistence : IRegionPersistence
{
    private readonly List<RegionDefinition> _regions = new();

    public ValueTask<IEnumerable<RegionDefinition>> LoadAsync() => ValueTask.FromResult<IEnumerable<RegionDefinition>>(_regions);

    public ValueTask SaveAsync(IEnumerable<RegionDefinition> regions)
    {
        _regions.Clear();
        _regions.AddRange(regions);
        return ValueTask.CompletedTask;
    }
}
