using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal sealed class BackgroundTrackingSampleProcessor : IOpenCvSampleProcessor
{
    private BackgroundSubtractorMOG2 _backgroundSubtractor = CreateBackgroundSubtractor();
    private readonly EuclideanDistanceTracker _tracker = new();

    public OpenCvSampleMode Mode => OpenCvSampleMode.BackgroundTracking;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var annotated = source.Clone();
        using var foregroundMask = new Mat();
        using var thresholdMask = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));

        _backgroundSubtractor.Apply(source, foregroundMask);
        Cv2.Threshold(foregroundMask, thresholdMask, 254, 255, ThresholdTypes.Binary);
        Cv2.MorphologyEx(thresholdMask, thresholdMask, MorphTypes.Open, kernel);
        Cv2.Dilate(thresholdMask, thresholdMask, kernel, iterations: 2);

        Cv2.FindContours(thresholdMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var detections = new List<Rect>();
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 400)
            {
                continue;
            }

            var bounds = Cv2.BoundingRect(contour);
            if (bounds.Width < 24 || bounds.Height < 24)
            {
                continue;
            }

            detections.Add(bounds);
        }

        var trackedObjects = _tracker.Update(detections);
        var details = new List<string>
        {
            $"Foreground candidates: {detections.Count}",
            $"Tracked objects: {trackedObjects.Count}"
        };

        foreach (var trackedObject in trackedObjects.Take(12))
        {
            var rect = trackedObject.Bounds;
            Cv2.Rectangle(annotated, rect, new Scalar(44, 181, 93), 3);
            Cv2.PutText(
                annotated,
                $"ID {trackedObject.Id}",
                new Point(rect.X, Math.Max(24, rect.Y - 8)),
                HersheyFonts.HersheySimplex,
                0.8,
                new Scalar(44, 181, 93),
                2,
                LineTypes.AntiAlias);

            details.Add($"ID {trackedObject.Id}: {rect.Width}x{rect.Height} at ({rect.X}, {rect.Y})");
        }

        if (trackedObjects.Count > 12)
        {
            details.Add($"... plus {trackedObjects.Count - 12} more tracked object(s)");
        }

        return OpenCvSampleProcessingHelpers.CreateResult(
            $"Processed {sourceName} with the background tracking sample.",
            details,
            annotated);
    }

    public void Reset()
    {
        _backgroundSubtractor.Dispose();
        _backgroundSubtractor = CreateBackgroundSubtractor();
        _tracker.Reset();
    }

    private static BackgroundSubtractorMOG2 CreateBackgroundSubtractor()
    {
        return BackgroundSubtractorMOG2.Create(history: 100, varThreshold: 40, detectShadows: true);
    }

    private sealed class EuclideanDistanceTracker
    {
        private readonly Dictionary<int, Point> _centerPoints = new();
        private int _nextId;

        public IReadOnlyList<TrackedObject> Update(IEnumerable<Rect> detections)
        {
            var trackedObjects = new List<TrackedObject>();
            var updatedCenters = new Dictionary<int, Point>();

            foreach (var detection in detections)
            {
                var center = new Point(detection.X + detection.Width / 2, detection.Y + detection.Height / 2);
                var matchedId = -1;

                foreach (var pair in _centerPoints)
                {
                    var distance = Math.Sqrt(Math.Pow(center.X - pair.Value.X, 2) + Math.Pow(center.Y - pair.Value.Y, 2));
                    if (distance < 35)
                    {
                        matchedId = pair.Key;
                        break;
                    }
                }

                if (matchedId < 0)
                {
                    matchedId = _nextId++;
                }

                updatedCenters[matchedId] = center;
                trackedObjects.Add(new TrackedObject(matchedId, detection));
            }

            _centerPoints.Clear();
            foreach (var pair in updatedCenters)
            {
                _centerPoints[pair.Key] = pair.Value;
            }

            return trackedObjects;
        }

        public void Reset()
        {
            _centerPoints.Clear();
            _nextId = 0;
        }
    }

    private sealed record TrackedObject(int Id, Rect Bounds);
}

internal sealed class TrainCollisionRiskSampleProcessor : IOpenCvSampleProcessor
{
    private const int HistoryLength = 12;
    private const int PredictionFrames = 18;
    private const double CollisionPadding = 24d;

    private BackgroundSubtractorMOG2 _backgroundSubtractor = CreateBackgroundSubtractor();
    private readonly TrainTracker _tracker = new();
    private int _frameCounter;

    public OpenCvSampleMode Mode => OpenCvSampleMode.TrainCollisionRisk;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        _frameCounter++;

