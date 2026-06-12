using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.Core.Domain;
using ObjectTracker.UI.Desktop.Region.Model;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

public static class TileOverlayRenderer
{
    private const int ProcessMaxWidth = 640;

    public static byte[]? RenderAnnotations(
        byte[] rawJpeg,
        IReadOnlyList<ColorCalibrationProfile> colorCalibrations,
        int minMotionArea = 220,
        int minColorPixels = 40,
        int morphKernelSize = 3)
    {
        using var source = Cv2.ImDecode(rawJpeg, ImreadModes.Color);
        if (source.Empty())
            return null;

        var scaleRatio = CalculateScaleRatio(source.Width, source.Height, ProcessMaxWidth);
        var processSize = new OpenCvSharp.Size(
            (int)(source.Width * scaleRatio),
            (int)(source.Height * scaleRatio));

        using var gray = new Mat();
        using var colorResized = new Mat();
        using var resized = new Mat();
        using var diff = new Mat();
        using var mask = new Mat();
        using var refinedMask = new Mat();
        using var hsv = new Mat();

        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
        Cv2.Resize(source, colorResized, processSize, interpolation: InterpolationFlags.Area);

        using var tempMedian = new Mat(processSize, MatType.CV_8UC1);
        tempMedian.SetTo(Scalar.Black);
        Cv2.Absdiff(tempMedian, resized, diff);
        Cv2.Threshold(diff, mask, 100, 255, ThresholdTypes.Binary);

        using var refined = RefineMotionMask(mask, morphKernelSize);
        refined.CopyTo(refinedMask);

        var movingRects = GetMovingObjectRectangles(refinedMask, minMotionArea);
        if (movingRects.Count == 0)
            return EncodeFrame(source);

        var frameColors = ClassifyColorsPerRect(colorResized, refinedMask, hsv, movingRects, colorCalibrations, minColorPixels);

        using var annotated = new Mat();
        colorResized.CopyTo(annotated, refinedMask);
        DrawMovingObjectBoxes(annotated, movingRects);
        RenderColorDetections(annotated, colorResized, refinedMask, hsv, movingRects, colorCalibrations, minColorPixels);

        var scaledAnnotated = new Mat();
        Cv2.Resize(annotated, scaledAnnotated, new OpenCvSharp.Size(source.Width, source.Height), interpolation: InterpolationFlags.Linear);

        return EncodeFrame(scaledAnnotated);
    }

    public static byte[] DrawRegions(
        byte[] rawJpeg,
        int gridColumns,
        int gridRows,
        IReadOnlyList<RegionOverlayInfo> regions)
    {
        using var frame = Cv2.ImDecode(rawJpeg, ImreadModes.Color);
        if (frame.Empty() || regions.Count == 0)
            return rawJpeg;

        var result = new Mat();
        frame.CopyTo(result);

        var scaleW = (double)frame.Width / gridColumns;
        var scaleH = (double)frame.Height / gridRows;
        var cellSize = Math.Min(scaleW, scaleH);
        var offsetX = (frame.Width - cellSize * gridColumns) / 2;
        var offsetY = (frame.Height - cellSize * gridRows) / 2;

        foreach (var region in regions)
        {
            var color = region.Type switch
            {
                20 => new Scalar(0, 255, 0),
                30 => new Scalar(0, 0, 255),
                _ => new Scalar(200, 200, 200)
            };

            foreach (var cell in region.Cells)
            {
                var x = offsetX + cell.Column * cellSize;
                var y = offsetY + cell.Row * cellSize;
                Cv2.Rectangle(result, new OpenCvSharp.Point((int)x, (int)y),
                    new OpenCvSharp.Point((int)(x + cellSize), (int)(y + cellSize)),
                    color, 1);
            }
        }

        return EncodeFrame(result);
    }

    public static byte[] CompositeWithRegions(
        byte[] annotatedJpeg,
        int gridColumns,
        int gridRows,
        IReadOnlyList<RegionOverlayInfo> regions)
    {
        using var annotated = Cv2.ImDecode(annotatedJpeg, ImreadModes.Color);
        if (annotated.Empty() || regions.Count == 0)
            return annotatedJpeg;

        var result = new Mat();
        annotated.CopyTo(result);

        var scaleW = (double)annotated.Width / gridColumns;
        var scaleH = (double)annotated.Height / gridRows;
        var cellSize = Math.Min(scaleW, scaleH);
        var offsetX = (annotated.Width - cellSize * gridColumns) / 2;
        var offsetY = (annotated.Height - cellSize * gridRows) / 2;

        foreach (var region in regions)
        {
            var color = region.Type switch
            {
                20 => new Scalar(0, 255, 0),
                30 => new Scalar(0, 0, 255),
                _ => new Scalar(200, 200, 200)
            };

            foreach (var cell in region.Cells)
            {
                var x = offsetX + cell.Column * cellSize;
                var y = offsetY + cell.Row * cellSize;
                Cv2.Rectangle(result, new OpenCvSharp.Point((int)x, (int)y),
                    new OpenCvSharp.Point((int)(x + cellSize), (int)(y + cellSize)),
                    color, 1);
            }
        }

        return EncodeFrame(result);
    }

    private static double CalculateScaleRatio(int width, int height, int maxWidth)
    {
        if (width <= maxWidth)
            return 1.0;
        return (double)maxWidth / width;
    }

