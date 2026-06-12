using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionManagerService : ObjectTracker.UI.Desktop.Region.Contracts.IRegionManagerService
{
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry _registry;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence _persistence;
    private readonly Func<Window, Guid?, Task<ObjectTracker.UI.Desktop.Region.Implementation.GridEditorDialog?>>? _dialogFactory;

    public RegionManagerService(
        ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry registry,
        ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence persistence = null,
        Func<Window, Guid?, Task<ObjectTracker.UI.Desktop.Region.Implementation.GridEditorDialog?>>? dialogFactory = null)
    {
        _registry = registry;
        _persistence = persistence;
        _dialogFactory = dialogFactory;
    }

    public IReadOnlyCollection<RegionDefinition> GetRegionsForZone(CameraZoneId cameraZoneId) => _registry.GetByZone(cameraZoneId);

    public async Task OpenGridEditorAsync(CameraZoneId cameraZoneId, Window owner, Guid? regionId = null, string name = "New Region", ObjectTracker.UI.Desktop.Region.Model.RegionType regionType = ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion)
    {
        if (_dialogFactory == null)
            return;

        var dialog = await _dialogFactory(owner, regionId);
        if (dialog == null)
            return;

        var result = await dialog.ShowDialog<ObjectTracker.UI.Desktop.Region.Implementation.GridEditorResult?>(owner);
        if (result == null || !result.Value.HasSelection)
            return;

        var cells = result.Value.Cells.ToList().AsReadOnly();

        if (regionId.HasValue)
        {
            var existingRegion = _registry.GetById(regionId.Value);
            if (existingRegion is not null)
            {
                var original = (RegionDefinition)existingRegion;
                var updated = new RegionDefinition(
                    Id: original.Id,
                    Name: original.Name,
                    Type: original.Type,
                    CameraZoneId: original.CameraZoneId,
                    Cells: cells,
                    CreatedAt: original.CreatedAt,
                    UpdatedAt: DateTime.UtcNow,
                    OverlappingZoneIds: original.OverlappingZoneIds
                );
                _registry.Update(updated);
            }
        }
        else
        {
            var newRegion = new RegionDefinition(
                Id: Guid.Empty,
                Name: name,
                Type: regionType,
                CameraZoneId: cameraZoneId,
                Cells: cells,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                OverlappingZoneIds: null
            );
            _registry.Create(newRegion);
        }

        if (_persistence != null)
        {
            await _persistence.SaveAsync(_registry.GetAll());
        }
    }

    public async Task DeleteRegionAsync(Guid regionId)
    {
        var deleted = _registry.Delete(regionId);
        if (deleted && _persistence != null)
        {
            await _persistence.SaveAsync(_registry.GetAll());
        }
    }

    public async Task ExportZoneRegionsAsync(CameraZoneId cameraZoneId, string filePath)
    {
        if (_persistence == null)
            return;

        await _persistence.ExportAsync(cameraZoneId, filePath);
    }

    public async Task ImportZoneRegionsAsync(CameraZoneId cameraZoneId, string filePath)
    {
        if (_persistence == null)
            return;

        await _persistence.ImportAsync(filePath, cameraZoneId);

        var importedRegions = (await _persistence.LoadAsync())
            .Where(region => region.CameraZoneId.Equals(cameraZoneId))
            .ToList();

        foreach (var importedRegion in importedRegions)
        {
            var existingRegion = _registry.GetById(importedRegion.Id);
            if (existingRegion is null)
            {
                _registry.Create(importedRegion);
            }
            else
            {
                _registry.Update(importedRegion);
            }
        }
    }

    public async Task ExportCameraRegionsAsync(CameraZoneId cameraZoneId, string filePath)
    {
        if (_persistence == null)
            return;

        await _persistence.ExportCameraRegionsAsync(cameraZoneId, filePath);
    }

    public async Task<int> ImportCameraRegionsAsync(CameraZoneId cameraZoneId, string filePath)
    {
        if (_persistence == null)
            return 0;

        var importedRegions = await _persistence.ImportCameraRegionsAsync(filePath, cameraZoneId);
        foreach (var importedRegion in importedRegions)
        {
            _registry.Create(importedRegion);
        }

        return importedRegions.Count;
    }
}
