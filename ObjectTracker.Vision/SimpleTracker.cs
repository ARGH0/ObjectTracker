using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

public sealed class SimpleTracker : ITracker
{
    private const float MatchDistancePixels = 80f;
    private const float StationarySpeedThreshold = 6f;
    private const long UncertainTimeoutMs = 3000;
    private const long RemovalTimeoutMs = 10000;

    private readonly Dictionary<string, TrackedTrain> tracks = new (StringComparer.OrdinalIgnoreCase);
    private int nextTrackNumber = 1;

    public IReadOnlyList<TrainState> Update(IReadOnlyList<Detection> detections, long frameTimestampUtcMs)
    {
        var results = new List<TrainState>(tracks.Count + detections.Count);
        var matches = new Dictionary<string, Detection>(StringComparer.OrdinalIgnoreCase);
        var usedDetections = new HashSet<int>();

        foreach (var track in tracks.Values.OrderBy(track => track.State.LocalTrainId, StringComparer.OrdinalIgnoreCase))
        {
            var bestIndex = -1;
            var bestDistance = float.MaxValue;

            for (var index = 0; index < detections.Count; index++)
            {
                if (usedDetections.Contains(index))
                {
                    continue;
                }

                var detection = detections[index];
                if (!IsCompatible(track, detection))
                {
                    continue;
                }

                var dx = detection.X - track.State.X;
                var dy = detection.Y - track.State.Y;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = index;
                }
            }

            if (bestIndex >= 0 && bestDistance <= MatchDistancePixels)
            {
                var detection = detections[bestIndex];
                usedDetections.Add(bestIndex);
                matches[track.State.LocalTrainId] = detection;
            }
        }

        foreach (var track in tracks.Values)
        {
            if (matches.TryGetValue(track.State.LocalTrainId, out var detection))
            {
                UpdateTrackWithDetection(track, detection);
            }
            else
            {
                UpdateTrackWithoutDetection(track, frameTimestampUtcMs);
            }

            results.Add(track.State);
        }

        foreach (var index in Enumerable.Range(0, detections.Count))
        {
            if (usedDetections.Contains(index))
            {
                continue;
            }

            var detection = detections[index];
            var track = CreateTrack(detection);
            tracks[track.State.LocalTrainId] = track;
            results.Add(track.State);
        }

        CleanupStaleTracks(frameTimestampUtcMs);

        return results
            .OrderBy(state => state.LocalTrainId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Reset()
    {
        tracks.Clear();
        nextTrackNumber = 1;
    }

    private static bool IsCompatible(TrackedTrain track, Detection detection)
    {
        if (track.State.MotionState == TrainMotionState.GoneFromTrack)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(track.State.TrainColor) &&
            !string.IsNullOrWhiteSpace(detection.Kind) &&
            !track.State.TrainColor.Equals(detection.Kind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(track.State.SourceId, detection.SourceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private TrackedTrain CreateTrack(Detection detection)
    {
        var localTrainId = $"train-{nextTrackNumber++ :000}";
        var state = new TrainState(
            localTrainId,
            detection.Kind,
            detection.X,
            detection.Y,
            0f,
            0f,
            Math.Clamp(detection.Confidence, 0.25f, 1f),
            TrainMotionState.Uncertain,
            CollisionWarningState.None,
            detection.SourceId,
            detection.TimestampUtcMs);

        return new TrackedTrain(state);
    }

    private static void UpdateTrackWithDetection(TrackedTrain track, Detection detection)
    {
        var previous = track.State;
        var dtMs = Math.Max(1, detection.TimestampUtcMs - previous.TimestampUtcMs);
        var dx = detection.X - previous.X;
        var dy = detection.Y - previous.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var speed = distance / (dtMs / 1000f);
        var direction = MathF.Atan2(dy, dx) * (180f / MathF.PI);
        var motionState = speed >= StationarySpeedThreshold ? TrainMotionState.Moving : TrainMotionState.Stationary;

        track.State = previous with
        {
            TrainColor = string.IsNullOrWhiteSpace(previous.TrainColor) ? detection.Kind : previous.TrainColor,
            X = detection.X,
            Y = detection.Y,
            SpeedPixelsPerSecond = speed,
            DirectionDegrees = direction,
            Confidence = Math.Clamp(0.6f + detection.Confidence, 0f, 1f),
            MotionState = motionState,
            CollisionWarningState = CollisionWarningState.None,
            SourceId = detection.SourceId,
            TimestampUtcMs = detection.TimestampUtcMs
        };
        track.MissedFrameCount = 0;
        track.LastDetectionTimestampUtcMs = detection.TimestampUtcMs;
    }

    private static void UpdateTrackWithoutDetection(TrackedTrain track, long frameTimestampUtcMs)
    {
        var previous = track.State;
        var elapsedMs = Math.Max(0, frameTimestampUtcMs - previous.TimestampUtcMs);
        var confidenceLoss = Math.Max(0.05f, elapsedMs / 10000f);
        var confidence = Math.Clamp(previous.Confidence - confidenceLoss, 0f, 1f);

        var motionState = elapsedMs >= UncertainTimeoutMs
            ? TrainMotionState.GoneFromTrack
            : TrainMotionState.Uncertain;

        track.State = previous with
        {
            Confidence = confidence,
            MotionState = motionState,
            CollisionWarningState = CollisionWarningState.None,
            TimestampUtcMs = frameTimestampUtcMs
        };
        track.MissedFrameCount++;
    }

    private void CleanupStaleTracks(long frameTimestampUtcMs)
    {
        var staleTracks = tracks
            .Where(pair => pair.Value.State.MotionState == TrainMotionState.GoneFromTrack &&
                           frameTimestampUtcMs - pair.Value.LastDetectionTimestampUtcMs > RemovalTimeoutMs)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var trackId in staleTracks)
        {
            tracks.Remove(trackId);
        }
    }

    private sealed class TrackedTrain
    {
        public TrackedTrain(TrainState state)
        {
            State = state;
            LastDetectionTimestampUtcMs = state.TimestampUtcMs;
        }

        public TrainState State { get; set; }

        public long LastDetectionTimestampUtcMs { get; set; }

        public int MissedFrameCount { get; set; }
    }
}
