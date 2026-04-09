using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal enum OpenCvSampleMode
{
    Blur,
    Clahe,
    Contours,
    Canny,
    ConnectedComponents,
    FastCorners,
    Histogram,
    HogPeople,
    HoughLines,
    Morphology,
    Mser,
    SimpleBlobDetector
}

internal static class OpenCvSampleModeExtensions
{
    public static string ToDisplayName(this OpenCvSampleMode mode)
    {
        return mode switch
        {
            OpenCvSampleMode.Blur => "Gaussian Blur",
            OpenCvSampleMode.Clahe => "CLAHE",
            OpenCvSampleMode.Contours => "Contours",
            OpenCvSampleMode.Canny => "Canny Edges",
            OpenCvSampleMode.ConnectedComponents => "Connected Components",
            OpenCvSampleMode.FastCorners => "FAST Corners",
            OpenCvSampleMode.Histogram => "Histogram",
            OpenCvSampleMode.HogPeople => "HOG People",
            OpenCvSampleMode.HoughLines => "Hough Lines",
            OpenCvSampleMode.Morphology => "Morphology",
            OpenCvSampleMode.Mser => "MSER",
            OpenCvSampleMode.SimpleBlobDetector => "Simple Blob Detector",
            _ => mode.ToString()
        };
    }

    public static string GetDescription(this OpenCvSampleMode mode)
    {
        return mode switch
        {
            OpenCvSampleMode.Blur => "Blur sample from the repo family: smooths the frame with a Gaussian kernel.",
            OpenCvSampleMode.Clahe => "Contrast Limited Adaptive Histogram Equalization on grayscale luminance.",
            OpenCvSampleMode.Contours => "Contour extraction with polygon approximation and shape-style labeling.",
            OpenCvSampleMode.Canny => "Edge map rendering using classic Canny thresholds.",
            OpenCvSampleMode.ConnectedComponents => "Threshold, label, and outline connected blobs with bounding boxes.",
            OpenCvSampleMode.FastCorners => "FAST keypoint detector drawn directly onto the frame.",
            OpenCvSampleMode.Histogram => "Intensity histogram visualization similar to the upstream histogram sample.",
            OpenCvSampleMode.HogPeople => "Default HOG person detector using OpenCV's built-in SVM weights.",
            OpenCvSampleMode.HoughLines => "Probabilistic Hough transform over Canny edges.",
            OpenCvSampleMode.Morphology => "Threshold and dilation preview using a cross-shaped structuring element.",
            OpenCvSampleMode.Mser => "MSER region extraction with per-region overlays.",
            OpenCvSampleMode.SimpleBlobDetector => "SimpleBlobDetector tuned to highlight circular and oval-like blobs.",
            _ => mode.ToString()
        };
    }
}

internal static class OpenCvObjectRecognizer
{
    public static RecognitionResult RecognizeImage(string imagePath, OpenCvSampleMode mode)
    {
        using var source = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (source.Empty())
        {
            throw new InvalidOperationException("The selected image could not be loaded.");
        }

        return Process(source, Path.GetFileName(imagePath), mode);
    }

    public static RecognitionResult RecognizeFrame(Mat frame, OpenCvSampleMode mode, string sourceName = "camera")
    {
        if (frame.Empty())
        {
            throw new InvalidOperationException("The camera frame was empty.");
        }

        using var source = frame.Clone();
        return Process(source, sourceName, mode);
    }

