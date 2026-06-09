using System;
using System.Collections.Generic;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionRegistry
{
    event Action<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> RegionCreated;
    event Action<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> RegionUpdated;
    event Action<System.Guid> RegionDeleted;

    IReadOnlyCollection<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> GetAll();
    IReadOnlyCollection<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> GetByZone(CameraZoneId cameraZoneId);
    ObjectTracker.UI.Desktop.Region.Model.RegionDefinition? GetById(System.Guid regionId);
    ObjectTracker.UI.Desktop.Region.Model.RegionDefinition Create(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition region);
    void Update(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition region);
    bool Delete(System.Guid regionId);
}
