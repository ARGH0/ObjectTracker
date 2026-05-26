using System;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ObjectTracker.Vision;

/// <summary>
/// Parallel processing helper for BackgroundEstimationEngine.
/// Enables motion and color detection to run concurrently for better performance.
/// </summary>
internal sealed class ParallelDetectionHelper : IDisposable
{
    private readonly object _motionLock = new();
    private Mat? _motionMask;
    private Mat? _motionCleanMask;

    private readonly object _colorLock = new();
    private Mat? _colorImage;

    /// <summary>
    /// Prepares motion detection results in parallel with color processing.
    /// </summary>
    public Task<(Mat mask, Mat cleanMask)> PrepareMotionDetectionAsync(
        Mat diff,
        int threshold,
        Mat morphKernel,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_motionLock)
            {
                _motionMask = new Mat();
                _motionCleanMask = new Mat();

                Cv2.Threshold(diff, _motionMask, threshold, 255, ThresholdTypes.Binary);
                Cv2.MorphologyEx(_motionMask, _motionCleanMask, MorphTypes.Open, morphKernel);
                Cv2.MorphologyEx(_motionCleanMask, _motionCleanMask, MorphTypes.Close, morphKernel);

                return (_motionMask.Clone(), _motionCleanMask.Clone());
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Prepares color image in parallel with motion detection.
    /// </summary>
    public Task<Mat> PrepareColorImageAsync(
        Mat source,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_colorLock)
            {
                _colorImage = new Mat();
                Cv2.CvtColor(source, _colorImage, ColorConversionCodes.BGR2HSV);
                return _colorImage.Clone();
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Runs motion and color preparation concurrently.
    /// </summary>
    public async Task<(Mat motionMask, Mat motionClean, Mat colorHsv)> PrepareDetectionsInParallelAsync(
        Mat diff,
        Mat colorSource,
        int threshold,
        Mat morphKernel,
        CancellationToken cancellationToken)
    {
        var motionTask = PrepareMotionDetectionAsync(diff, threshold, morphKernel, cancellationToken);
        var colorTask = PrepareColorImageAsync(colorSource, cancellationToken);

        await Task.WhenAll(motionTask, colorTask);

        var (motionMask, motionClean) = await motionTask;
        var colorHsv = await colorTask;

        return (motionMask, motionClean, colorHsv);
    }

    public void Dispose()
    {
        lock (_motionLock)
        {
            _motionMask?.Dispose();
            _motionCleanMask?.Dispose();
        }

        lock (_colorLock)
        {
            _colorImage?.Dispose();
        }
    }
}
