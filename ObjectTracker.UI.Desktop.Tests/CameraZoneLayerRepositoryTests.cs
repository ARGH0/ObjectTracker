using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneLayerRepositoryTests
{
    /// <summary>
    /// <description>Feature: CameraZoneLayerRepository persists and reloads multiple layers of the same type per camera zone.
    /// 
    ///   Scenario: Saving two HIGH-CAUTION layers for "zone-a" with different regions and reloading should preserve both.
    ///     Given a CameraZoneLayerRepository initialized with a temporary file path,
    ///      And Save is called with layer-1 (Bridge approach, region-1 at cells 1,1;1,2) and layer-2 (Switch cluster, region-2 at cell 5,5),
    ///     When Load() is called,
    ///     Then exactly two layers of type HIGH-CAUTION for zone-a should be returned,
    ///      And both layer-1 and layer-2 should be present.</description>
    /// </summary>
    [Fact]
    public void SaveThenLoad_PreservesMultipleLayersOfSameTypePerCameraZone()
    {
        var repository = new CameraZoneLayerRepository(BuildTempFilePath());
        var cameraZoneId = "zone-a";

        repository.Save(new[]
        {
            new CameraZoneLayer(
                LayerId: "layer-1",
                CameraZoneId: cameraZoneId,
                LayerTypeId: "HIGH-CAUTION",
                Name: "Bridge approach",
                Regions: new[]
                {
                    new CameraZoneRegion("region-1", "Bridge Entry", 101, new [] { new GridCell(1, 1), new GridCell(1, 2) })
                }),
            new CameraZoneLayer(
                LayerId: "layer-2",
                CameraZoneId: cameraZoneId,
                LayerTypeId: "HIGH-CAUTION",
                Name: "Switch cluster",
                Regions: new[]
                {
                    new CameraZoneRegion("region-2", "Switch A", 202, new [] { new GridCell(5, 5) })
                })
        });

        var loaded = repository.Load();
        var sameTypeLayers = loaded.Where(layer => layer.CameraZoneId == cameraZoneId && layer.LayerTypeId == "HIGH-CAUTION").ToList();

        Assert.Equal(2, sameTypeLayers.Count);
        Assert.Contains(sameTypeLayers, layer => layer.LayerId == "layer-1");
        Assert.Contains(sameTypeLayers, layer => layer.LayerId == "layer-2");
    }

    /// <summary>
    /// <description>Feature: CameraZoneLayerRepository preserves region identity while allowing editable fields to change.
    /// 
    ///   Scenario: Saving a layer twice with modified region data should retain the original RegionId but reflect updated fields.
    ///     Given a CameraZoneLayerRepository initialized with a temporary file path,
    ///      And Save is called initially with layer-1 containing region-77 (Original, code 7, cell 2,3),
    ///      And Save is called again with the same layer but region-77 renamed to "Renamed Region" with code 999 and cell 4,4,
    ///     When Load() is called,
    ///     Then loadedRegion.RegionId should be "region-77",
    ///      And loadedRegion.Name should be "Renamed Region",
    ///      And loadedRegion.Code should be 999,
    ///      And the cell should be (4,4).</description>
    /// </summary>
    [Fact]
    public void SaveThenLoad_PreservesRegionIdentityWhileAllowingEditableFields()
    {
        var repository = new CameraZoneLayerRepository(BuildTempFilePath());

        repository.Save(new[]
        {
            new CameraZoneLayer(
                LayerId: "layer-1",
                CameraZoneId: "zone-a",
                LayerTypeId: "NEUTRAL",
                Name: "Neutral layer",
                Regions: new[]
                {
                    new CameraZoneRegion("region-77", "Original", 7, new [] { new GridCell(2, 3) })
                })
        });

        repository.Save(new[]
        {
            new CameraZoneLayer(
                LayerId: "layer-1",
                CameraZoneId: "zone-a",
                LayerTypeId: "NEUTRAL",
                Name: "Neutral layer",
                Regions: new[]
                {
                    new CameraZoneRegion("region-77", "Renamed Region", 999, new [] { new GridCell(4, 4) })
                })
        });

        var loadedRegion = repository.Load().Single().Regions.Single();

        Assert.Equal("region-77", loadedRegion.RegionId);
        Assert.Equal("Renamed Region", loadedRegion.Name);
        Assert.Equal(999, loadedRegion.Code);
        Assert.Single(loadedRegion.Cells);
        Assert.Equal(new GridCell(4, 4), loadedRegion.Cells.Single());
    }

    /// <summary>
    /// <description>Feature: CameraZoneLayerRepository returns an empty list when the file is missing.
    /// 
    ///   Scenario: Loading from a non-existent path produces an empty layers collection.
    ///     Given a CameraZoneLayerRepository initialized with a temporary file path that does not exist,
    ///     When Load() is called,
    ///     Then loaded should be empty.</description>
    /// </summary>
    [Fact]
    public void Load_WhenFileMissing_ReturnsEmpty()
    {
        var repository = new CameraZoneLayerRepository(BuildTempFilePath());

        var loaded = repository.Load();

        Assert.Empty(loaded);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "camera-zone-layers.json");
    }
}
