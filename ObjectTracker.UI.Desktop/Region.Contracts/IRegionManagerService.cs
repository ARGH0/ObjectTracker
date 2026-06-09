using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionManagerService
{
    IReadOnlyCollection<RegionDefinition> GetRegionsForZone(CameraZoneId cameraZoneId);
    Task OpenGridEditorAsync(CameraZoneId cameraZoneId, Window owner, Guid? regionId = null);
    bool DeleteRegion(Guid regionId);
}
