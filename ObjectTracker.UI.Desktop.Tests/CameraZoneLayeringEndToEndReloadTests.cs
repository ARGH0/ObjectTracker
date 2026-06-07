using System;
using System.IO;
using System.Linq;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneLayeringEndToEndReloadTests
{
    /// <summary>
    /// <description>Feature: Camera zone layering end-to-end reload preserves app grid, camera zones, and regions through save/load cycles.
    /// 
    ///   Scenario: Saving app settings, layer types, zone bindings, and layers to temporary files, then reloading them should produce correct composition results.
    ///     Given an AppSettingsStore with a 32x18 grid,
    ///      And a LayerTypeCatalogService with NO-VISION (precedence 0) and NEUTRAL (precedence 20),
    ///      And a CameraZoneBindingStore with zone "zone-1" (Bridge Zone) bound to "video-a.mp4",
    ///      And a CameraZoneLayerRepository with two layers: layer-hc (NO-VISION, region-100 at cell 3,3 with code 100) and layer-n (NEUTRAL, region-200 at cells 3,3 and 4,3),
    ///     When all stores are loaded back from disk and EffectiveZoneCompositionService.Compose is called for "zone-1",
    ///     Then GridColumns should be 32 and GridRows should be 18,
    ///      And the zone snapshot should contain one zone with CameraZoneId "zone-1",
    ///      And the preserved region should have RegionId "region-100", Name "Bridge Underpass", and Code 100,
    ///      And the cell at (3,3) in composition should resolve to NO-VISION layer type with region-100.</description>
    /// </summary>
    [Fact]
    public void ReloadRoundTrip_PreservesAppGridCameraZoneAndRegions()
    {
        var paths = BuildTempPaths();

        var appSettingsStore = new AppSettingsStore(paths.AppSettingsPath);
        var layerTypeStore = new LayerTypeSettingsStore(paths.LayerTypesPath);
        var zoneBindingStore = new CameraZoneBindingStore(paths.CameraZonesPath);
        var layerRepository = new CameraZoneLayerRepository(paths.CameraZoneLayersPath);

        appSettingsStore.Save(new AppSettings(32, 18));

        var catalog = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("NO-VISION", "No-Vision", 0, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled),
            new LayerTypeDefinition("NEUTRAL", "Neutral", 20, LayerMergePolicy.MergeForEffectiveMask, LayerTypeBehaviorClass.Informational)
        });
        layerTypeStore.Save(catalog.GetOrderedByPrecedence());

        zoneBindingStore.Save(
            zones: new[] { new CameraZoneDefinition("zone-1", "Bridge Zone") },
            bindings: new[] { new CameraZoneBinding("video-a.mp4", "zone-1") });

        layerRepository.Save(new[]
        {
            new CameraZoneLayer(
                LayerId: "layer-hc",
                CameraZoneId: "zone-1",
                LayerTypeId: "NO-VISION",
                Name: "No-Vision Layer",
                Regions: new[]
                {
                    new CameraZoneRegion("region-100", "Bridge Underpass", 100, new[] { new GridCell(3, 3) })
                }),
            new CameraZoneLayer(
                LayerId: "layer-n",
                CameraZoneId: "zone-1",
                LayerTypeId: "NEUTRAL",
                Name: "Neutral Layer",
                Regions: new[]
                {
                    new CameraZoneRegion("region-200", "Background", null, new[] { new GridCell(3, 3), new GridCell(4, 3) })
                })
        });

        var loadedAppSettings = appSettingsStore.Load();
        var loadedCatalog = layerTypeStore.Load();
        var loadedZoneSnapshot = zoneBindingStore.Load();
        var loadedLayers = layerRepository.Load();

        Assert.Equal(32, loadedAppSettings.GridColumns);
        Assert.Equal(18, loadedAppSettings.GridRows);

        Assert.Single(loadedZoneSnapshot.Zones);
        Assert.Equal("zone-1", loadedZoneSnapshot.Zones.Single().CameraZoneId);
        Assert.Single(loadedZoneSnapshot.Bindings);
        Assert.Equal("zone-1", loadedZoneSnapshot.Bindings.Single().CameraZoneId);

        var preservedRegion = loadedLayers
            .Single(layer => layer.LayerId == "layer-hc")
            .Regions
            .Single();
        Assert.Equal("region-100", preservedRegion.RegionId);
        Assert.Equal("Bridge Underpass", preservedRegion.Name);
        Assert.Equal(100, preservedRegion.Code);

        var composition = new EffectiveZoneCompositionService().Compose("zone-1", loadedLayers, loadedCatalog);
        var overlapCell = composition.Single(cell => cell.Cell.Equals(new GridCell(3, 3)));
        Assert.Equal("NO-VISION", overlapCell.LayerTypeId);
        Assert.Equal("region-100", overlapCell.RegionId);
    }

    /// <summary>
    /// <description>Feature: Camera zone layering end-to-end reload produces deterministic composition results.
    /// 
    ///   Scenario: Saving and reloading layers with the same type overlapping on the same cell should produce a consistent winner based on layer ID ordering.
    ///     Given a LayerTypeCatalogService with HIGH-CAUTION (precedence 5),
    ///      And a CameraZoneLayerRepository with two layers "layer-z" and "layer-a" both of type HIGH-CAUTION overlapping at cell (1,1),
    ///     When all stores are loaded back from disk and EffectiveZoneCompositionService.Compose is called for "zone-1",
    ///     Then composition should contain exactly one entry,
    ///      And the winning layer should be "layer-a" (alphabetically first),
    ///      And the winning region should be "region-a".</description>
    /// </summary>
    [Fact]
    public void ReloadRoundTrip_CompositionRemainsDeterministic()
    {
        var paths = BuildTempPaths();

        var layerTypeStore = new LayerTypeSettingsStore(paths.LayerTypesPath);
        var layerRepository = new CameraZoneLayerRepository(paths.CameraZoneLayersPath);

        var catalog = LayerTypeCatalogService.Create(new[]
        {
            new LayerTypeDefinition("HIGH-CAUTION", "High-Caution", 5, LayerMergePolicy.PreserveRegions, LayerTypeBehaviorClass.LogicCoupled)
        });
        layerTypeStore.Save(catalog.GetOrderedByPrecedence());

        layerRepository.Save(new[]
        {
            new CameraZoneLayer("layer-z", "zone-1", "HIGH-CAUTION", "Z", new[]
            {
                new CameraZoneRegion("region-z", "z", null, new[] { new GridCell(1, 1) })
            }),
            new CameraZoneLayer("layer-a", "zone-1", "HIGH-CAUTION", "A", new[]
            {
                new CameraZoneRegion("region-a", "a", null, new[] { new GridCell(1, 1) })
            })
        });

        var loadedCatalog = layerTypeStore.Load();
        var loadedLayers = layerRepository.Load();
        var composition = new EffectiveZoneCompositionService().Compose("zone-1", loadedLayers, loadedCatalog);

        Assert.Single(composition);
        Assert.Equal("layer-a", composition.Single().LayerId);
        Assert.Equal("region-a", composition.Single().RegionId);
    }

    private static TempPaths BuildTempPaths()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        return new TempPaths(
            Path.Combine(folder, "app-settings.json"),
            Path.Combine(folder, "layer-types.json"),
            Path.Combine(folder, "camera-zones.json"),
            Path.Combine(folder, "camera-zone-layers.json"));
    }

    private readonly record struct TempPaths(
        string AppSettingsPath,
        string LayerTypesPath,
        string CameraZonesPath,
        string CameraZoneLayersPath);
}
