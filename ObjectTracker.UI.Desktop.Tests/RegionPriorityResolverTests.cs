using System;
using System.Collections.Generic;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;
using Xunit;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;
using RegionGridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RegionPriorityResolverTests
{
    private static IReadOnlyCollection<RegionGridCell> Cells(int col, int row)
    {
        return new List<RegionGridCell> { new RegionGridCell(col, row) }.AsReadOnly();
    }

    private static RegionDefinition Region(RegionType type, CameraZoneId zoneId)
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new RegionDefinition(Guid.NewGuid(), "region", type, zoneId, Cells(0, 0), baseTime, baseTime, null);
    }

    [Fact]
    public void Resolve_WithExcludedRegion_ExcludeWins()
    {
        var resolver = new RegionPriorityResolver();
        var zoneId = new CameraZoneId("test-zone");

        var regions = new List<RegionDefinition>
        {
            Region(RegionType.CameraOverlapRegion, zoneId),
            Region(RegionType.ExcludeRegion, zoneId),
            Region(RegionType.HighProbabilityRailRegion, zoneId),
        };

        var result = resolver.Resolve(regions);

        Assert.Equal(RegionType.ExcludeRegion, result);
    }

    [Fact]
    public void Resolve_WithHighProbabilityAndCrossroadRegions_HighProbabilityWins()
    {
        var resolver = new RegionPriorityResolver();
        var zoneId = new CameraZoneId("test-zone");

        var regions = new List<RegionDefinition>
        {
            Region(RegionType.EnterCrossroadRegion, zoneId),
            Region(RegionType.ExitCrossroadRegion, zoneId),
            Region(RegionType.HighProbabilityRailRegion, zoneId),
        };

        var result = resolver.Resolve(regions);

        Assert.Equal(RegionType.HighProbabilityRailRegion, result);
    }

    [Fact]
    public void Resolve_WithCameraOverlapAndAllOtherTypes_CameraOverlapLoses()
    {
        var resolver = new RegionPriorityResolver();
        var zoneId = new CameraZoneId("test-zone");

        var regions = new List<RegionDefinition>
        {
            Region(RegionType.CameraOverlapRegion, zoneId),
            Region(RegionType.HighProbabilityRailRegion, zoneId),
        };

        var result = resolver.Resolve(regions);

        Assert.Equal(RegionType.HighProbabilityRailRegion, result);
    }

    [Fact]
    public void Resolve_EmptyRegions_ReturnsDefault()
    {
        var resolver = new RegionPriorityResolver();

        var result = resolver.Resolve(Array.Empty<RegionDefinition>());

        Assert.Equal(RegionType.CameraOverlapRegion, result);
    }
}
