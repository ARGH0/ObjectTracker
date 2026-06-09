using System.Collections.Generic;
using System.Linq;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionPriorityResolver : ObjectTracker.UI.Desktop.Region.Contracts.IRegionPriorityResolver
{
    public RegionType Resolve(IEnumerable<RegionDefinition> overlappingRegions)
    {
        var sorted = overlappingRegions.OrderBy(r => (int)r.Type).ToList();
        return sorted.Count > 0 ? sorted.First().Type : RegionType.CameraOverlapRegion;
    }

    public IEnumerable<(RegionDefinition Region, RegionType Type)> ResolveAll(IEnumerable<RegionDefinition> overlappingRegions)
    {
        return overlappingRegions.OrderBy(r => (int)r.Type).Select(r => (r, r.Type));
    }
}
