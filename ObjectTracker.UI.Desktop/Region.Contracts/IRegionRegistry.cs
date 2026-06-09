using System.Collections.Generic;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionRegistry
{
    IReadOnlyCollection<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> GetAll();
    IReadOnlyCollection<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> GetByZone(CameraZoneId cameraZoneId);
    ObjectTracker.UI.Desktop.Region.Model.RegionDefinition? GetById(System.Guid regionId);
    void Create(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition region);
    void Update(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition region);
    void Delete(System.Guid regionId);
}
