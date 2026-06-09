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
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.ICoordinateMapper _mapper;
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.IRegionPriorityResolver _priorityResolver;
    private readonly Dictionary<Guid, GridCell> _previousCells = new();

    public RegionEvaluator(ObjectTracker.UI.Desktop.Region.Contracts.ICoordinateMapper mapper, ObjectTracker.UI.Desktop.Region.Contracts.IRegionPriorityResolver priorityResolver)
    {
        _mapper = mapper;
        _priorityResolver = priorityResolver;
    }

    public EnrichedTrainState Evaluate(TrainDetection detection, string zoneId, IEnumerable<RegionDefinition> regions)
    {
        var imageWidth = detection.ImageWidth > 0 ? detection.ImageWidth : 640;
        var imageHeight = detection.ImageHeight > 0 ? detection.ImageHeight : 480;
        var cell = _mapper.MapPixelToCell(detection.PixelX, detection.PixelY, detection.GridCols, detection.GridRows, imageWidth, imageHeight);
        var matchingRegions = regions.Where(r => r.Cells.Any(c => c.Column == cell.Column && c.Row == cell.Row)).ToList();
        var resolved = matchingRegions.Any() ? _priorityResolver.ResolveAll(matchingRegions).ToList() : new List<(RegionDefinition, RegionType)>();
        var activeType = resolved.Any() ? (RegionType?)resolved.First().Item2 : null;

        float confidence = detection.Confidence;
        if (activeType == RegionType.HighProbabilityRailRegion && resolved.Count > 0)
        {
            confidence = detection.Confidence + resolved.First().Item1.ConfidenceBoost;
        }

        var transitionEvents = new List<string>();

        GridCell? previousCell = null;
        bool wasInEnterCrossroadBefore = false;
        if (_previousCells.TryGetValue(detection.LocalTrainId, out var prev))
        {
            previousCell = prev;
            var prevMatchingRegions = regions.Where(r => r.Cells.Any(c => c.Column == previousCell.Value.Column && c.Row == previousCell.Value.Row)).ToList();
            wasInEnterCrossroadBefore = prevMatchingRegions.Any(r => r.Type == RegionType.EnterCrossroadRegion);
        }

        bool isCurrentlyInEnterCrossroad = matchingRegions.Any(r => r.Type == RegionType.EnterCrossroadRegion);
        if (isCurrentlyInEnterCrossroad && previousCell.HasValue && !wasInEnterCrossroadBefore)
        {
            transitionEvents.Add($"TrainEnteredCrossroad: trainId={detection.LocalTrainId}, zoneId={zoneId}, timestamp={DateTime.UtcNow.Ticks}, previousCell=({previousCell.Value.Column},{previousCell.Value.Row}), newCell=({cell.Column},{cell.Row})");
        }

        _previousCells[detection.LocalTrainId] = cell;

        return new EnrichedTrainState(
            detection.LocalTrainId,
            detection.TrainColor,
            detection.PixelX,
            detection.PixelY,
            confidence,
            detection.MotionState,
            activeType == RegionType.ExcludeRegion,
            activeType,
            transitionEvents);
    }
}
