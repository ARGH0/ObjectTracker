using System;
using System.Linq;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class LayerTypeCatalogServiceTests
{
    /// <summary>
    /// <description>Feature: LayerTypeCatalogService.CreateDefault returns a catalog with deterministic precedence ordering.
    /// 
    ///   Scenario: Creating the default layer type catalog should produce definitions sorted by ascending precedence.
    ///     Given LayerTypeCatalogService.CreateDefault() is called,
    ///     When GetOrderedByPrecedence() is called on the returned catalog,
    ///     Then the result should not be empty,
    ///      And every adjacent pair of definitions should have left.Precedence <= right.Precedence.</description>
    /// </summary>
    [Fact]
    public void CreateDefault_ReturnsDeterministicPrecedenceOrdering()
    {
        var catalog = LayerTypeCatalogService.CreateDefault();

        var definitions = catalog.GetOrderedByPrecedence().ToList();

        Assert.NotEmpty(definitions);
        Assert.True(definitions.Zip(definitions.Skip(1), (left, right) => left.Precedence <= right.Precedence).All(value => value));
    }

    /// <summary>
    /// <description>Feature: LayerTypeCatalogService.Create throws when given duplicate precedence values.
    /// 
    ///   Scenario: Creating a catalog with two layer types that share the same precedence should fail.
    ///     Given two LayerTypeDefinitions both with precedence 0 (no-vision and rail-roi),
    ///     When LayerTypeCatalogService.Create is called with these definitions,
    ///     Then an InvalidOperationException should be thrown containing "Duplicate precedence".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: LayerTypeUsageProjection shows camera usage information for a selected layer type.
    /// 
    ///   Scenario: Building a layer type usage projection for NO-VISION that is used in one camera zone should surface the correct details.
    ///     Given a NO-VISION layer "Bridge Mask" on "zone-a",
    ///      And a CameraZoneDefinition "zone-a" named "Bridge Camera Zone",
    ///      And a source binding from "source-a" to "zone-a",
    ///      And a display name mapping "source-a" -> "Bridge Camera",
    ///     When BuildLayerTypeUsageProjection("NO-VISION", ...) is called,
    ///     Then usage.Items should contain exactly one entry with CameraDisplayName "Bridge Camera", CameraZoneName "Bridge Camera Zone", and LayerName "Bridge Mask".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: LayerTypeDeleteState blocks deletion when a layer type is in use and provides dependency context.
    /// 
    ///   Scenario: Attempting to delete a layer type that has active usage should be blocked with an informative message.
    ///     Given a LayerTypeUsageProjection for NO-VISION showing it is used as "Bridge Mask" on "Bridge Camera" in "Bridge Camera Zone",
    ///     When BuildLayerTypeDeleteState("NO-VISION", usage) is called,
    ///     Then CanDelete should be false,
    ///      And the message should contain "NO-VISION", "Bridge Camera", and "Bridge Mask".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: LayerTypeCatalogService.AddLayerType adds a new global layer type to the catalog.
    /// 
    ///   Scenario: Adding a "station-platform" layer type with precedence 40 should result in it being present in the updated catalog.
    ///     Given a default LayerTypeCatalog,
    ///     When AddLayerType is called with Station Platform (precedence 40, MergeForEffectiveMask),
    ///     Then the updated catalog should contain a definition with LayerTypeId "STATION-PLATFORM" and DisplayName "Station Platform".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: LayerTypeCatalogService.TryAddLayerType returns a warning and leaves the catalog unchanged when precedence is duplicate.
    /// 
    ///   Scenario: Attempting to add a layer type with a precedence already in use should fail gracefully without modifying the catalog.
    ///     Given a LayerTypeCatalog with no-vision (precedence 0),
    ///     When TryAddLayerType is called with rail-roi also at precedence 0,
    ///     Then Added should be false,
    ///      And the returned Catalog should be the same instance as the original,
    ///      And Warning should contain "Duplicate precedence",
    ///      And RAIL-ROI should not be present in the catalog.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: LayerTypeCatalogService.RemoveLayerType removes a global layer type from the catalog.
    /// 
    ///   Scenario: Removing "temporary" (precedence 10) from a two-element catalog should leave only "neutral" (precedence 20).
    ///     Given a LayerTypeCatalog with temporary (precedence 10) and neutral (precedence 20),
    ///     When RemoveLayerType("temporary") is called,
    ///     Then TEMPORARY should not be present in the updated catalog,
    ///      And NEUTRAL should still be present.</description>
    /// </summary>
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
