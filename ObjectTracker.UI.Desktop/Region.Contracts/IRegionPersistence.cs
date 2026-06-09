using System.Collections.Generic;
using System.Threading.Tasks;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionPersistence
{
    ValueTask<IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition>> LoadAsync();
    ValueTask SaveAsync(IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> regions);
}
