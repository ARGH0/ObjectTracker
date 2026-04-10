using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal sealed class FastCornersSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.FastCorners;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var annotated = source.Clone();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        var keypoints = Cv2.FAST(gray, 50, true);

        foreach (var keyPoint in keypoints)
        {
            annotated.Circle((Point)keyPoint.Pt, 3, Scalar.Red, -1, LineTypes.AntiAlias);
        }

        var details = new[]
        {
            $"Keypoints: {keypoints.Length}",
            "Threshold: 50",
            "Nonmax suppression: enabled"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the FAST sample.", details, annotated);
    }
}

internal sealed class HogPeopleSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.HogPeople;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var annotated = source.Clone();
        using var hog = new HOGDescriptor();
        hog.SetSVMDetector(HOGDescriptor.GetDefaultPeopleDetector());

        var found = hog.DetectMultiScale(source, 0, new Size(8, 8), new Size(24, 16), 1.05, 2);
        var details = new List<string>
        {
            $"Detector size valid: {hog.CheckDetectorSize()}",
            $"Regions found: {found.Length}"
        };

        for (var index = 0; index < found.Length; index++)
        {
            var rect = found[index];
            var adjusted = new Rect
            {
                X = rect.X + (int)Math.Round(rect.Width * 0.1),
                Y = rect.Y + (int)Math.Round(rect.Height * 0.1),
                Width = (int)Math.Round(rect.Width * 0.8),
                Height = (int)Math.Round(rect.Height * 0.8)
            };

            annotated.Rectangle(adjusted.TopLeft, adjusted.BottomRight, Scalar.Red, 3);

            if (index < 10)
            {
                details.Add($"Detection {index + 1}: {adjusted.Width}x{adjusted.Height} at ({adjusted.X}, {adjusted.Y})");
            }
        }

        if (found.Length > 10)
        {
            details.Add($"... plus {found.Length - 10} more detection(s)");
        }

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the HOG sample.", details, annotated);
    }
}

internal sealed class MserSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.Mser;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var annotated = source.Clone();
        using var detector = MSER.Create();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        detector.DetectRegions(gray, out var contours, out _);

        for (var i = 0; i < contours.Length; i++)
        {
            var color = Scalar.RandomColor();
            foreach (var point in contours[i].Take(250))
            {
                annotated.Circle(point, 1, color, -1);
            }
        }

        var details = new List<string>
        {
            $"Regions: {contours.Length}"
        };

        details.AddRange(contours
            .OrderByDescending(region => region.Length)
            .Take(8)
            .Select((region, index) => $"Region {index + 1}: {region.Length} point(s)"));

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the MSER sample.", details, annotated);
    }
}

internal sealed class SimpleBlobDetectorSampleProcessor : IOpenCvSampleProcessor
{
    public OpenCvSampleMode Mode => OpenCvSampleMode.SimpleBlobDetector;

    public RecognitionResult Process(Mat source, string sourceName)
    {
        using var working = source.Clone();
        using var annotated = new Mat();
        Cv2.BitwiseNot(working, working);

        var circleParams = new SimpleBlobDetector.Params
        {
            MinThreshold = 10,
            MaxThreshold = 230,
            FilterByArea = true,
            MinArea = 500,
            MaxArea = 50000,
            FilterByCircularity = true,
            MinCircularity = 0.9f,
            FilterByConvexity = true,
            MinConvexity = 0.95f,
            FilterByInertia = true,
            MinInertiaRatio = 0.95f
        };

        var ovalParams = new SimpleBlobDetector.Params
        {
            MinThreshold = 10,
            MaxThreshold = 230,
            FilterByArea = true,
            MinArea = 500,
            MaxArea = 10000,
            FilterByCircularity = true,
            MinCircularity = 0.58f,
            FilterByConvexity = true,
            MinConvexity = 0.96f,
            FilterByInertia = true,
            MinInertiaRatio = 0.1f
        };

        using var circleDetector = SimpleBlobDetector.Create(circleParams);
        using var ovalDetector = SimpleBlobDetector.Create(ovalParams);

        var circles = circleDetector.Detect(working);
        var ovals = ovalDetector.Detect(working);

        Cv2.DrawKeypoints(working, circles, annotated, Scalar.HotPink, DrawMatchesFlags.DrawRichKeypoints);
        Cv2.DrawKeypoints(annotated, ovals, annotated, Scalar.LimeGreen, DrawMatchesFlags.DrawRichKeypoints);

        var details = new[]
        {
            $"Circle-like blobs: {circles.Length}",
            $"Oval-like blobs: {ovals.Length}",
            "Input inverted before detection"
        };

        return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with the simple blob detector sample.", details, annotated);
    }
}