    private static RecognitionResult Process(Mat source, string sourceName, OpenCvSampleMode mode)
    {
        return mode switch
        {
            OpenCvSampleMode.Blur => RunBlur(source, sourceName),
            OpenCvSampleMode.Clahe => RunClahe(source, sourceName),
            OpenCvSampleMode.Contours => RunContours(source, sourceName),
            OpenCvSampleMode.Canny => RunCanny(source, sourceName),
            OpenCvSampleMode.ConnectedComponents => RunConnectedComponents(source, sourceName),
            OpenCvSampleMode.FastCorners => RunFastCorners(source, sourceName),
            OpenCvSampleMode.Histogram => RunHistogram(source, sourceName),
            OpenCvSampleMode.HogPeople => RunHogPeople(source, sourceName),
            OpenCvSampleMode.HoughLines => RunHoughLines(source, sourceName),
            OpenCvSampleMode.Morphology => RunMorphology(source, sourceName),
            OpenCvSampleMode.Mser => RunMser(source, sourceName),
            OpenCvSampleMode.SimpleBlobDetector => RunSimpleBlobDetector(source, sourceName),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static RecognitionResult RunBlur(Mat source, string sourceName)
    {
        using var annotated = new Mat();
        Cv2.GaussianBlur(source, annotated, new Size(9, 9), 1.8);

        var details = new[]
        {
            "Kernel: 9x9",
            "Sigma: 1.8",
            $"Frame size: {source.Width}x{source.Height}"
        };

        return CreateResult($"Processed {sourceName} with the blur sample.", details, annotated);
    }

    private static RecognitionResult RunClahe(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var enhanced = new Mat();
        using var annotated = new Mat();
        using var clahe = Cv2.CreateCLAHE();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        clahe.ClipLimit = 20;
        clahe.TilesGridSize = new Size(8, 8);
        clahe.Apply(gray, enhanced);
        Cv2.CvtColor(enhanced, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new[]
        {
            "Clip limit: 20",
            "Tile grid: 8x8",
            $"Mean intensity: {Cv2.Mean(enhanced).Val0:F1}"
        };

        return CreateResult($"Processed {sourceName} with the CLAHE sample.", details, annotated);
    }

    private static RecognitionResult RunContours(Mat source, string sourceName)
    {
        using var annotated = source.Clone();
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        Cv2.Canny(blurred, edges, 60, 180);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var details = new List<string>();
        var index = 1;

        foreach (var contour in contours.OrderByDescending(candidate => Cv2.ContourArea(candidate)))
        {
            var area = Cv2.ContourArea(contour);
            if (area < 1500)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, true);
            if (perimeter <= 0)
            {
                continue;
            }

            var polygon = Cv2.ApproxPolyDP(contour, 0.03 * perimeter, true);
            var bounds = Cv2.BoundingRect(polygon);
            if (bounds.Width < 30 || bounds.Height < 30)
            {
                continue;
            }

            var label = ClassifyShape(polygon, bounds);
            details.Add($"{label} {index}: {bounds.Width}x{bounds.Height} at ({bounds.X}, {bounds.Y})");

            Cv2.Rectangle(annotated, bounds, new Scalar(32, 124, 229), 3);
            Cv2.PutText(
                annotated,
                label,
                new Point(bounds.X, Math.Max(24, bounds.Y - 8)),
                HersheyFonts.HersheySimplex,
                0.8,
                new Scalar(32, 124, 229),
                2,
                LineTypes.AntiAlias);

            index++;
        }

        var status = details.Count == 0
            ? $"Processed {sourceName} with the contour sample. No large contour-based objects were detected."
            : $"Processed {sourceName} with the contour sample. Detected {details.Count} object(s).";

        return CreateResult(status, details, annotated);
    }

    private static RecognitionResult RunCanny(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();
        using var annotated = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.2);
        Cv2.Canny(blurred, edges, 60, 180);
        Cv2.CvtColor(edges, annotated, ColorConversionCodes.GRAY2BGR);

        var edgePixels = Cv2.CountNonZero(edges);
        var details = new List<string>
        {
            $"Edge pixels: {edgePixels:N0}",
            "Thresholds: 60 / 180",
            $"Frame size: {source.Width}x{source.Height}"
        };

        var status = $"Processed {sourceName} with the Canny sample.";
        return CreateResult(status, details, annotated);
    }

    private static RecognitionResult RunConnectedComponents(Mat source, string sourceName)
    {
        using var gray = source.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var binary = gray.Threshold(0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
        using var annotated = binary.CvtColor(ColorConversionCodes.GRAY2BGR);

        var cc = Cv2.ConnectedComponentsEx(binary);
        if (cc.LabelCount <= 1)
        {
            return CreateResult(
                $"Processed {sourceName} with the connected components sample. No foreground blobs survived thresholding.",
                new[] { "Label count: 0" },
                annotated);
        }

        var blobs = cc.Blobs.Skip(1).ToArray();
        foreach (var blob in blobs)
        {
            annotated.Rectangle(blob.Rect, Scalar.Red, 2);
        }

        var largestBlob = blobs.OrderByDescending(blob => blob.Area).First();
        var details = blobs
            .OrderByDescending(blob => blob.Area)
            .Take(10)
            .Select((blob, index) => $"Blob {index + 1}: area {blob.Area}, rect {blob.Rect.Width}x{blob.Rect.Height} at ({blob.Rect.X}, {blob.Rect.Y})")
            .ToList();

        if (blobs.Length > 10)
        {
            details.Add($"... plus {blobs.Length - 10} more blob(s)");
        }

        details.Insert(0, $"Largest area: {largestBlob.Area}");
        details.Insert(1, $"Label count: {blobs.Length}");

        return CreateResult($"Processed {sourceName} with the connected components sample.", details, annotated);
    }

    private static RecognitionResult RunFastCorners(Mat source, string sourceName)
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

        return CreateResult($"Processed {sourceName} with the FAST sample.", details, annotated);
    }

    private static RecognitionResult RunHistogram(Mat source, string sourceName)
    {
        using var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

        const int width = 260;
        const int height = 200;
        using var render = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.All(255));
        using var hist = new Mat();

        var dims = new[] { 256 };
        var ranges = new[] { new Rangef(0, 256) };
        Cv2.CalcHist([gray], [0], null, hist, 1, dims, ranges);
        Cv2.MinMaxLoc(hist, out double _, out double maxValue);

        for (var i = 1; i < 256; i++)
        {
            var previous = hist.At<float>(i - 1);
            var current = hist.At<float>(i);
            var p1 = new Point(i - 1, height - Math.Round(previous / maxValue * height));
            var p2 = new Point(i, height - Math.Round(current / maxValue * height));
            Cv2.Line(render, p1, p2, new Scalar(32, 124, 229), 1, LineTypes.AntiAlias);
        }

        var details = new[]
        {
            $"Mean intensity: {Cv2.Mean(gray).Val0:F1}",
            $"StdDev: {ComputeStdDev(gray):F1}",
            "Bins: 256"
        };

        return CreateResult($"Processed {sourceName} with the histogram sample.", details, render);
    }

