using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

/// <summary>
/// IoU-based multi-object tracker with velocity prediction and assignment.
/// Replaces SimpleTracker with a more robust association method.
/// </summary>
public sealed class IouTracker : ITracker
{
    private readonly Dictionary<int, TrackRecord> _tracks = new();
    private int _nextTrackId = 1;
    private const int MaxMissedFrames = 30;
    private const float IoUThreshold = 0.3f;

    public IReadOnlyList<TrackState> Update(IReadOnlyList<Detection> detections)
    {
        // Predict current positions for existing tracks
        var predictions = new Dictionary<int, PredictedTrack>();
        foreach (var (trackId, record) in _tracks)
        {
            // Simple velocity-based prediction
            var predictedX = record.LastX + record.VelocityX;
            var predictedY = record.LastY + record.VelocityY;
            predictions[trackId] = new PredictedTrack
            {
                TrackId = trackId,
                X = predictedX,
                Y = predictedY,
                W = record.LastW,
                H = record.LastH,
                Age = record.Age
            };
        }

        // Compute IoU matrix: predictions × detections
        var ious = ComputeIoUMatrix(predictions.Values.ToList(), detections);

        // Hungarian-style greedy assignment: match highest IoU first
        var assignments = GreedyAssign(ious, IoUThreshold);
        var usedTrackIds = new HashSet<int>();
        var usedDetectionIndices = new HashSet<int>();

        foreach (var (trackId, detectionIdx) in assignments)
        {
            usedTrackIds.Add(trackId);
            usedDetectionIndices.Add(detectionIdx);
        }

        // Update matched tracks
        var result = new List<TrackState>();
        foreach (var (trackId, detectionIdx) in assignments)
        {
            var detection = detections[detectionIdx];
            if (_tracks.TryGetValue(trackId, out var record))
            {
                var dtMs = Math.Max(1, detection.TimestampUtcMs - record.LastTimestampMs);
                var dx = detection.X - record.LastX;
                var dy = detection.Y - record.LastY;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var speed = distance / (dtMs / 1000f);
                var direction = MathF.Atan2(dy, dx) * (180f / MathF.PI);

                record.LastX = detection.X;
                record.LastY = detection.Y;
                record.LastW = detection.BoxWidth;
                record.LastH = detection.BoxHeight;
                record.VelocityX = dx;
                record.VelocityY = dy;
                record.LastTimestampMs = detection.TimestampUtcMs;
                record.MissedFrames = 0;
                record.Age++;

                result.Add(new TrackState(
                    $"track-{trackId}",
                    detection.X,
                    detection.Y,
                    speed,
                    direction,
                    detection.SourceId,
                    detection.TimestampUtcMs));
            }
        }

        // Create new tracks for unmatched detections
        foreach (int detIdx in Enumerable.Range(0, detections.Count))
        {
            if (usedDetectionIndices.Contains(detIdx))
                continue;

            var detection = detections[detIdx];
            var newTrackId = _nextTrackId++;
            _tracks[newTrackId] = new TrackRecord
            {
                LastX = detection.X,
                LastY = detection.Y,
                LastW = detection.BoxWidth,
                LastH = detection.BoxHeight,
                VelocityX = 0,
                VelocityY = 0,
                LastTimestampMs = detection.TimestampUtcMs,
                MissedFrames = 0,
                Age = 1
            };

            result.Add(new TrackState(
                $"track-{newTrackId}",
                detection.X,
                detection.Y,
                0,
                0,
                detection.SourceId,
                detection.TimestampUtcMs));
        }

        // Age out unmatched tracks
        var toRemove = new List<int>();
        foreach (var trackId in _tracks.Keys)
        {
            if (!usedTrackIds.Contains(trackId))
            {
                _tracks[trackId].MissedFrames++;
                if (_tracks[trackId].MissedFrames > MaxMissedFrames)
                {
                    toRemove.Add(trackId);
                }
            }
        }

        foreach (var trackId in toRemove)
        {
            _tracks.Remove(trackId);
        }

        return result;
    }

    public void Reset()
    {
        _tracks.Clear();
        _nextTrackId = 1;
    }

    private List<(int trackId, int detectionIdx)> GreedyAssign(float[,] ious, float threshold)
    {
        var assignments = new List<(int, int)>();
        var usedTracks = new HashSet<int>();
        var usedDetections = new HashSet<int>();

        // Sort by IoU descending and assign greedily
        var pairs = new List<(int t, int d, float iou)>();
        for (int t = 0; t < ious.GetLength(0); t++)
        {
            for (int d = 0; d < ious.GetLength(1); d++)
            {
                if (ious[t, d] > threshold)
                {
                    pairs.Add((t, d, ious[t, d]));
                }
            }
        }

        pairs.Sort((a, b) => b.iou.CompareTo(a.iou));

        foreach (var (t, d, _) in pairs)
        {
            if (!usedTracks.Contains(t) && !usedDetections.Contains(d))
            {
                assignments.Add((t, d));
                usedTracks.Add(t);
                usedDetections.Add(d);
            }
        }

        return assignments;
    }

    private float[,] ComputeIoUMatrix(List<PredictedTrack> predictions, IReadOnlyList<Detection> detections)
    {
        var matrix = new float[predictions.Count, detections.Count];
        for (int i = 0; i < predictions.Count; i++)
        {
            for (int j = 0; j < detections.Count; j++)
            {
                var pred = predictions[i];
                var det = detections[j];
                matrix[i, j] = ComputeIoU(
                    (pred.X - pred.W / 2, pred.Y - pred.H / 2, pred.X + pred.W / 2, pred.Y + pred.H / 2),
                    (det.BoxX, det.BoxY, det.BoxX + det.BoxWidth, det.BoxY + det.BoxHeight));
            }
        }

        return matrix;
    }

    private static float ComputeIoU((float x1, float y1, float x2, float y2) a, (float x1, float y1, float x2, float y2) b)
    {
        var ix1 = Math.Max(a.x1, b.x1);
        var iy1 = Math.Max(a.y1, b.y1);
        var ix2 = Math.Min(a.x2, b.x2);
        var iy2 = Math.Min(a.y2, b.y2);

        var inter = Math.Max(0, ix2 - ix1) * Math.Max(0, iy2 - iy1);
        var areaA = (a.x2 - a.x1) * (a.y2 - a.y1);
        var areaB = (b.x2 - b.x1) * (b.y2 - b.y1);
        var union = areaA + areaB - inter;

        return union > 0 ? inter / union : 0;
    }

    private class TrackRecord
    {
        public float LastX { get; set; }
        public float LastY { get; set; }
        public float LastW { get; set; }
        public float LastH { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public long LastTimestampMs { get; set; }
        public int MissedFrames { get; set; }
        public int Age { get; set; }
    }

    private class PredictedTrack
    {
        public int TrackId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }
        public int Age { get; set; }
    }
}
