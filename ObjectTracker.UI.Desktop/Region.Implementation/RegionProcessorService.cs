using System;
using System.Collections.Generic;
using System.Linq;
using TrainDetection = ObjectTracker.UI.Desktop.Region.Contracts.TrainDetection;
using EnrichedTrainState = ObjectTracker.UI.Desktop.Region.Contracts.EnrichedTrainState;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionProcessorService : ObjectTracker.UI.Desktop.Region.Contracts.IRegionProcessorService
{
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionEvaluator _evaluator;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IHandoffResolver _handoffResolver;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry _registry;
    private readonly List<EnrichedTrainState> _lastStates = new();

    public RegionProcessorService(
        ObjectTracker.UI.Desktop.Region.Contracts.IRegionEvaluator evaluator,
        ObjectTracker.UI.Desktop.Region.Contracts.IHandoffResolver handoffResolver,
        ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry registry)
    {
        _evaluator = evaluator;
        _handoffResolver = handoffResolver;
        _registry = registry;
    }

    public void ProcessDetection(TrainDetection detection, string zoneId)
    {
        var regions = _registry.GetByZone(new ObjectTracker.UI.Desktop.Region.Model.CameraZoneId(zoneId));
        var enriched = _evaluator.Evaluate(detection, zoneId, regions);
        _handoffResolver.RegisterDetection(zoneId, detection.LocalTrainId, detection.TrainColor, DateTime.UtcNow, detection.Confidence);
        if (!enriched.IsExcluded)
        {
            _lastStates.Add(enriched);
        }
    }

    public IEnumerable<EnrichedTrainState> GetLastProcessedStates() => _lastStates;
}
