using System.Collections.Generic;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionManagerService : ObjectTracker.UI.Desktop.Region.Contracts.IRegionManagerService
{
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry _registry;

    public RegionManagerService(ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyCollection<RegionDefinition> GetRegionsForZone(CameraZoneId cameraZoneId) => _registry.GetByZone(cameraZoneId);
    public void OpenGridEditor(CameraZoneId cameraZoneId) { /* TODO: open dialog */ }
}
