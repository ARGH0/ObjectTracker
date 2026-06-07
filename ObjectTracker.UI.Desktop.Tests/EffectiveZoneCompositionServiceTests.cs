using System.Linq;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class EffectiveZoneCompositionServiceTests
{
    /// <summary>
    /// <description>Feature: EffectiveZoneCompositionService.Compose resolves overlapping cells by giving priority to the higher-priority layer type.
    /// 
    ///   Scenario: Two layers of different types overlap on cell (1,1), the NO-VISION layer should win over NEUTRAL.
    ///     Given an EffectiveZoneCompositionService,
    ///      And a LayerTypeCatalog with NO-VISION (precedence 0) and NEUTRAL (precedence 30),
    ///      And two layers: layer-a (NEUTRAL, region-a at cell 1,1) and layer-b (NO-VISION, region-b at cell 1,1),
    ///     When Compose("zone-1", layers, catalog) is called,
    ///     Then exactly one result should be returned,
    ///      And the result LayerTypeId should be "NO-VISION",
    ///      And the result RegionId should be "region-b".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: EffectiveZoneCompositionService.Compose produces a null RegionId when the winning type uses MergeForEffectiveMask.
    /// 
    ///   Scenario: A single NEUTRAL layer with MergeForEffectiveMask policy overlaps on cell (2,3).
    ///     Given an EffectiveZoneCompositionService,
    ///      And a LayerTypeCatalog with NEUTRAL (precedence 10, MergeForEffectiveMask),
    ///      And one layer: layer-a (NEUTRAL, region-a at cell 2,3),
    ///     When Compose("zone-1", layers, catalog) is called,
    ///     Then exactly one result should be returned,
    ///      And the result RegionId should be null.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: EffectiveZoneCompositionService.Compose produces deterministic results when layers of the same type overlap.
    /// 
    ///   Scenario: Two HIGH-CAUTION layers overlap on cell (2,2), the winner is determined by layer ID and region ID ordering.
    ///     Given an EffectiveZoneCompositionService,
    ///      And a LayerTypeCatalog with HIGH-CAUTION (precedence 10, PreserveRegions),
    ///      And two layers: layer-z (region-z at cell 2,2) and layer-a (region-a at cell 2,2),
    ///     When Compose("zone-1", layers, catalog) is called,
    ///     Then exactly one result should be returned,
    ///      And the result LayerId should be "layer-a" (alphabetically first),
    ///      And the result RegionId should be "region-a".</description>
    /// </summary>
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
