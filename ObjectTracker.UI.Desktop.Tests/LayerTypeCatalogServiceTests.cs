using System;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class LayerTypeCatalogServiceTests
{
    [Fact]
    public void CreateDefault_ReturnsDeterministicPrecedenceOrdering()
    {
        var catalog = LayerTypeCatalogService.CreateDefault();

        var definitions = catalog.GetOrderedByPrecedence().ToList();

        Assert.NotEmpty(definitions);
        Assert.True(definitions.Zip(definitions.Skip(1), (left, right) => left.Precedence <= right.Precedence).All(value => value));
    }

    [Fact]
    public void Create_WithDuplicatePrecedence_Throws()
    {
        var duplicate = new[]
        {
            new LayerTypeDefinition("no-vision", "No-Vision", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("rail-roi", "Rail ROI", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => LayerTypeCatalogService.Create(duplicate));

        Assert.Contains("Duplicate precedence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LayerTypeUsageProjection_ShowsCameraUsageForSelectedLayerType()
    {
        var usage = MainWindow.BuildLayerTypeUsageProjection(
            "NO-VISION",
            new[]
            {
                new CameraZoneLayer("layer-1", "zone-a", "NO-VISION", "Bridge Mask", Array.Empty<CameraZoneRegion>())
            },
            new[] { new CameraZoneDefinition("zone-a", "Bridge Camera Zone") },
            new[] { new CameraZoneBinding("source-a", "zone-a") },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source-a"] = "Bridge Camera"
            });

        Assert.Single(usage.Items);
        Assert.Equal("Bridge Camera", usage.Items[0].CameraDisplayName);
        Assert.Equal("Bridge Camera Zone", usage.Items[0].CameraZoneName);
        Assert.Equal("Bridge Mask", usage.Items[0].LayerName);
    }

    [Fact]
    public void LayerTypeDeleteState_WhenLayerTypeIsInUse_BlocksDeleteWithDependencyContext()
    {
        var usage = new MainWindow.LayerTypeUsageProjection(new[]
        {
            new MainWindow.LayerTypeUsageItem("Bridge Camera", "Bridge Camera Zone", "Bridge Mask", 2)
        });

        var state = MainWindow.BuildLayerTypeDeleteState("NO-VISION", usage);

        Assert.False(state.CanDelete);
        Assert.Contains("NO-VISION", state.Message, StringComparison.Ordinal);
        Assert.Contains("Bridge Camera", state.Message, StringComparison.Ordinal);
        Assert.Contains("Bridge Mask", state.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddLayerType_ReturnsCatalogWithNewGlobalLayerType()
    {
        var catalog = LayerTypeCatalogService.CreateDefault();

        var updated = catalog.AddLayerType(new LayerTypeDefinition(
            "station-platform",
            "Station Platform",
            40,
            LayerMergePolicy.MergeForEffectiveMask,
            LayerTypeBehaviorClass.Informational));

        var definitions = updated.GetOrderedByPrecedence();

        Assert.Contains(definitions, definition => definition.LayerTypeId == "STATION-PLATFORM" && definition.DisplayName == "Station Platform");
    }

    [Fact]
    public void TryAddLayerType_WithDuplicatePrecedence_ReturnsWarningAndLeavesCatalogUnchanged()
    {
        var catalog = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("no-vision", "No-Vision", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled)
        });

        var result = catalog.TryAddLayerType(new LayerTypeDefinition(
            "rail-roi",
            "Rail ROI",
            0,
            LayerMergePolicy.PreserveRegions,
            LayerTypeBehaviorClass.LogicCoupled));

        Assert.False(result.Added);
        Assert.Same(catalog, result.Catalog);
        Assert.Contains("Duplicate precedence", result.Warning, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Catalog.GetOrderedByPrecedence(), definition => definition.LayerTypeId == "RAIL-ROI");
    }

    [Fact]
    public void RemoveLayerType_ReturnsCatalogWithoutRemovedGlobalLayerType()
    {
        var catalog = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("temporary", "Temporary", 10, LayerMergePolicy.MergeForEffectiveMask, LayerTypeBehaviorClass.Informational),
            new LayerTypeDefinition("neutral", "Neutral", 20, LayerMergePolicy.MergeForEffectiveMask, LayerTypeBehaviorClass.Informational)
        });

        var updated = catalog.RemoveLayerType("temporary");

        Assert.DoesNotContain(updated.GetOrderedByPrecedence(), definition => definition.LayerTypeId == "TEMPORARY");
        Assert.Contains(updated.GetOrderedByPrecedence(), definition => definition.LayerTypeId == "NEUTRAL");
    }
}
