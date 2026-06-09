using System;
using System.Collections.Generic;
using System.Linq;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class FakeRegionRegistry : ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry
{
    private readonly Dictionary<Guid, RegionDefinition> _regions = new();

    public event Action<RegionDefinition>? RegionCreated;
    public event Action<RegionDefinition>? RegionUpdated;
    public event Action<Guid>? RegionDeleted;

    public void Add(RegionDefinition region)
    {
        _regions[region.Id] = region;
    }

    public IReadOnlyCollection<RegionDefinition> GetAll() => _regions.Values.ToList();

    public IReadOnlyCollection<RegionDefinition> GetByZone(CameraZoneId cameraZoneId)
    {
        return _regions.Values
            .Where(r => r.CameraZoneId == cameraZoneId || (r.OverlappingZoneIds?.Contains(cameraZoneId.Value) == true))
            .ToList();
    }

    public RegionDefinition? GetById(Guid regionId) => _regions.TryGetValue(regionId, out var r) ? r : null;

    public RegionDefinition Create(RegionDefinition region)
    {
        if (region.Id == Guid.Empty) region = region with { Id = Guid.NewGuid() };
        _regions[region.Id] = region;
        RegionCreated?.Invoke(region);
        return region;
    }

    public void Update(RegionDefinition region)
    {
        if (_regions.ContainsKey(region.Id))
        {
            _regions[region.Id] = region;
            RegionUpdated?.Invoke(region);
        }
    }

    public bool Delete(Guid regionId)
    {
        var removed = _regions.Remove(regionId);
        if (removed) RegionDeleted?.Invoke(regionId);
        return removed;
    }
}
