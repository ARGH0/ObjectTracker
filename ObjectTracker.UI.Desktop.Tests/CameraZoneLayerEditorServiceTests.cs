using System;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneLayerEditorServiceTests
{
    [Fact]
    public void AddLayer_AppendsLayerForCameraZone()
    {
        var service = new CameraZoneLayerEditorService();
        var layers = service.AddLayer(Array.Empty<CameraZoneLayer>(), "zone-a", "HIGH-CAUTION", "Bridge");

        Assert.Single(layers);
        Assert.Equal("zone-a", layers[0].CameraZoneId);
        Assert.Equal("HIGH-CAUTION", layers[0].LayerTypeId);
    }

    [Fact]
    public void UpsertRegion_RejectsCellOutsideGridBounds()
    {
        var service = new CameraZoneLayerEditorService();
        var layers = service.AddLayer(Array.Empty<CameraZoneLayer>(), "zone-a", "HIGH-CAUTION", "Bridge");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.UpsertRegion(layers, layers[0].LayerId, null, "R1", null, "99,99", new AppSettings(32, 18)));

        Assert.Contains("outside configured grid", exception.Message, StringComparison.Ordinal);
    }

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
