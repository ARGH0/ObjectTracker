using System.Linq;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class EffectiveZoneCompositionServiceTests
{
    [Fact]
    public void Compose_WhenCellsOverlap_HigherPriorityLayerTypeWins()
    {
        var service = new EffectiveZoneCompositionService();
        var catalog = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("NO-VISION", "No-Vision", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("NEUTRAL", "Neutral", 30, LayerMergePolicy.MergeForEffectiveMask, LayerTypeBehaviorClass.Informational)
        });

        var layers = new[]
        {
            new CameraZoneLayer("layer-a", "zone-1", "NEUTRAL", "n", new[]
            {
                new CameraZoneRegion("region-a", "r", null, new[] { new GridCell(1, 1) })
            }),
            new CameraZoneLayer("layer-b", "zone-1", "NO-VISION", "x", new[]
            {
                new CameraZoneRegion("region-b", "r", null, new[] { new GridCell(1, 1) })
            })
        };

        var result = service.Compose("zone-1", layers, catalog);

        Assert.Single(result);
        Assert.Equal("NO-VISION", result[0].LayerTypeId);
        Assert.Equal("region-b", result[0].RegionId);
    }

    [Fact]
    public void Compose_WhenWinningTypeIsMergeForEffectiveMask_RegionIdIsNull()
    {
        var service = new EffectiveZoneCompositionService();
        var catalog = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("NEUTRAL", "Neutral", 10, LayerMergePolicy.MergeForEffectiveMask, LayerTypeBehaviorClass.Informational)
        });

        var layers = new[]
        {
            new CameraZoneLayer("layer-a", "zone-1", "NEUTRAL", "n", new[]
            {
                new CameraZoneRegion("region-a", "r", null, new[] { new GridCell(2, 3) })
            })
        };

        var result = service.Compose("zone-1", layers, catalog);

        Assert.Single(result);
        Assert.Null(result[0].RegionId);
    }

    [Fact]
    public void Compose_WithSameTypeOverlap_IsDeterministicByLayerAndRegionId()
    {
        var service = new EffectiveZoneCompositionService();
        var catalog = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("HIGH-CAUTION", "High-Caution", 10, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled)
        });

        var layers = new[]
        {
            new CameraZoneLayer("layer-z", "zone-1", "HIGH-CAUTION", "n", new[]
            {
                new CameraZoneRegion("region-z", "r", null, new[] { new GridCell(2, 2) })
            }),
            new CameraZoneLayer("layer-a", "zone-1", "HIGH-CAUTION", "n", new[]
            {
                new CameraZoneRegion("region-a", "r", null, new[] { new GridCell(2, 2) })
            })
        };

        var result = service.Compose("zone-1", layers, catalog);

        Assert.Single(result);
        Assert.Equal("layer-a", result.Single().LayerId);
        Assert.Equal("region-a", result.Single().RegionId);
    }
}
