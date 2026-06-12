using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;
using Xunit;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RegionManagerServiceTests
{
    [Fact]
    public void GetRegionsForZone_ReturnsOnlyRegionsForThatZone()
    {
        // Arrange
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);
        var service = new RegionManagerService(registry);

        var zoneA = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a");
        var zoneB = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-b");

        registry.Create(new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.NewGuid(), Name: "Region A", Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
            CameraZoneId: zoneA, Cells: new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(0, 0) },
            CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow, OverlappingZoneIds: null));

        registry.Create(new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.NewGuid(), Name: "Region B", Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion,
            CameraZoneId: zoneB, Cells: new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(1, 1) },
            CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow, OverlappingZoneIds: null));

        // Act
        var zoneAResult = service.GetRegionsForZone(zoneA);
        var zoneBResult = service.GetRegionsForZone(zoneB);

        // Assert
        Assert.Single(zoneAResult);
        Assert.Single(zoneBResult);
        Assert.Equal("Region A", zoneAResult.First().Name);
        Assert.Equal("Region B", zoneBResult.First().Name);
    }

    [Fact]
    public void DeleteRegion_RemovesFromRegistry()
    {
        // Arrange
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);
        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-1");

        var id = Guid.NewGuid();
        registry.Create(new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: id, Name: "To Delete", Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
            CameraZoneId: zoneId, Cells: new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(0, 0) },
            CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow, OverlappingZoneIds: null));

        // Act
        var deleted = registry.Delete(id);

        // Assert
        Assert.True(deleted);
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void DeleteRegion_FiresRegionDeletedEvent()
    {
        // Arrange
        var fakePersistence = new FakeRegionPersistence();
        var registry = new RegionRegistry(fakePersistence);
        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-1");

        var id = Guid.NewGuid();
        registry.Create(new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: id, Name: "Delete Event", Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
            CameraZoneId: zoneId, Cells: new List<ObjectTracker.UI.Desktop.Region.Model.GridCell> { new(0, 0) },
            CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow, OverlappingZoneIds: null));

        var deletedFired = false;
        var receivedId = Guid.Empty;
        registry.RegionDeleted += r => { deletedFired = true; receivedId = r; };

        // Act
        registry.Delete(id);

        // Assert
        Assert.True(deletedFired);
        Assert.Equal(id, receivedId);
    }

    [Fact]
    public async Task ImportZoneRegionsAsync_AddsImportedRegionsToRegistry()
    {
        var storagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"objecttracker-regions-{Guid.NewGuid()}.json");
        var importPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"objecttracker-import-{Guid.NewGuid()}.json");

        try
        {
            var importData = new
            {
                regions = new[]
                {
                    new
                    {
                        Name = "Imported Region",
                        Type = 10,
                        Cells = new[] { new { Column = 2, Row = 3 } }
                    }
                }
            };

            await System.IO.File.WriteAllTextAsync(
                importPath,
                System.Text.Json.JsonSerializer.Serialize(importData));

            var persistence = new RegionPersistence(storagePath);
            var registry = new RegionRegistry(persistence);
            var service = new RegionManagerService(registry, persistence);
            var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a");

            await service.ImportZoneRegionsAsync(zoneId, importPath);

            var regions = service.GetRegionsForZone(zoneId).ToList();

            var region = Assert.Single(regions);
            Assert.Equal("Imported Region", region.Name);
            Assert.Equal(ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, region.Type);
            Assert.Equal(new GridCell(2, 3), Assert.Single(region.Cells));
        }
        finally
        {
            if (System.IO.File.Exists(storagePath))
                System.IO.File.Delete(storagePath);

            if (System.IO.File.Exists(importPath))
                System.IO.File.Delete(importPath);
        }
    }

    [Fact]
    public async Task ImportZoneRegionsAsync_UpdatesExistingRegistryRegionByImportedName()
    {
        var storagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"objecttracker-regions-{Guid.NewGuid()}.json");
        var importPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"objecttracker-import-{Guid.NewGuid()}.json");

        try
        {
            var importData = new
            {
                regions = new[]
                {
                    new
                    {
                        Name = "Existing Region",
                        Type = 10,
                        Cells = new[] { new { Column = 4, Row = 5 } }
                    }
                }
            };

            await System.IO.File.WriteAllTextAsync(
                importPath,
                System.Text.Json.JsonSerializer.Serialize(importData));

            var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a");
            var existingRegion = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                Id: Guid.NewGuid(),
                Name: "Existing Region",
                Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
                CameraZoneId: zoneId,
                Cells: new[] { new GridCell(1, 1) },
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                OverlappingZoneIds: null);

            var persistence = new RegionPersistence(storagePath);
            await persistence.SaveAsync(new[] { existingRegion });

            var registry = new RegionRegistry(persistence);
            registry.Create(existingRegion);
            var service = new RegionManagerService(registry, persistence);

            await service.ImportZoneRegionsAsync(zoneId, importPath);

            var region = Assert.Single(service.GetRegionsForZone(zoneId));
            Assert.Equal(existingRegion.Id, region.Id);
            Assert.Equal(ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion, region.Type);
            Assert.Equal(new GridCell(4, 5), Assert.Single(region.Cells));
        }
        finally
        {
            if (System.IO.File.Exists(storagePath))
                System.IO.File.Delete(storagePath);

            if (System.IO.File.Exists(importPath))
                System.IO.File.Delete(importPath);
        }
    }

    [Fact]
    public async Task ImportCameraRegionsAsync_AddsImportedCameraRegionsToRegistry()
    {
        var storagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"objecttracker-regions-{Guid.NewGuid()}.json");
        var importPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"objecttracker-import-{Guid.NewGuid()}.json");

        try
        {
            var sourceZone = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("source-zone");
            var targetZone = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("target-zone");
            var sourceRegion = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                Id: Guid.NewGuid(),
                Name: "Imported Camera Region",
                Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExitCrossroadRegion,
                CameraZoneId: sourceZone,
                Cells: new[] { new GridCell(6, 7) },
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                OverlappingZoneIds: null);

            var importPersistence = new RegionPersistence(importPath);
            await importPersistence.SaveAsync(new[] { sourceRegion });

            var persistence = new RegionPersistence(storagePath);
            var registry = new RegionRegistry(persistence);
            var service = new RegionManagerService(registry, persistence);

            var importedCount = await service.ImportCameraRegionsAsync(targetZone, importPath);

            Assert.Equal(1, importedCount);
            var region = Assert.Single(service.GetRegionsForZone(targetZone));
            Assert.Equal("Imported Camera Region", region.Name);
            Assert.Equal(targetZone, region.CameraZoneId);
            Assert.NotEqual(sourceRegion.Id, region.Id);
        }
        finally
        {
            if (System.IO.File.Exists(storagePath))
                System.IO.File.Delete(storagePath);

            if (System.IO.File.Exists(importPath))
                System.IO.File.Delete(importPath);
        }
    }
}
