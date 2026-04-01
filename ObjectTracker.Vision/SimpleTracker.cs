using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class SimpleTracker : ITracker
{
    private readonly Dictionary<string, TrackState> _previous = new();

    public IReadOnlyList<TrackState> Update(IReadOnlyList<Detection> detections)
    {
        var tracks = new List<TrackState>(detections.Count);

        foreach (var detection in detections)
        {
            var id = string.IsNullOrWhiteSpace(detection.Id)
                ? $"{detection.Kind}-{detection.SourceId}"
                : detection.Id;

            var speed = 0f;
            var direction = 0f;

            if (_previous.TryGetValue(id, out var previous))
            {
                var dtMs = Math.Max(1, detection.TimestampUtcMs - previous.TimestampUtcMs);
                var dx = detection.X - previous.X;
                var dy = detection.Y - previous.Y;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                speed = distance / (dtMs / 1000f);
                direction = MathF.Atan2(dy, dx) * (180f / MathF.PI);
            }

            var state = new TrackState(
                id,
                detection.X,
                detection.Y,
                speed,
                direction,
                detection.SourceId,
                detection.TimestampUtcMs);

            tracks.Add(state);
            _previous[id] = state;
        }

        return tracks;
    }

    public void Reset() => _previous.Clear();
}