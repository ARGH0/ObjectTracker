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
    Task OpenGridEditorAsync(CameraZoneId cameraZoneId, Window owner, Guid? regionId = null, string name = "New Region", ObjectTracker.UI.Desktop.Region.Model.RegionType regionType = ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion);
    Task DeleteRegionAsync(Guid regionId);
    Task ExportZoneRegionsAsync(CameraZoneId cameraZoneId, string filePath);
    Task ImportZoneRegionsAsync(CameraZoneId cameraZoneId, string filePath);
}
