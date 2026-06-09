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
    private readonly List<EnrichedTrainState> _lastStates = new();

    public RegionProcessorService(ObjectTracker.UI.Desktop.Region.Contracts.IRegionEvaluator evaluator, ObjectTracker.UI.Desktop.Region.Contracts.IHandoffResolver handoffResolver)
    {
        _evaluator = evaluator;
        _handoffResolver = handoffResolver;
    }

    public void ProcessDetection(TrainDetection detection)
    {
        _handoffResolver.RegisterDetection("zone-default", detection.LocalTrainId, detection.TrainColor, DateTime.UtcNow, detection.Confidence);
    }

    public IEnumerable<EnrichedTrainState> GetLastProcessedStates() => _lastStates;
}
