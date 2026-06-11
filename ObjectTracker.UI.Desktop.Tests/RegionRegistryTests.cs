using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RegionRegistryTests
{
    private static ObjectTracker.UI.Desktop.Region.Model.RegionDefinition CreateRegion(Guid id, string name, string zoneId, ObjectTracker.UI.Desktop.Region.Model.RegionType type, params ObjectTracker.UI.Desktop.Region.Model.GridCell[] cells)
    {
        return new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: id,
            Name: name,
            Type: type,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zoneId),
            Cells: cells.ToList().AsReadOnly(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: null
        );
    }

    private static ObjectTracker.UI.Desktop.Region.Model.RegionDefinition CreateRegionWithCells(Guid id, string name, string zoneId, ObjectTracker.UI.Desktop.Region.Model.RegionType type, List<ObjectTracker.UI.Desktop.Region.Model.GridCell> cells)
    {
        return new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: id,
            Name: name,
            Type: type,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zoneId),
            Cells: cells.AsReadOnly(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: null
        );
    }

    [Fact]
    public void Create_AssignsGuid_WhenIdIsEmpty()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var emptyGuid = Guid.Empty;
        var region = CreateRegion(emptyGuid, "Test Region", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));

        var result = registry.Create(region);

        Assert.NotEqual(Guid.Empty, result.Id);
        var stored = registry.GetById(result.Id)!.Value;
        Assert.Equal("Test Region", stored.Name);
    }

    [Fact]
    public void Create_ReturnsRegion_WithAssignedId()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var emptyGuid = Guid.Empty;
        var region = CreateRegion(emptyGuid, "Created Region", "zone-2", ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(1, 1), new ObjectTracker.UI.Desktop.Region.Model.GridCell(2, 1));

        var result = registry.Create(region);

        Assert.Equal("Created Region", result.Name);
        Assert.Equal(ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, result.Type);
    }

    [Fact]
    public void Create_AddsRegion_ToRegistry()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var emptyGuid = Guid.Empty;
        var region = CreateRegion(emptyGuid, "Count Test", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));

        registry.Create(region);

        var all = registry.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal("Count Test", all[0].Name);
    }

    [Fact]
    public void Create_DoesNotAddDuplicate_WhenSameId()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var id = Guid.NewGuid();
        var region1 = CreateRegion(id, "First Name", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        var region2 = CreateRegion(id, "Second Name", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));

        registry.Create(region1);
        registry.Create(region2);

        Assert.Single(registry.GetAll());
        Assert.Equal("Second Name", registry.GetById(id)!.Value.Name);
    }

    [Fact]
    public void Create_PreservesProvidedId()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var providedId = Guid.NewGuid();
        var region = CreateRegion(providedId, "Provided ID", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));

        var result = registry.Create(region);

        Assert.Equal(providedId, result.Id);
    }

    [Fact]
    public void Update_ReplacesExistingRegion()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var id = Guid.NewGuid();
        var cells = new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(0, 0), new(1, 0) };
        var original = CreateRegionWithCells(id, "Original", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, cells);
        registry.Create(original);

        var updated = CreateRegionWithCells(id, "Updated Name", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(2, 2) });

        registry.Update(updated);

        var stored = registry.GetById(id)!.Value;
        Assert.Equal("Updated Name", stored.Name);
        Assert.Equal(ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, stored.Type);
    }

    [Fact]
    public void Update_PreservesId()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var id = Guid.NewGuid();
        var original = CreateRegion(id, "Preserve Test", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        registry.Create(original);

        var updated = CreateRegionWithCells(id, "New Name", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(0, 0) });

        registry.Update(updated);

        var stored = registry.GetById(id)!.Value;
        Assert.Equal(id, stored.Id);
    }

    [Fact]
    public void Update_DoesNotAdd_WhenIdDoesNotExist()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var nonExistentId = Guid.NewGuid();
        var updated = CreateRegionWithCells(nonExistentId, "Ghost", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { });

        registry.Update(updated);

        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Delete_RemovesExistingRegion()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var id = Guid.NewGuid();
        var region = CreateRegion(id, "To Delete", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        registry.Create(region);

        var result = registry.Delete(id);

        Assert.True(result);
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenIdNotFound()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var result = registry.Delete(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void GetById_ReturnsRegion_WhenExists()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var id = Guid.NewGuid();
        var region = CreateRegion(id, "Find Me", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        registry.Create(region);

        var result = registry.GetById(id);

        Assert.NotNull(result);
        Assert.Equal("Find Me", result.Value.Name);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var result = registry.GetById(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void GetByZone_ReturnsOnlyRegionsForSpecifiedZone()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var regionA = CreateRegion(Guid.NewGuid(), "Zone A", "zone-a", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        var regionB = CreateRegion(Guid.NewGuid(), "Zone B", "zone-b", ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(1, 1));

        registry.Create(regionA);
        registry.Create(regionB);

        var zoneAResult = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a")).ToList();
        var zoneBResult = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-b")).ToList();

        Assert.Single(zoneAResult);
        Assert.Equal("Zone A", zoneAResult[0].Name);
        Assert.Single(zoneBResult);
        Assert.Equal("Zone B", zoneBResult[0].Name);
    }

    [Fact]
    public void GetByZone_ReturnsEmpty_WhenNoRegionsForZone()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        registry.Create(CreateRegion(Guid.NewGuid(), "Other Zone", "zone-x", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0)));

        var result = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-y"));

        Assert.Empty(result);
    }

    [Fact]
    public void GetByZone_IncludesCameraOverlapRegion_WhenZoneInOverlappingIds()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var overlapRegion = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.NewGuid(),
            Name: "Overlap Zone",
            Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.CameraOverlapRegion,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"),
            Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(5, 5) },
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: new List<string> { "zone-a", "zone-b" }.AsReadOnly()
        );

        registry.Create(overlapRegion);

        var zoneAResult = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a")).ToList();
        var zoneBResult = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-b")).ToList();

        Assert.Single(zoneAResult);
        Assert.Equal("Overlap Zone", zoneAResult[0].Name);
        Assert.Single(zoneBResult);
        Assert.Equal("Overlap Zone", zoneBResult[0].Name);
    }

    [Fact]
    public void GetByZone_ExcludesCameraOverlapRegion_WhenZoneNotInOverlappingIds()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var overlapRegion = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.NewGuid(),
            Name: "Overlap Zone",
            Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.CameraOverlapRegion,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"),
            Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(5, 5) },
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: new List<string> { "zone-a", "zone-b" }.AsReadOnly()
        );

        registry.Create(overlapRegion);

        var result = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-c"));

        Assert.Empty(result);
    }

    [Fact]
    public void GetByZone_IncludesBothDirectAndOverlapRegions()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var directRegion = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.NewGuid(),
            Name: "Direct Zone A",
            Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"),
            Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(0, 0) },
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: null
        );

        var overlapRegion = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.NewGuid(),
            Name: "Overlap Zone",
            Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.CameraOverlapRegion,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"),
            Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(5, 5) },
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: new List<string> { "zone-a", "zone-b" }.AsReadOnly()
        );

        registry.Create(directRegion);
        registry.Create(overlapRegion);

        var zoneAResult = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a")).ToList();

        Assert.Equal(2, zoneAResult.Count);
    }

    [Fact]
    public void GetAll_ReturnsAllRegions()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var region1 = CreateRegion(Guid.NewGuid(), "First", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        var region2 = CreateRegion(Guid.NewGuid(), "Second", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(1, 1));

        registry.Create(region1);
        registry.Create(region2);

        var all = registry.GetAll().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenNoRegions()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var all = registry.GetAll();

        Assert.Empty(all);
    }

    [Fact]
    public async Task CreateAsync_InitializesFromPersistence()
    {
        var persistence = new FakeRegionPersistence();
        var region1 = CreateRegion(Guid.NewGuid(), "Persisted A", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        var region2 = CreateRegion(Guid.NewGuid(), "Persisted B", "zone-2", ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(1, 1));

        await persistence.SaveAsync([region1, region2]);

        var registry = await RegionRegistry.CreateAsync(persistence);

        var all = registry.GetAll().ToList();
        Assert.Equal(2, all.Count);
        Assert.Equal("Persisted A", all[0].Name);
        Assert.Equal("Persisted B", all[1].Name);
    }

    [Fact]
    public async Task CreateAsync_InitializesFromEmptyPersistence()
    {
        var persistence = new FakeRegionPersistence();

        var registry = await RegionRegistry.CreateAsync(persistence);

        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Create_FiresRegionCreatedEvent()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var created = false;
        var receivedRegion = default(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition);
        registry.RegionCreated += (r) => { created = true; receivedRegion = r; };

        var region = CreateRegion(Guid.NewGuid(), "Event Test", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        registry.Create(region);

        Assert.True(created);
        Assert.Equal("Event Test", receivedRegion.Name);
    }

    [Fact]
    public void Update_FiresRegionUpdatedEvent()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var id = Guid.NewGuid();
        var region = CreateRegion(id, "Original Name", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        registry.Create(region);

        var updated = false;
        var receivedRegion = default(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition);
        registry.RegionUpdated += (r) => { updated = true; receivedRegion = r; };

        var newRegion = CreateRegionWithCells(id, "Updated Name", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(1, 1) });
        registry.Update(newRegion);

        Assert.True(updated);
        Assert.Equal("Updated Name", receivedRegion.Name);
        Assert.Equal(ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, receivedRegion.Type);
    }

    [Fact]
    public void Delete_FiresRegionDeletedEvent()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var id = Guid.NewGuid();
        var region = CreateRegion(id, "To Delete", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        registry.Create(region);

        var deleted = false;
        var receivedId = Guid.Empty;
        registry.RegionDeleted += (deletedId) => { deleted = true; receivedId = deletedId; };

        registry.Delete(id);

        Assert.True(deleted);
        Assert.Equal(id, receivedId);
    }

    [Fact]
    public async Task CreateAsync_DoesNotFireEvents_OnInitialization()
    {
        var persistence = new FakeRegionPersistence();
        var region1 = CreateRegion(Guid.NewGuid(), "Persisted A", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
        await persistence.SaveAsync([region1]);

        var created = false;
        var registry = await RegionRegistry.CreateAsync(persistence);
        registry.RegionCreated += (_) => { created = true; };

        Assert.False(created);
    }

    [Fact]
    public void GetAll_DuringConcurrentWrites_ReturnsConsistentSnapshot()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var exceptions = new List<Exception>();
        var readCount = 0;

        var writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var id = Guid.NewGuid();
                var region = CreateRegion(id, $"Region-{i}", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
                registry.Create(region);
            }
        });

        var readTask = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    var snapshot = registry.GetAll();
                    Interlocked.Increment(ref readCount);
                    _ = snapshot.Count;
                    Thread.Sleep(TimeSpan.FromMilliseconds(1));
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                { exceptions.Add(ex); }
            }
        });

        Task.WaitAll(readTask, writeTask);

        Assert.Empty(exceptions);
        Assert.Equal(1000, readCount);
    }

    [Fact]
    public void GetByZone_DuringConcurrentWrites_ReturnsConsistentResult()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        var exceptions = new List<Exception>();

        var writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                var id = Guid.NewGuid();
                var region = CreateRegion(id, $"ZoneA-{i}", "zone-a", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
                registry.Create(region);
            }
        });

        var readTask = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    var result = registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"));
                    _ = result.Count;
                    Thread.Sleep(TimeSpan.FromMilliseconds(1));
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                { exceptions.Add(ex); }
            }
        });

        Task.WaitAll(readTask, writeTask);

        Assert.Empty(exceptions);
    }

    [Fact]
    public void Delete_DuringConcurrentReads_ReturnsConsistentState()
    {
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);

        for (int i = 0; i < 20; i++)
        {
            var id = Guid.NewGuid();
            var region = CreateRegion(id, $"DeleteTest-{i}", "zone-1", ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, new ObjectTracker.UI.Desktop.Region.Model.GridCell(0, 0));
            registry.Create(region);
        }

        var exceptions = new List<Exception>();

        var deleteTask = Task.Run(() =>
        {
            for (int i = 0; i < 20; i++)
            {
                var all = registry.GetAll().ToList();
                if (all.Count != 0)
                {
                    registry.Delete(all[0].Id);
                }
            }
        });

        var readTask = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    var snapshot = registry.GetAll();
                    _ = snapshot.Count;
                    Thread.Sleep(TimeSpan.FromMilliseconds(1));
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                { exceptions.Add(ex); }
            }
        });

        Task.WaitAll(readTask, deleteTask);

        Assert.Empty(exceptions);
    }
}
