using System.Collections.Generic;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionPersistence
{
    IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> Load();
    void Save(IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> regions);
}
