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