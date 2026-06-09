using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionRegistry : ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry
{
    private readonly Dictionary<Guid, RegionDefinition> _regions = new();
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence _persistence;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    public event Action<RegionDefinition>? RegionCreated;
    public event Action<RegionDefinition>? RegionUpdated;
    public event Action<Guid>? RegionDeleted;

    public RegionRegistry(ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence persistence)
    {
        _persistence = persistence;
    }

    public static async ValueTask<RegionRegistry> CreateAsync(ObjectTracker.UI.Desktop.Region.Contracts.IRegionPersistence persistence)
    {
        var registry = new RegionRegistry(persistence);
        foreach (var region in await persistence.LoadAsync())
        {
            registry._regions[region.Id] = region;
        }
        return registry;
    }

    public IReadOnlyCollection<RegionDefinition> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return _regions.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IReadOnlyCollection<RegionDefinition> GetByZone(CameraZoneId cameraZoneId)
    {
        _lock.EnterReadLock();
        try
        {
            return _regions.Values
                .Where(r => r.CameraZoneId == cameraZoneId || (r.OverlappingZoneIds?.Contains(cameraZoneId.Value) == true))
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public RegionDefinition? GetById(Guid regionId)
    {
        _lock.EnterReadLock();
        try
        {
            return _regions.TryGetValue(regionId, out var region) ? region : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public RegionDefinition Create(RegionDefinition region)
    {
        _lock.EnterWriteLock();
        try
        {
            if (region.Id == Guid.Empty)
            {
                region = region with { Id = Guid.NewGuid() };
            }
            _regions[region.Id] = region;
            RegionCreated?.Invoke(region);
            return region;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Update(RegionDefinition region)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_regions.ContainsKey(region.Id))
            {
                _regions[region.Id] = region;
                RegionUpdated?.Invoke(region);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Delete(Guid regionId)
    {
        _lock.EnterWriteLock();
        try
        {
            var removed = _regions.Remove(regionId);
            if (removed)
            {
                RegionDeleted?.Invoke(regionId);
            }
            return removed;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
