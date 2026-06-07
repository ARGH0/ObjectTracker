using System;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneLayerEditorServiceTests
{
    /// <summary>
    /// <description>Feature: CameraZoneLayerEditorService.AddLayer appends a new layer for a camera zone.
    /// 
    ///   Scenario: Adding a layer to an empty set of layers creates a single layer with the correct properties.
    ///     Given a CameraZoneLayerEditorService,
    ///     When AddLayer is called with an empty layers array, "zone-a", "HIGH-CAUTION", and "Bridge",
    ///     Then exactly one layer should be returned,
    ///      And the layer CameraZoneId should be "zone-a",
    ///      And the layer LayerTypeId should be "HIGH-CAUTION".</description>
    /// </summary>
    [Fact]
    public void AddLayer_AppendsLayerForCameraZone()
    {
        var service = new CameraZoneLayerEditorService();
        var layers = service.AddLayer(Array.Empty<CameraZoneLayer>(), "zone-a", "HIGH-CAUTION", "Bridge");

        Assert.Single(layers);
        Assert.Equal("zone-a", layers[0].CameraZoneId);
        Assert.Equal("HIGH-CAUTION", layers[0].LayerTypeId);
    }

    /// <summary>
    /// <description>Feature: CameraZoneLayerEditorService.UpsertRegion rejects cell coordinates outside the configured grid bounds.
    /// 
    ///   Scenario: Attempting to upsert a region with a cell coordinate beyond the app settings grid dimensions should throw.
    ///     Given a CameraZoneLayerEditorService and an existing layer for "zone-a" of type "HIGH-CAUTION",
    ///     When UpsertRegion is called with cell "99,99" on a 32x18 grid,
    ///     Then an InvalidOperationException should be thrown containing "outside configured grid".</description>
    /// </summary>
    [Fact]
    public void UpsertRegion_RejectsCellOutsideGridBounds()
    {
        var service = new CameraZoneLayerEditorService();
        var layers = service.AddLayer(Array.Empty<CameraZoneLayer>(), "zone-a", "HIGH-CAUTION", "Bridge");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.UpsertRegion(layers, layers[0].LayerId, null, "R1", null, "99,99", new AppSettings(32, 18)));

        Assert.Contains("outside configured grid", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <description>Feature: CameraZoneLayerEditorService.UpsertRegion and RemoveRegion correctly update layer regions.
    /// 
    ///   Scenario: Adding a region to a layer and then removing it should result in an empty regions collection.
    ///     Given a CameraZoneLayerEditorService and an existing layer for "zone-a" of type "HIGH-CAUTION",
    ///      And UpsertRegion is called to add a region "Entry" with cells "1,1;2,1",
    ///      And RemoveRegion is called on that same layer and region,
    ///     Then the resulting layer should have zero regions.</description>
    /// </summary>
    [Fact]
    public void UpsertRegion_ThenRemoveRegion_UpdatesLayerRegions()
    {
        var service = new CameraZoneLayerEditorService();
        var layers = service.AddLayer(Array.Empty<CameraZoneLayer>(), "zone-a", "HIGH-CAUTION", "Bridge");

        var withRegion = service.UpsertRegion(layers, layers[0].LayerId, null, "Entry", 10, "1,1;2,1", new AppSettings(32, 18));
        var regionId = withRegion[0].Regions.Single().RegionId;

        var withoutRegion = service.RemoveRegion(withRegion, withRegion[0].LayerId, regionId);

        Assert.Empty(withoutRegion[0].Regions);
    }
}
