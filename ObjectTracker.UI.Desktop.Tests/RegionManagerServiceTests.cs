using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;
using Xunit;

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
}
