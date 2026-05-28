using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraZoneLayerRepositoryTests
{
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
