using System.Reflection;
using Xunit;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RegionModelTests
{
    [Fact]
    public void RegionType_HasAllFiveValues()
    {
        var values = Enum.GetValues<RegionType>();

        Assert.Equal(5, values.Length);
        Assert.Contains(RegionType.ExcludeRegion, values);
        Assert.Contains(RegionType.HighProbabilityRailRegion, values);
        Assert.Contains(RegionType.EnterCrossroadRegion, values);
        Assert.Contains(RegionType.ExitCrossroadRegion, values);
        Assert.Contains(RegionType.CameraOverlapRegion, values);
    }

    [Fact]
    public void RegionType_PrioritiesMatchSpec()
    {
        Assert.Equal(0, (int)RegionType.ExcludeRegion);
        Assert.Equal(10, (int)RegionType.HighProbabilityRailRegion);
        Assert.Equal(20, (int)RegionType.EnterCrossroadRegion);
        Assert.Equal(20, (int)RegionType.ExitCrossroadRegion);
        Assert.Equal(30, (int)RegionType.CameraOverlapRegion);
    }

    [Fact]
    public void GridCell_IsImmutable()
    {
        var type = typeof(GridCell);
        var info = type.GetTypeInfo();

        Assert.True(type.IsValueType, "GridCell should be a value type");
        Assert.True(info.ContainsGenericParameters == false, "GridCell should not contain unbound generics");

        var cell = new GridCell(5, 10);
        var clone = cell with { };

        Assert.Equal(cell.Column, clone.Column);
        Assert.Equal(cell.Row, clone.Row);
        Assert.True(cell.Equals(clone), "cloned GridCell should be equal");
    }

    [Fact]
    public void GridCell_StoresColumnAndRow()
    {
        var cell = new GridCell(7, 14);

        Assert.Equal(7, cell.Column);
        Assert.Equal(14, cell.Row);
    }

    [Fact]
    public void CameraZoneId_IsImmutable()
    {
        var type = typeof(CameraZoneId);
        var info = type.GetTypeInfo();

        Assert.True(type.IsValueType, "CameraZoneId should be a value type");
        Assert.True(info.ContainsGenericParameters == false, "CameraZoneId should not contain unbound generics");

        var id = new CameraZoneId("zone-abc");
        var clone = id with { };

        Assert.Equal(id.Value, clone.Value);
        Assert.True(id.Equals(clone), "cloned CameraZoneId should be equal");
    }

    [Fact]
    public void CameraZoneId_StoresValue()
    {
        var id = new CameraZoneId("zone-abc-123");

        Assert.Equal("zone-abc-123", id.Value);
    }

    [Fact]
    public void RegionDefinition_IsImmutable()
    {
        var type = typeof(RegionDefinition);
        var info = type.GetTypeInfo();

        Assert.True(type.IsValueType, "RegionDefinition should be a value type");
        Assert.True(info.ContainsGenericParameters == false, "RegionDefinition should not contain unbound generics");

        var cells = new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(0, 0), new(1, 0) };
        var region = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name: "Test Region",
            Type: RegionType.ExcludeRegion,
            CameraZoneId: new CameraZoneId("zone-001"),
            Cells: cells,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: null
        );

        var clone = region with { };

        Assert.Equal(region.Id, clone.Id);
        Assert.Equal(region.Name, clone.Name);
        Assert.Equal(region.Type, clone.Type);
        Assert.True(region.Equals(clone), "cloned RegionDefinition should be equal");
    }

    [Fact]
    public void RegionDefinition_StoresAllRequiredProperties()
    {
        var cells = new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(0, 0), new(1, 0) };
        var region = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name: "Test Region",
            Type: RegionType.ExcludeRegion,
            CameraZoneId: new CameraZoneId("zone-001"),
            Cells: cells,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: null
        );

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), region.Id);
        Assert.Equal("Test Region", region.Name);
        Assert.Equal(RegionType.ExcludeRegion, region.Type);
        Assert.Equal("zone-001", region.CameraZoneId.Value);
        Assert.Equal(2, region.Cells.Count);
    }
}
