using System;
using System.Collections.Generic;
using System.Linq;
using HandoffEvent = ObjectTracker.UI.Desktop.Region.Contracts.HandoffEvent;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class HandoffResolver : ObjectTracker.UI.Desktop.Region.Contracts.IHandoffResolver
{
    private readonly Dictionary<string, List<(string zoneId, Guid localTrainId, string signature, DateTime timestamp, float confidence)>> _detections = new();

    public void RegisterDetection(string zoneId, Guid localTrainId, string objectSignature, DateTime timestamp, float confidence)
    {
        if (!_detections.ContainsKey(zoneId))
        {
            _detections[zoneId] = new List<(string, Guid, string, DateTime, float)>();
        }
        _detections[zoneId].Add((zoneId, localTrainId, objectSignature, timestamp, confidence));
    }

    public IEnumerable<HandoffEvent> Resolve()
    {
        return Enumerable.Empty<HandoffEvent>();
    }
}
