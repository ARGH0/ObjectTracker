using System;
using OpenCvSharp;

namespace ObjectTracker;

public sealed class MotionMaskRefiner
{
    public readonly record struct Options(int CloseKernelSize, int OpenKernelSize)
    {
    }

    public Mat Refine(Mat sourceMask, Options options)
    {
        if (sourceMask.Empty())
        {
            throw new ArgumentException("Source mask is empty.", nameof(sourceMask));
        }

        if (sourceMask.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("Source mask must be CV_8UC1.", nameof(sourceMask));
        }

        var closeSize = EnsureOddAtLeastOne(options.CloseKernelSize);
        var openSize = EnsureOddAtLeastOne(options.OpenKernelSize);

        var refined = new Mat();
        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(closeSize, closeSize));
        Cv2.MorphologyEx(sourceMask, refined, MorphTypes.Close, closeKernel);

        if (openSize > 1)
        {
            using var openKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(openSize, openSize));
            Cv2.MorphologyEx(refined, refined, MorphTypes.Open, openKernel);
        }

        return refined;
    }

    private static int EnsureOddAtLeastOne(int value)
    {
        var clamped = Math.Max(1, value);
        return clamped % 2 == 0 ? clamped + 1 : clamped;
    }
}
