using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RegionPersistenceTests : IDisposable
{
    private readonly string _testFilePath;

    public RegionPersistenceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"objecttracker-test-regions-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task RoundTrip_EmptyList_SerializesToEmptyArray()
    {
        var persistence = new RegionPersistence(_testFilePath);

        await persistence.SaveAsync(Array.Empty<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition>());

        var loaded = await persistence.LoadAsync();
        var list = loaded.ToList();

        Assert.Empty(list);
    }

    [Fact]
    public async Task RoundTrip_SingleRegion_PreservesAllFields()
    {
        var cells = new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(12, 5), new(13, 5) };
        var original = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            Name: "North Crossing Entry",
            Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.EnterCrossroadRegion,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-abc-123"),
            Cells: cells,
            CreatedAt: new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt: new DateTime(2026, 6, 9, 14, 30, 0, DateTimeKind.Utc),
            OverlappingZoneIds: null
        );

        var persistence = new RegionPersistence(_testFilePath);
        await persistence.SaveAsync(new[] { original });

        var loaded = (await persistence.LoadAsync()).ToList();

        Assert.Single(loaded);
        var region = loaded[0];
        Assert.Equal(original.Id, region.Id);
        Assert.Equal(original.Name, region.Name);
        Assert.Equal(original.Type, region.Type);
        Assert.Equal(original.CameraZoneId, region.CameraZoneId);
        Assert.Equal(original.Cells.Count, region.Cells.Count);
        Assert.Equal(original.CreatedAt, region.CreatedAt);
        Assert.Equal(original.UpdatedAt, region.UpdatedAt);
    }

    [Fact]
    public async Task RoundTrip_MultipleZones_PreservesAllRegions()
    {
        var regions = new[]
        {
            new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name: "Zone A Region",
                Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
                CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"),
                Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(0, 0) },
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                OverlappingZoneIds: null
            ),
            new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name: "Zone B Region",
                Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion,
                CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-b"),
                Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(1, 1), new(2, 1) },
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                OverlappingZoneIds: null
            )
        };

        var persistence = new RegionPersistence(_testFilePath);
        await persistence.SaveAsync(regions);

        var loaded = (await persistence.LoadAsync()).ToList();

        Assert.Equal(2, loaded.Count);
        var byId = loaded.ToDictionary(r => r.Id);
        Assert.True(byId.ContainsKey(regions[0].Id));
        Assert.True(byId.ContainsKey(regions[1].Id));
        Assert.Equal(regions[0].Name, byId[regions[0].Id].Name);
        Assert.Equal(regions[1].Name, byId[regions[1].Id].Name);
    }

    [Fact]
    public async Task LoadAsync_FileDoesNotExist_ReturnsEmpty()
    {
        var persistence = new RegionPersistence(_testFilePath);

        var loaded = await persistence.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task FakeRegionPersistence_StoresAndReturnsRegions()
    {
        var fake = new FakeRegionPersistence();

        var cells = new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(1, 1) };
        var region = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: System.Guid.NewGuid(),
            Name: "Fake Test",
            Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-1"),
            Cells: cells,
            CreatedAt: System.DateTime.UtcNow,
            UpdatedAt: System.DateTime.UtcNow,
            OverlappingZoneIds: null
        );

        await fake.SaveAsync(new[] { region });

        var loaded = (await fake.LoadAsync()).ToList();

        Assert.Single(loaded);
        Assert.Equal(region.Id, loaded[0].Id);
        Assert.Equal(region.Name, loaded[0].Name);
    }

    [Fact]
    public async Task RoundTrip_HighProbabilityRailRegion_PreservesConfidenceBoost()
    {
        var cells = new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(10, 20) };
        var original = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
            Id: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name: "Main Rail Path",
            Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion,
            CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-rail"),
            Cells: cells,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            OverlappingZoneIds: null,
            ConfidenceBoost: 0.25f
        );

        var persistence = new RegionPersistence(_testFilePath);
        await persistence.SaveAsync(new[] { original });

        var loaded = (await persistence.LoadAsync()).ToList();

        Assert.Single(loaded);
        var region = loaded[0];
        Assert.Equal(original.Id, region.Id);
        Assert.Equal(original.Type, region.Type);
        Assert.Equal(0.25f, region.ConfidenceBoost);
    }

    [Fact]
    public async Task ExportAsync_OnlyExportsRegionsForSpecifiedZone()
    {
        var exportPath = Path.Combine(Path.GetTempPath(), $"objecttracker-export-{Guid.NewGuid()}.json");

        try
        {
            var regions = new[]
            {
                new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                    Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name: "Zone A Region",
                    Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
                    CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"),
                    Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(0, 0) },
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    OverlappingZoneIds: null
                ),
                new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                    Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name: "Zone B Region",
                    Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion,
                    CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-b"),
                    Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(1, 1) },
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    OverlappingZoneIds: null
                )
            };

            var persistence = new RegionPersistence(_testFilePath);
            await persistence.SaveAsync(regions);

            var zoneAId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a");
            await persistence.ExportAsync(zoneAId, exportPath);

            var exportedJson = await System.IO.File.ReadAllTextAsync(exportPath);
            Assert.Contains("Zone A Region", exportedJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Zone B Region", exportedJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_MergesImportedRegionsWithExisting()
    {
        var importPath = Path.Combine(Path.GetTempPath(), $"objecttracker-import-{Guid.NewGuid()}.json");

        try
        {
            // Create an export file with a region named "Zone A Region"
            var exportData = new
            {
                regions = new[]
                {
                    new
                    {
                        Name = "Zone A Region",
                        Type = 0,
                        Cells = new[] { new { Column = 5, Row = 5 } }
                    },
                    new
                    {
                        Name = "New Imported Region",
                        Type = 10,
                        Cells = new[] { new { Column = 3, Row = 3 } }
                    }
                }
            };

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            await System.IO.File.WriteAllTextAsync(importPath, System.Text.Json.JsonSerializer.Serialize(exportData, options));

            // Save existing regions to the main persistence file
            var existingRegions = new[]
            {
                new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                    Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name: "Zone A Region",
                    Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
                    CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a"),
                    Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(0, 0) },
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    OverlappingZoneIds: null
                ),
                new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                    Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name: "Zone B Region",
                    Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion,
                    CameraZoneId: new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-b"),
                    Cells: new ObjectTracker.UI.Desktop.Region.Model.GridCell[] { new(1, 1) },
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    OverlappingZoneIds: null
                )
            };

            var persistence = new RegionPersistence(_testFilePath);
            await persistence.SaveAsync(existingRegions);

            var zoneAId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a");
            await persistence.ImportAsync(importPath, zoneAId);

            var loaded = (await persistence.LoadAsync()).ToList();

            Assert.Equal(3, loaded.Count);

            var zoneARegions = loaded.Where(r => r.CameraZoneId.Equals(zoneAId)).ToList();
            Assert.Equal(2, zoneARegions.Count);

            var importedRegion = zoneARegions.FirstOrDefault(r => r.Name == "New Imported Region");
            Assert.NotEqual(default(ObjectTracker.UI.Desktop.Region.Model.RegionDefinition), importedRegion);
            Assert.Equal(ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion, importedRegion.Type);
        }
        finally
        {
            if (File.Exists(importPath))
            {
                File.Delete(importPath);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_FileDoesNotExist_DoesNotThrow()
    {
        var persistence = new RegionPersistence(_testFilePath);

        await persistence.SaveAsync(Array.Empty<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition>());

        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.json");

        var zoneId = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a");

        await persistence.ImportAsync(nonExistentPath, zoneId);

        var loaded = (await persistence.LoadAsync()).ToList();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task ExportCameraRegionsAsync_WritesFullRegionDefinitionsForCameraZone()
    {
        var zoneA = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-a");
        var zoneB = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("zone-b");
        var exportPath = Path.Combine(Path.GetTempPath(), $"objecttracker-camera-regions-{Guid.NewGuid()}.json");

        try
        {
            var regions = new[]
            {
                new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                    Id: Guid.NewGuid(),
                    Name: "Zone A Region",
                    Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.HighProbabilityRailRegion,
                    CameraZoneId: zoneA,
                    Cells: new[] { new ObjectTracker.UI.Desktop.Region.Model.GridCell(1, 2) },
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    OverlappingZoneIds: null),
                new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                    Id: Guid.NewGuid(),
                    Name: "Zone B Region",
                    Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.ExcludeRegion,
                    CameraZoneId: zoneB,
                    Cells: new[] { new ObjectTracker.UI.Desktop.Region.Model.GridCell(3, 4) },
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    OverlappingZoneIds: null)
            };

            var persistence = new RegionPersistence(_testFilePath);
            await persistence.SaveAsync(regions);

            await persistence.ExportCameraRegionsAsync(zoneA, exportPath);

            var importedPersistence = new RegionPersistence(exportPath);
            var exportedRegions = (await importedPersistence.LoadAsync()).ToList();
            var exportedRegion = Assert.Single(exportedRegions);
            Assert.Equal("Zone A Region", exportedRegion.Name);
            Assert.Equal(zoneA, exportedRegion.CameraZoneId);
        }
        finally
        {
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    [Fact]
    public async Task ImportCameraRegionsAsync_AddsAllFileRegionsToTargetCameraZoneWithNewIds()
    {
        var sourceZone = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("source-zone");
        var targetZone = new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId("target-zone");
        var importPath = Path.Combine(Path.GetTempPath(), $"objecttracker-camera-regions-{Guid.NewGuid()}.json");

        try
        {
            var sourceRegion = new ObjectTracker.UI.Desktop.Region.Model.RegionDefinition(
                Id: Guid.NewGuid(),
                Name: "Imported Region",
                Type: ObjectTracker.UI.Desktop.Region.Model.RegionType.EnterCrossroadRegion,
                CameraZoneId: sourceZone,
                Cells: new[] { new ObjectTracker.UI.Desktop.Region.Model.GridCell(7, 8) },
                CreatedAt: DateTime.UtcNow.AddDays(-1),
                UpdatedAt: DateTime.UtcNow.AddDays(-1),
                OverlappingZoneIds: new[] { "other-zone" });

            var sourcePersistence = new RegionPersistence(importPath);
            await sourcePersistence.SaveAsync(new[] { sourceRegion });

            var persistence = new RegionPersistence(_testFilePath);
            await persistence.SaveAsync(Array.Empty<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition>());

            var importedRegions = await persistence.ImportCameraRegionsAsync(importPath, targetZone);

            var importedRegion = Assert.Single(importedRegions);
            Assert.NotEqual(sourceRegion.Id, importedRegion.Id);
            Assert.Equal("Imported Region", importedRegion.Name);
            Assert.Equal(targetZone, importedRegion.CameraZoneId);
            Assert.Null(importedRegion.OverlappingZoneIds);

            var loadedRegion = Assert.Single(await persistence.LoadAsync());
            Assert.Equal(importedRegion.Id, loadedRegion.Id);
            Assert.Equal(targetZone, loadedRegion.CameraZoneId);
        }
        finally
        {
            if (File.Exists(importPath))
            {
                File.Delete(importPath);
            }
        }
    }
}
