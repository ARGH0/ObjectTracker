using OpenCvSharp;
using System.Collections.Generic;

namespace ObjectTracker.Vision;

/// <summary>
/// Motion gate for region-of-interest filtering.
/// Extracts motion regions from background subtraction and provides ROI bounds for detection gating.
/// Reduces false positives by constraining detector to motion regions.
/// </summary>
public sealed class MotionGate
{
    public struct MotionRegion
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public bool Contains(float px, float py)
        {
            return px >= X && px < X + Width && py >= Y && py < Y + Height;
        }
    }

    private readonly int _minMotionArea;

    public MotionGate(int minMotionArea = 50)
    {
        _minMotionArea = minMotionArea;
    }

    /// <summary>
    /// Extract motion regions from a binary motion mask.
    /// </summary>
    public List<MotionRegion> ExtractMotionRegions(Mat motionMask)
    {
        var regions = new List<MotionRegion>();

        if (motionMask.Empty())
            return regions;

        Cv2.FindContours(motionMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < _minMotionArea)
                continue;

            var rect = Cv2.BoundingRect(contour);
            regions.Add(new MotionRegion
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            });
        }

        return regions;
    }

    /// <summary>
    /// Check if a detection center falls within any motion region.
    /// </summary>
    public bool IsInMotion(float detectionX, float detectionY, List<MotionRegion> regions)
    {
        if (regions.Count == 0)
            return true; // If no motion detected, allow all detections (fallback)

        foreach (var region in regions)
        {
            if (region.Contains(detectionX, detectionY))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Compute an expanded/padded region to provide a margin around motion blobs.
    /// </summary>
    public MotionRegion ExpandRegion(MotionRegion region, float padPercent = 0.1f)
    {
        var padX = region.Width * padPercent;
        var padY = region.Height * padPercent;

        return new MotionRegion
        {
            X = Math.Max(0, region.X - padX),
            Y = Math.Max(0, region.Y - padY),
            Width = region.Width + 2 * padX,
            Height = region.Height + 2 * padY
        };
    }
}
