using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;
using Xunit;
using CameraZoneId = ObjectTracker.UI.Desktop.Region.Model.CameraZoneId;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class RegionProcessorServiceTests
{
    private static RegionDefinition CreateRegion(RegionType type, params (int Col, int Row)[] cells)
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var gridCells = new List<ObjectTracker.UI.Desktop.Region.Model.GridCell>();
        foreach (var (col, row) in cells)
        {
            gridCells.Add(new ObjectTracker.UI.Desktop.Region.Model.GridCell(col, row));
        }
        return new RegionDefinition(
            Guid.NewGuid(),
            "test-region",
            type,
            new CameraZoneId("zone-1"),
            gridCells.AsReadOnly(),
            baseTime,
            baseTime,
            null);
    }

    private static TrainDetection CreateDetection(Guid trainId, float pixelX, float pixelY)
    {
        return new TrainDetection(
            trainId,
            "Red",
            pixelX,
            pixelY,
            50f,
            30f,
            64,
            48,
            0.9f,
            "moving",
            640,
            480);
    }

    [Fact]
    public void ProcessDetection_ExcludedTrain_NotInProcessedStates()
    {
        var mapper = new CoordinateMapper();
        var resolver = new RegionPriorityResolver();
        var evaluator = new RegionEvaluator(mapper, resolver);

        var fakeRegistry = new FakeRegionRegistry();
        var excludeRegion = CreateRegion(RegionType.ExcludeRegion, (10, 20));
        fakeRegistry.Add(excludeRegion);

        var handoffResolver = new HandoffResolver();
        var processor = new RegionProcessorService(evaluator, handoffResolver, fakeRegistry);

        var trainId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var detection = CreateDetection(trainId, 100f, 200f);

        processor.ProcessDetection(detection, "zone-1");

        var states = processor.GetLastProcessedStates().ToList();
        Assert.Empty(states);
    }

    [Fact]
    public void ProcessDetection_NonExcludedTrain_PassesThrough()
    {
        var mapper = new CoordinateMapper();
        var resolver = new RegionPriorityResolver();
        var evaluator = new RegionEvaluator(mapper, resolver);

        var fakeRegistry = new FakeRegionRegistry();
        var excludeRegion = CreateRegion(RegionType.ExcludeRegion, (10, 20));
        fakeRegistry.Add(excludeRegion);

        var handoffResolver = new HandoffResolver();
        var processor = new RegionProcessorService(evaluator, handoffResolver, fakeRegistry);

        var trainId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var detection = CreateDetection(trainId, 50f, 100f);

        processor.ProcessDetection(detection, "zone-1");

        var states = processor.GetLastProcessedStates().ToList();
        Assert.Single(states);
        Assert.Equal(trainId, states[0].LocalTrainId);
        Assert.False(states[0].IsExcluded);
    }

    [Fact]
    public void ProcessDetection_MultipleExcludeRegions_UnionFiltersAll()
    {
        var mapper = new CoordinateMapper();
        var resolver = new RegionPriorityResolver();
        var evaluator = new RegionEvaluator(mapper, resolver);

        var fakeRegistry = new FakeRegionRegistry();
        var excludeRegion1 = CreateRegion(RegionType.ExcludeRegion, (10, 20));
        var excludeRegion2 = CreateRegion(RegionType.ExcludeRegion, (5, 10));
        fakeRegistry.Add(excludeRegion1);
        fakeRegistry.Add(excludeRegion2);

        var handoffResolver = new HandoffResolver();
        var processor = new RegionProcessorService(evaluator, handoffResolver, fakeRegistry);

        var trainId1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var trainId2 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var trainId3 = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var detection1 = CreateDetection(trainId1, 100f, 200f);
        var detection2 = CreateDetection(trainId2, 50f, 100f);
        var detection3 = CreateDetection(trainId3, 320f, 240f);

        processor.ProcessDetection(detection1, "zone-1");
        processor.ProcessDetection(detection2, "zone-1");
        processor.ProcessDetection(detection3, "zone-1");

        var states = processor.GetLastProcessedStates().ToList();
        Assert.Single(states);
        Assert.Equal(trainId3, states[0].LocalTrainId);
    }

    [Fact]
    public async Task ProcessDetection_IntegrationFullFlow_ExcludedTrainsFiltered()
    {
        var persistence = new FakeRegionPersistence();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var gridCells = new List<ObjectTracker.UI.Desktop.Region.Model.GridCell>
        {
            new(10, 20),
            new(11, 20)
        };
        var excludeRegion = new RegionDefinition(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "exclude-zone",
            RegionType.ExcludeRegion,
            new CameraZoneId("zone-integration"),
            gridCells.AsReadOnly(),
            baseTime,
            baseTime,
            null);
        await persistence.SaveAsync(new[] { excludeRegion });

        var registry = await ObjectTracker.UI.Desktop.Region.Implementation.RegionRegistry.CreateAsync(persistence);
        var mapper = new CoordinateMapper();
        var resolver = new RegionPriorityResolver();
        var evaluator = new RegionEvaluator(mapper, resolver);
        var handoffResolver = new HandoffResolver();
        var processor = new RegionProcessorService(evaluator, handoffResolver, registry);

        var trainId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var detection = CreateDetection(trainId, 100f, 200f);

        processor.ProcessDetection(detection, "zone-integration");

        var states = processor.GetLastProcessedStates().ToList();
        Assert.Empty(states);
    }
}
