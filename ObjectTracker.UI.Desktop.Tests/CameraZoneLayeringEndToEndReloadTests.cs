using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneLayeringEndToEndReloadTests
{
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