    private static Mat RefineMotionMask(Mat mask, int morphKernelSize)
    {
        using var eroded = new Mat();
        var dilated = new Mat();
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(morphKernelSize, morphKernelSize));
        Cv2.MorphologyEx(mask, eroded, MorphTypes.Erode, kernel);
        Cv2.MorphologyEx(eroded, dilated, MorphTypes.Dilate, kernel);
        return dilated;
    }

    private static List<OpenCvSharp.Rect> GetMovingObjectRectangles(Mat motionMask, int minMotionArea)
    {
        Cv2.FindContours(motionMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var rects = new List<OpenCvSharp.Rect>();
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minMotionArea)
                continue;
            rects.Add(Cv2.BoundingRect(contour));
        }
        return rects;
    }

    private static void DrawMovingObjectBoxes(Mat destination, IReadOnlyList<OpenCvSharp.Rect> movingRects)
    {
        foreach (var rect in movingRects)
        {
            Cv2.Rectangle(destination, rect, new Scalar(0, 240, 255), 2);
            Cv2.PutText(destination, "Moving",
                new OpenCvSharp.Point(rect.X, Math.Max(16, rect.Y - 4)),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 240, 255), 1);
        }
    }

    private static IReadOnlyList<(OpenCvSharp.Rect Rect, string Color)> ClassifyColorsPerRect(
        Mat colorResized,
        Mat motionMask,
        Mat hsv,
        IReadOnlyList<OpenCvSharp.Rect> movingRects,
        IReadOnlyList<ColorCalibrationProfile> colorCalibrations,
        int minColorPixels)
    {
        var results = new List<(OpenCvSharp.Rect Rect, string Color)>();
        foreach (var rect in movingRects)
        {
            using var colorRoi = new Mat(colorResized, rect);
            using var motionRoi = new Mat(motionMask, rect);
            Cv2.CvtColor(colorRoi, hsv, ColorConversionCodes.BGR2HSV);
            var (label, _) = ClassifyDominantColor(hsv, motionRoi, colorCalibrations, minColorPixels);
            results.Add((rect, label));
        }
        return results;
    }

    private static void RenderColorDetections(
        Mat destination,
        Mat sourceColor,
        Mat motionMask,
        Mat hsv,
        IReadOnlyList<OpenCvSharp.Rect> movingRects,
        IReadOnlyList<ColorCalibrationProfile> colorCalibrations,
        int minColorPixels)
    {
        var colors = ClassifyColorsPerRect(sourceColor, motionMask, hsv, movingRects, colorCalibrations, minColorPixels);
        foreach (var (rect, label) in colors)
        {
            var color = GetOverlayColor(label);
            Cv2.Rectangle(destination, rect, color, 2);
            Cv2.PutText(destination, label,
                new OpenCvSharp.Point(rect.X, Math.Max(16, rect.Y - 4)),
                HersheyFonts.HersheySimplex, 0.55, color, 2);
        }
    }

    private static (string Label, Scalar Color) ClassifyDominantColor(
        Mat hsvRoi,
        Mat motionRoiMask,
        IReadOnlyList<ColorCalibrationProfile> colorCalibrations,
        int minColorPixels)
    {
        if (colorCalibrations.Count == 0)
            return ("Unknown", new Scalar(180, 180, 180));

        var bestCount = 0;
        ColorCalibrationProfile? bestProfile = null;

        foreach (var profile in colorCalibrations)
        {
            using var mask = BuildMask(hsvRoi, profile);
            Cv2.BitwiseAnd(mask, motionRoiMask, mask);
            var count = Cv2.CountNonZero(mask);
            if (count > bestCount)
            {
                bestCount = count;
                bestProfile = profile;
            }
        }

        if (bestProfile is null || bestCount < minColorPixels)
            return ("Unknown", new Scalar(180, 180, 180));

        return (bestProfile.Value.Name, GetOverlayColor(bestProfile.Value.Name));
    }

    private static Mat BuildMask(Mat hsv, ColorCalibrationProfile profile)
    {
        if (profile.HueLower <= profile.HueUpper)
        {
            var mask = new Mat();
            Cv2.InRange(hsv,
                new Scalar(profile.HueLower, profile.SaturationLower, profile.ValueLower),
                new Scalar(profile.HueUpper, profile.SaturationUpper, profile.ValueUpper), mask);
            return mask;
        }

        var primary = new Mat();
        var secondary = new Mat();
        var combined = new Mat();

        Cv2.InRange(hsv,
            new Scalar(profile.HueLower, profile.SaturationLower, profile.ValueLower),
            new Scalar(180, profile.SaturationUpper, profile.ValueUpper), primary);
        Cv2.InRange(hsv,
            new Scalar(0, profile.SaturationLower, profile.ValueLower),
            new Scalar(profile.HueUpper, profile.SaturationUpper, profile.ValueUpper), secondary);
        Cv2.BitwiseOr(primary, secondary, combined);

        primary.Dispose();
        secondary.Dispose();
        return combined;
    }

    private static Scalar GetOverlayColor(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "RED" => new Scalar(60, 60, 255),
            "GREEN" => new Scalar(60, 220, 60),
            "BLUE" => new Scalar(255, 120, 50),
            "YELLOW" => new Scalar(40, 220, 240),
            "WHITE" => new Scalar(255, 255, 255),
            _ => new Scalar(180, 180, 180)
        };
    }

    private static byte[] EncodeFrame(Mat frame)
    {
        Cv2.ImEncode(".jpg", frame, out var jpegBytes, new[] { (int)ImwriteFlags.JpegQuality, 85 });
        return jpegBytes;
    }
}

public readonly record struct RegionOverlayInfo(
    string Name,
    int Type,
    IReadOnlyList<GridCell> Cells);
