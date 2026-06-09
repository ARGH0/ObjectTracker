using System.Collections.Generic;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionPriorityResolver
{
    RegionType Resolve(IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> overlappingRegions);
    IEnumerable<(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition Region, RegionType Type)> ResolveAll(IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> overlappingRegions);
}