    private static RecognitionResult RunHogPeople(Mat source, string sourceName)
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

        return CreateResult($"Processed {sourceName} with the HOG sample.", details, annotated);
    }

    private static RecognitionResult RunHoughLines(Mat source, string sourceName)
    {
        using var annotated = source.Clone();
        using var gray = new Mat();
        using var edges = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Canny(gray, edges, 50, 200, 3, false);

        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, 60, 40, 12);
        var details = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var start = new Point(line.P1.X, line.P1.Y);
            var end = new Point(line.P2.X, line.P2.Y);
            var length = Math.Sqrt(Math.Pow(line.P1.X - line.P2.X, 2) + Math.Pow(line.P1.Y - line.P2.Y, 2));

            Cv2.Line(annotated, start, end, new Scalar(216, 78, 61), 3, LineTypes.AntiAlias);

            if (index < 12)
            {
                details.Add($"Line {index + 1}: ({line.P1.X}, {line.P1.Y}) -> ({line.P2.X}, {line.P2.Y}), len {length:F1}");
            }
        }

        if (lines.Length > 12)
        {
            details.Add($"... plus {lines.Length - 12} more line segment(s)");
        }

        var status = lines.Length == 0
            ? $"Processed {sourceName} with the Hough line sample. No strong line segments were found."
            : $"Processed {sourceName} with the Hough line sample. Found {lines.Length} line segment(s).";

        return CreateResult(status, details, annotated);
    }

    private static RecognitionResult RunMorphology(Mat source, string sourceName)
    {
        using var gray = new Mat();
        using var binary = new Mat();
        using var annotated = new Mat();

        byte[] kernelValues = [0, 1, 0, 1, 1, 1, 0, 1, 0];
        using var kernel = Mat.FromPixelData(3, 3, MatType.CV_8UC1, kernelValues);

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Otsu);
        Cv2.Dilate(binary, annotated, kernel);
        Cv2.CvtColor(annotated, annotated, ColorConversionCodes.GRAY2BGR);

        var details = new[]
        {
            "Operation: dilate",
            "Kernel: 3x3 cross",
            $"Foreground pixels: {Cv2.CountNonZero(binary):N0}"
        };

        return CreateResult($"Processed {sourceName} with the morphology sample.", details, annotated);
    }

    private static RecognitionResult RunMser(Mat source, string sourceName)
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

        return CreateResult($"Processed {sourceName} with the MSER sample.", details, annotated);
    }

    private static RecognitionResult RunSimpleBlobDetector(Mat source, string sourceName)
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

        return CreateResult($"Processed {sourceName} with the simple blob detector sample.", details, annotated);
    }

    private static RecognitionResult CreateResult(string status, IReadOnlyList<string> details, Mat annotated)
    {
        Cv2.ImEncode(".png", annotated, out var encoded);
        return new RecognitionResult(status, details, encoded);
    }

    private static double ComputeStdDev(Mat gray)
    {
        Cv2.MeanStdDev(gray, out _, out var stddev);
        return stddev.Val0;
    }

    private static string ClassifyShape(Point[] polygon, Rect bounds)
    {
        return polygon.Length switch
        {
            3 => "Triangle",
            4 => IsSquare(bounds) ? "Square" : "Rectangle",
            >= 8 => "Circle",
            _ => "Object"
        };
    }

    private static bool IsSquare(Rect bounds)
    {
        var ratio = bounds.Width / (double)Math.Max(1, bounds.Height);
        return ratio is > 0.85 and < 1.15;
    }
}

internal sealed record RecognitionResult(string Status, IReadOnlyList<string> Details, byte[] AnnotatedImageBytes);