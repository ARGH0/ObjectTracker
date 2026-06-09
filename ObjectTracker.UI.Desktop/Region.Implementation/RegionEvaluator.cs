using System;
using System.Collections.Generic;
using System.Linq;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;
using TrainDetection = ObjectTracker.UI.Desktop.Region.Contracts.TrainDetection;
using EnrichedTrainState = ObjectTracker.UI.Desktop.Region.Contracts.EnrichedTrainState;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionEvaluator : ObjectTracker.UI.Desktop.Region.Contracts.IRegionEvaluator
{
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry _registry;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.ICoordinateMapper _mapper;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionPriorityResolver _priorityResolver;

    public RegionEvaluator(ObjectTracker.UI.Desktop.Region.Contracts.IRegionRegistry registry, ObjectTracker.UI.Desktop.Region.Contracts.ICoordinateMapper mapper, ObjectTracker.UI.Desktop.Region.Contracts.IRegionPriorityResolver priorityResolver)
    {
        _registry = registry;
        _mapper = mapper;
        _priorityResolver = priorityResolver;
    }

    public EnrichedTrainState Evaluate(TrainDetection detection, IEnumerable<RegionDefinition> regions)
    {
        var cell = _mapper.Map(detection.PixelX, detection.ProcessWidth, detection.PixelY, detection.ProcessHeight, detection.GridCols, detection.GridRows);
        var matchingRegions = regions.Where(r => r.Cells.Any(c => c.Column == cell.Column && c.Row == cell.Row));
        var activeType = matchingRegions.Any() ? _priorityResolver.Resolve(matchingRegions) : (RegionType?)null;

        return new EnrichedTrainState(
            detection.LocalTrainId,
            detection.TrainColor,
            detection.PixelX,
            detection.PixelY,
            detection.Confidence,
            detection.MotionState,
            activeType == RegionType.ExcludeRegion,
            activeType,
            new List<string>());
    }
}
