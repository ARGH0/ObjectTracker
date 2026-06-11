using System;
using System.Collections.Generic;
using RegionType = ObjectTracker.UI.Desktop.Region.Model.RegionType;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public readonly record struct TrainDetection(
    Guid LocalTrainId,
    string TrainColor,
    float PixelX,
    float PixelY,
    float ProcessWidth,
    float ProcessHeight,
    int GridCols,
    int GridRows,
    float Confidence,
    string MotionState,
    int ImageWidth,
    int ImageHeight);

public readonly record struct EnrichedTrainState(
    Guid LocalTrainId,
    string TrainColor,
    float PixelX,
    float PixelY,
    float Confidence,
    string MotionState,
    bool IsExcluded,
    RegionType? ActiveRegionType,
    IReadOnlyCollection<string> TransitionEvents,
    string? ActiveRegionName);

public interface IRegionEvaluator
{
    EnrichedTrainState Evaluate(TrainDetection detection, string zoneId, IEnumerable<ObjectTracker.UI.Desktop.Region.Model.RegionDefinition> regions);
}
