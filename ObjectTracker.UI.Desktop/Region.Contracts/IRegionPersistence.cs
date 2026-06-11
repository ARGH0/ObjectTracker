using System.Collections.Generic;
using System.Threading.Tasks;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface IRegionPersistence
{
    ValueTask<IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition>> LoadAsync();
    ValueTask SaveAsync(IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> regions);
    ValueTask ExportAsync(CameraZoneId cameraZoneId, string filePath);
    ValueTask ImportAsync(string filePath, CameraZoneId targetCameraZoneId);
}
