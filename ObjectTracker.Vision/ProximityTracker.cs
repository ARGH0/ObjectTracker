using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

/// <summary>
/// Tracks detected objects across frames by matching detections to existing tracks
/// based on proximity and object kind, rather than relying on detector-assigned IDs.
/// This ensures stable track IDs even when the detector's per-frame contour order changes.
/// </summary>
public sealed class ProximityTracker : ITracker
{
    private const float DefaultMaxMatchDistancePx = 100f;
    private const int DefaultMaxMissedFrames = 5;

    private readonly float _maxMatchDistancePx;
    private readonly int _maxMissedFrames;

    private readonly Dictionary<string, TrackRecord> _activeTracks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _nextIdByKind = new(StringComparer.OrdinalIgnoreCase);
    private long _currentFrame;

    public ProximityTracker(
        float maxMatchDistancePx = DefaultMaxMatchDistancePx,
        int maxMissedFrames = DefaultMaxMissedFrames)
    {
        _maxMatchDistancePx = maxMatchDistancePx;
        _maxMissedFrames = maxMissedFrames;
    }

    public IReadOnlyList<TrackState> Update(IReadOnlyList<Detection> detections)
    {
        _currentFrame++;

        var detectionsByKind = detections
            .GroupBy(d => d.Kind, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var activeTracksByKind = _activeTracks.Values
            .GroupBy(t => t.Kind, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var matchedTrackIds = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<TrackState>(detections.Count);

        foreach (var (kind, kindDetections) in detectionsByKind)
        {
            var candidates = activeTracksByKind.TryGetValue(kind, out var tracks)
                ? tracks
                : [];

            foreach (var detection in kindDetections)
            {
                TrackRecord? bestMatch = null;
                var bestDistance = float.MaxValue;

                foreach (var track in candidates)
                {
                    if (matchedTrackIds.Contains(track.TrackId))
                        continue;

                    var dx = detection.X - track.X;
                    var dy = detection.Y - track.Y;
                    var dist = MathF.Sqrt((dx * dx) + (dy * dy));

                    if (dist < bestDistance && dist <= _maxMatchDistancePx)
                    {
                        bestDistance = dist;
                        bestMatch = track;
                    }
                }

                string trackId;
                float speed = 0f;
                float direction = 0f;

                if (bestMatch is not null)
                {
                    trackId = bestMatch.TrackId;
                    matchedTrackIds.Add(trackId);

                    var dtMs = Math.Max(1, Math.Abs(detection.TimestampUtcMs - bestMatch.TimestampUtcMs));
                    var dx = detection.X - bestMatch.X;
                    var dy = detection.Y - bestMatch.Y;
                    var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                    speed = distance / (dtMs / 1000f);
                    direction = MathF.Atan2(dy, dx) * (180f / MathF.PI);
                }
                else
                {
                    trackId = AssignNewId(kind);
                }

                results.Add(new TrackState(
                    trackId,
                    detection.X,
                    detection.Y,
                    speed,
                    direction,
                    detection.SourceId,
                    detection.TimestampUtcMs));

                _activeTracks[trackId] = new TrackRecord(
                    trackId,
                    kind,
                    detection.X,
                    detection.Y,
                    detection.TimestampUtcMs,
                    _currentFrame);
            }
        }

        // Evict tracks that have not been matched for too many consecutive frames.
        var staleIds = _activeTracks
            .Where(kv => _currentFrame - kv.Value.LastSeenFrame > _maxMissedFrames)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in staleIds)
        {
            _activeTracks.Remove(id);
        }

        return results;
    }

    public void Reset()
    {
        _activeTracks.Clear();
        _nextIdByKind.Clear();
        _currentFrame = 0;
    }

    private string AssignNewId(string kind)
    {
        var lowerKind = kind.ToLowerInvariant();
        _nextIdByKind.TryGetValue(lowerKind, out var n);
        n++;
        _nextIdByKind[lowerKind] = n;
        return $"{lowerKind}-{n}";
    }

    private sealed record TrackRecord(
        string TrackId,
        string Kind,
        float X,
        float Y,
        long TimestampUtcMs,
        long LastSeenFrame);
}