        using var annotated = source.Clone();
        using var foregroundMask = new Mat();
        using var thresholdMask = new Mat();
        using var cleanedMask = new Mat();
        using var gray = new Mat();
        using var horizontalKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 3));
        using var cleanupKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));

        _backgroundSubtractor.Apply(source, foregroundMask);
        Cv2.Threshold(foregroundMask, thresholdMask, 220, 255, ThresholdTypes.Binary);
        Cv2.MorphologyEx(thresholdMask, cleanedMask, MorphTypes.Open, cleanupKernel);
        Cv2.Dilate(cleanedMask, cleanedMask, horizontalKernel, iterations: 2);

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.FindContours(cleanedMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var detections = new List<Rect>();
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 900)
            {
                continue;
            }

            var bounds = Cv2.BoundingRect(contour);
            if (bounds.Width < 36 || bounds.Height < 18)
            {
                continue;
            }

            var aspectRatio = bounds.Width / (double)Math.Max(1, bounds.Height);
            if (aspectRatio < 1.1 || aspectRatio > 8.5)
            {
                continue;
            }

            var region = new Mat(gray, bounds);
            try
            {
                var mean = Cv2.Mean(region).Val0;
                if (mean < 18 || mean > 245)
                {
                    continue;
                }
            }
            finally
            {
                region.Dispose();
            }

            detections.Add(bounds);
        }

        var trackedTrains = _tracker.Update(detections);
        var collisionRisks = PredictCollisionRisks(trackedTrains);

        var details = new List<string>
        {
            $"Frame: {_frameCounter}",
            $"Train candidates: {detections.Count}",
            $"Tracked trains: {trackedTrains.Count}"
        };

        if (_frameCounter < 6)
        {
            details.Add("Tracker is warming up. Use a short video clip from a fixed camera for better estimates.");
        }

        foreach (var train in trackedTrains.OrderBy(track => track.Id).Take(10))
        {
            var color = collisionRisks.Any(risk => risk.Involves(train.Id))
                ? new Scalar(52, 92, 227)
                : new Scalar(48, 176, 93);

            Cv2.Rectangle(annotated, train.Bounds, color, 3);
            Cv2.PutText(
                annotated,
                $"T{train.Id} v={train.Speed:F1}",
                new Point(train.Bounds.X, Math.Max(24, train.Bounds.Y - 8)),
                HersheyFonts.HersheySimplex,
                0.7,
                color,
                2,
                LineTypes.AntiAlias);

            if (train.Velocity.X != 0 || train.Velocity.Y != 0)
            {
                var start = train.Center;
                var end = new Point(
                    (int)Math.Round(start.X + train.Velocity.X * 5),
                    (int)Math.Round(start.Y + train.Velocity.Y * 5));
                Cv2.ArrowedLine(annotated, start, end, color, 2, LineTypes.AntiAlias, 0, 0.18);
            }

            details.Add($"Train {train.Id}: {train.Bounds.Width}x{train.Bounds.Height} at ({train.Bounds.X}, {train.Bounds.Y}), speed {train.Speed:F1} px/frame");
        }

        if (trackedTrains.Count > 10)
        {
            details.Add($"... plus {trackedTrains.Count - 10} more tracked train(s)");
        }

        foreach (var risk in collisionRisks.Take(4))
        {
            Cv2.Line(annotated, risk.FirstCenter, risk.SecondCenter, new Scalar(35, 74, 214), 2, LineTypes.AntiAlias);
            Cv2.Circle(annotated, risk.ProjectedMidpoint, 8, new Scalar(35, 74, 214), 3, LineTypes.AntiAlias);
            details.Add($"Collision risk: train {risk.FirstId} and train {risk.SecondId} in ~{risk.FramesUntilImpact:0.0} frame(s), gap {risk.ProjectedGap:F1}px");
        }

        if (collisionRisks.Count > 4)
        {
            details.Add($"... plus {collisionRisks.Count - 4} more collision-risk pair(s)");
        }

        var status = collisionRisks.Count switch
        {
            > 0 => $"Processed {sourceName} with train collision monitoring. {collisionRisks.Count} collision risk(s) predicted.",
            _ when trackedTrains.Count > 0 => $"Processed {sourceName} with train collision monitoring. No near-term collision risk detected.",
            _ => $"Processed {sourceName} with train collision monitoring. No train-like motion detected yet."
        };

        return OpenCvSampleProcessingHelpers.CreateResult(status, details, annotated);
    }

    public void Reset()
    {
        _backgroundSubtractor.Dispose();
        _backgroundSubtractor = CreateBackgroundSubtractor();
        _tracker.Reset();
        _frameCounter = 0;
    }

    private static BackgroundSubtractorMOG2 CreateBackgroundSubtractor()
    {
        return BackgroundSubtractorMOG2.Create(history: 120, varThreshold: 24, detectShadows: false);
    }

    private static IReadOnlyList<CollisionRisk> PredictCollisionRisks(IReadOnlyList<TrainTrack> trackedTrains)
    {
        var risks = new List<CollisionRisk>();

        for (var i = 0; i < trackedTrains.Count; i++)
        {
            var first = trackedTrains[i];
            if (!first.HasPrediction)
            {
                continue;
            }

            for (var j = i + 1; j < trackedTrains.Count; j++)
            {
                var second = trackedTrains[j];
                if (!second.HasPrediction)
                {
                    continue;
                }

                var combinedReach = first.Radius + second.Radius + CollisionPadding;
                CollisionRisk? bestRisk = null;

                for (var frame = 1; frame <= PredictionFrames; frame++)
                {
                    var firstPoint = first.Project(frame);
                    var secondPoint = second.Project(frame);
                    var gap = Distance(firstPoint, secondPoint);
                    if (gap > combinedReach)
                    {
                        continue;
                    }

                    var midpoint = new Point(
                        (int)Math.Round((firstPoint.X + secondPoint.X) / 2d),
                        (int)Math.Round((firstPoint.Y + secondPoint.Y) / 2d));

                    bestRisk = new CollisionRisk(
                        first.Id,
                        second.Id,
                        first.Center,
                        second.Center,
                        midpoint,
                        frame,
                        gap);
                    break;
                }

                if (bestRisk is not null)
                {
                    risks.Add(bestRisk);
                }
            }
        }

        return risks.OrderBy(risk => risk.FramesUntilImpact).ThenBy(risk => risk.ProjectedGap).ToList();
    }

    private static double Distance(Point2d first, Point2d second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private sealed class TrainTracker
    {
        private readonly Dictionary<int, TrackState> _tracks = new();
        private int _nextId;

        public IReadOnlyList<TrainTrack> Update(IEnumerable<Rect> detections)
        {
            var results = new List<TrainTrack>();
            var usedTrackIds = new HashSet<int>();

            foreach (var detection in detections.OrderByDescending(rect => rect.Width * rect.Height))
            {
                var center = CenterOf(detection);
                var matchedTrack = _tracks
                    .Values
                    .Where(track => !usedTrackIds.Contains(track.Id))
                    .OrderBy(track => Distance(track.Center, center))
                    .FirstOrDefault(track => Distance(track.Center, center) < Math.Max(72, track.LastBounds.Width));

                if (matchedTrack is null)
                {
                    matchedTrack = new TrackState(_nextId++, detection);
                    _tracks[matchedTrack.Id] = matchedTrack;
                }
                else
                {
                    matchedTrack.Update(detection);
                }

                usedTrackIds.Add(matchedTrack.Id);
                results.Add(matchedTrack.ToTrainTrack());
            }

            var staleTrackIds = _tracks.Values
                .Where(track => !usedTrackIds.Contains(track.Id))
                .Select(track => track.MarkMissed())
                .Where(remove => remove)
                .Select(track => track.Id)
                .ToArray();

            foreach (var staleTrackId in staleTrackIds)
            {
                _tracks.Remove(staleTrackId);
            }

            return results;
        }

        public void Reset()
        {
            _tracks.Clear();
            _nextId = 0;
        }

        private static Point CenterOf(Rect rect)
        {
            return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        }

        private static double Distance(Point first, Point second)
        {
            var dx = first.X - second.X;
            var dy = first.Y - second.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    private sealed class TrackState
    {
        private readonly Queue<Point> _history = new();
        private int _missedFrames;

        public TrackState(int id, Rect initialBounds)
        {
            Id = id;
            LastBounds = initialBounds;
            Center = new Point(initialBounds.X + initialBounds.Width / 2, initialBounds.Y + initialBounds.Height / 2);
            _history.Enqueue(Center);
        }

        public int Id { get; }

        public Rect LastBounds { get; private set; }

        public Point Center { get; private set; }

        public Point2d Velocity { get; private set; }

        public void Update(Rect bounds)
        {
            LastBounds = bounds;
            var previousCenter = Center;
            Center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            _history.Enqueue(Center);

            while (_history.Count > HistoryLength)
            {
                _history.Dequeue();
            }

            var historyArray = _history.ToArray();
            if (historyArray.Length >= 2)
            {
                var oldest = historyArray[0];
                var newest = historyArray[^1];
                var steps = Math.Max(1, historyArray.Length - 1);
                Velocity = new Point2d((newest.X - oldest.X) / (double)steps, (newest.Y - oldest.Y) / (double)steps);
            }
            else
            {
                Velocity = new Point2d(Center.X - previousCenter.X, Center.Y - previousCenter.Y);
            }

            _missedFrames = 0;
        }

        public TrackState MarkMissed()
        {
            _missedFrames++;
            return this;
        }

        public TrainTrack ToTrainTrack()
        {
            var speed = Math.Sqrt(Velocity.X * Velocity.X + Velocity.Y * Velocity.Y);
            var radius = Math.Sqrt(Math.Pow(LastBounds.Width / 2d, 2) + Math.Pow(LastBounds.Height / 2d, 2));
            return new TrainTrack(Id, LastBounds, Center, Velocity, speed, radius, speed >= 1.5);
        }

        public static implicit operator bool(TrackState track)
        {
            return track._missedFrames > 8;
        }
    }

    private sealed record TrainTrack(int Id, Rect Bounds, Point Center, Point2d Velocity, double Speed, double Radius, bool HasPrediction)
    {
        public Point2d Project(int framesAhead)
        {
            return new Point2d(Center.X + Velocity.X * framesAhead, Center.Y + Velocity.Y * framesAhead);
        }
    }

    private sealed record CollisionRisk(int FirstId, int SecondId, Point FirstCenter, Point SecondCenter, Point ProjectedMidpoint, double FramesUntilImpact, double ProjectedGap)
    {
        public bool Involves(int trackId)
        {
            return FirstId == trackId || SecondId == trackId;
        }
    }
}