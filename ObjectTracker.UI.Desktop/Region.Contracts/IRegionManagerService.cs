using System.Collections.Generic;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionManagerService
{
    IReadOnlyCollection<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> GetRegionsForZone(CameraZoneId cameraZoneId);
    void OpenGridEditor(CameraZoneId cameraZoneId);
}
