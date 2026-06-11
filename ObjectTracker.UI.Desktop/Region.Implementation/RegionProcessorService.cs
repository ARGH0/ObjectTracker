using System;
using EnrichedTrainState = ObjectTracker.UI.Desktop.Region.Contracts.EnrichedTrainState;
using TrainDetection = ObjectTracker.UI.Desktop.Region.Contracts.TrainDetection;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionProcessorService : ObjectTracker.UI.Desktop.Region.Contracts.IRegionProcessorService
{
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionEvaluator _evaluator;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IHandoffResolver _handoffResolver;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry _registry;

    public RegionProcessorService(
        ObjectTracker.UI.Desktop.Region.Contracts.IRegionEvaluator evaluator,
        ObjectTracker.UI.Desktop.Region.Contracts.IHandoffResolver handoffResolver,
        ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry registry)
    {
        _evaluator = evaluator;
        _handoffResolver = handoffResolver;
        _registry = registry;
    }

    public EnrichedTrainState? ProcessDetection(TrainDetection detection, string zoneId)
    {
        var regions = _registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zoneId));
        return _evaluator.Evaluate(detection, zoneId, regions);
    }
}
