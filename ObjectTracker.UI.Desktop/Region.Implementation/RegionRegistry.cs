using System;
using System.Collections.Generic;
using System.Linq;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionRegistry : ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry
{
    private readonly Dictionary<Guid, RegionDefinition> _regions = new();
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence _persistence;

    public RegionRegistry(ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence persistence)
    {
        _persistence = persistence;
        foreach (var region in _persistence.Load())
        {
            _regions[region.Id] = region;
        }
    }

    public IReadOnlyCollection<RegionDefinition> GetAll() => _regions.Values.ToList();
    public IReadOnlyCollection<RegionDefinition> GetByZone(CameraZoneId cameraZoneId) => _regions.Values.Where(r => r.CameraZoneId == cameraZoneId).ToList();
    public RegionDefinition? GetById(Guid regionId) => _regions.TryGetValue(regionId, out var region) ? region : null;

    public void Create(RegionDefinition region) => _regions[region.Id] = region;
    public void Update(RegionDefinition region) { if (_regions.ContainsKey(region.Id)) _regions[region.Id] = region; }
    public void Delete(Guid regionId) => _regions.Remove(regionId);
}
