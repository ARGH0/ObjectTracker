using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.UI.Desktop.Region.Model;
using EnrichedTrainState = ObjectTracker.UI.Desktop.Region.Contracts.EnrichedTrainState;
using RegionDefinition = ObjectTracker.UI.Desktop.Region.Model.RegionDefinition;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;
using TrainDetection = ObjectTracker.UI.Desktop.Region.Contracts.TrainDetection;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class RegionEvaluator : ObjectTracker.UI.Desktop.Region.Contracts.IRegionEvaluator
{
    private readonly ObjectTracker.UI.Desktop.Region.Contracts.ICoordinateMapper _mapper;
    private readonly Dictionary<Guid, GridCell> _previousCells = new();

    public RegionEvaluator(ObjectTracker.UI.Desktop.Region.Contracts.ICoordinateMapper mapper)
    {
        _mapper = mapper;
    }

    public EnrichedTrainState Evaluate(TrainDetection detection, string zoneId, IEnumerable<RegionDefinition> regions)
    {
        var imageWidth = detection.ImageWidth > 0 ? detection.ImageWidth : 640;
        var imageHeight = detection.ImageHeight > 0 ? detection.ImageHeight : 480;
        var cell = _mapper.MapPixelToCell(detection.PixelX, detection.PixelY, detection.GridCols, detection.GridRows, imageWidth, imageHeight);
        var matchingRegions = regions.Where(r => r.Cells.Any(c => c.Column == cell.Column && c.Row == cell.Row)).ToList();
        var resolved = matchingRegions.Count != 0 ? matchingRegions.Select(r => (r, r.Type)).ToList() : new List<(RegionDefinition, RegionType)>();
        var activeType = resolved.Count != 0 ? (RegionType?)resolved.First().Item2 : null;

        float confidence = detection.Confidence;
        if (activeType == RegionType.HighProbabilityRailRegion && resolved.Count > 0)
        {
            confidence = detection.Confidence + resolved.First().Item1.ConfidenceBoost;
        }

        var transitionEvents = new List<string>();

        GridCell? previousCell = null;
        bool wasInEnterCrossroadBefore = false;
        if (_previousCells.TryGetValue(detection.GlobalTrainId, out var prev))
        {
            previousCell = prev;
            var prevMatchingRegions = regions.Where(r => r.Cells.Any(c => c.Column == previousCell.Value.Column && c.Row == previousCell.Value.Row)).ToList();
            wasInEnterCrossroadBefore = prevMatchingRegions.Any(r => r.Type == RegionType.EnterCrossroadRegion);
        }

        bool isCurrentlyInEnterCrossroad = matchingRegions.Any(r => r.Type == RegionType.EnterCrossroadRegion);
        if (isCurrentlyInEnterCrossroad)
        {
            var enterRegion = matchingRegions.First(r => r.Type == RegionType.EnterCrossroadRegion);
            var previousCellText = previousCell.HasValue
                ? $"({previousCell.Value.Column},{previousCell.Value.Row})"
                : "unknown";
            transitionEvents.Add($"TrainEnteredCrossroad: trainId={detection.GlobalTrainId}, zoneId={zoneId}, regionName={enterRegion.Name}, timestamp={DateTime.UtcNow.Ticks}, previousCell={previousCellText}, newCell=({cell.Column},{cell.Row})");
        }

        bool wasInExitCrossroadBefore = false;
        if (_previousCells.TryGetValue(detection.GlobalTrainId, out var prevExit))
        {
            var prevExitMatchingRegions = regions.Where(r => r.Cells.Any(c => c.Column == prevExit.Column && c.Row == prevExit.Row)).ToList();
            wasInExitCrossroadBefore = prevExitMatchingRegions.Any(r => r.Type == RegionType.ExitCrossroadRegion);
        }

        bool isCurrentlyInExitCrossroad = matchingRegions.Any(r => r.Type == RegionType.ExitCrossroadRegion);
        GridCell? previousCellForExit = null;
        if (_previousCells.TryGetValue(detection.GlobalTrainId, out var prevExitCell))
        {
            previousCellForExit = prevExitCell;
        }

        if (wasInExitCrossroadBefore && !isCurrentlyInExitCrossroad && previousCellForExit.HasValue)
        {
            var exitRegion = regions.First(r => r.Type == RegionType.ExitCrossroadRegion && r.Cells.Any(c => c.Column == previousCellForExit.Value.Column && c.Row == previousCellForExit.Value.Row));
            transitionEvents.Add($"TrainExitedCrossroad: trainId={detection.GlobalTrainId}, zoneId={zoneId}, regionName={exitRegion.Name}, timestamp={DateTime.UtcNow.Ticks}, previousCell=({previousCellForExit.Value.Column},{previousCellForExit.Value.Row}), newCell=({cell.Column},{cell.Row})");
        }

        var activeRegionName = resolved.Count != 0 ? resolved.First().Item1.Name : null;

        _previousCells[detection.GlobalTrainId] = cell;

        return new EnrichedTrainState(
            detection.GlobalTrainId,
            detection.LocalTrainId,
            detection.TrainColor,
            detection.PixelX,
            detection.PixelY,
            confidence,
            detection.MotionState,
            activeType == RegionType.ExcludeRegion,
            activeType,
            transitionEvents,
            activeRegionName);
    }
}
