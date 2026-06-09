using System;
using System.Collections.Generic;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public readonly record struct HandoffEvent(
    string SourceZoneId,
    string TargetZoneId,
    Guid LocalTrainId,
    string ObjectSignature,
    DateTime SourceTimestamp,
    DateTime TargetTimestamp,
    float SourceConfidence,
    float TargetConfidence);

public interface IHandoffResolver
{
    void RegisterDetection(string zoneId, Guid localTrainId, string objectSignature, DateTime timestamp, float confidence);
    IEnumerable<HandoffEvent> Resolve();
}
