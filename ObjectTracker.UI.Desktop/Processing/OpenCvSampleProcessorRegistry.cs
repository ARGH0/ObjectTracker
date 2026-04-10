namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Maps <see cref="OpenCvSampleMode"/> values to their processor instances.
/// </summary>
internal static class OpenCvSampleProcessorRegistry
{
    private static readonly IReadOnlyDictionary<OpenCvSampleMode, IOpenCvSampleProcessor> Processors =
        new Dictionary<OpenCvSampleMode, IOpenCvSampleProcessor>
        {
            [OpenCvSampleMode.BackgroundTracking] = new BackgroundTrackingSampleProcessor(),
            [OpenCvSampleMode.TrainCollisionRisk] = new TrainCollisionRiskSampleProcessor(),
            [OpenCvSampleMode.Blur] = new BlurSampleProcessor(),
            [OpenCvSampleMode.Clahe] = new ClaheSampleProcessor(),
            [OpenCvSampleMode.Contours] = new ContoursSampleProcessor(),
            [OpenCvSampleMode.Canny] = new CannySampleProcessor(),
            [OpenCvSampleMode.ConnectedComponents] = new ConnectedComponentsSampleProcessor(),
            [OpenCvSampleMode.FastCorners] = new FastCornersSampleProcessor(),
            [OpenCvSampleMode.Histogram] = new HistogramSampleProcessor(),
            [OpenCvSampleMode.HogPeople] = new HogPeopleSampleProcessor(),
            [OpenCvSampleMode.HoughLines] = new HoughLinesSampleProcessor(),
            [OpenCvSampleMode.Morphology] = new MorphologySampleProcessor(),
            [OpenCvSampleMode.Mser] = new MserSampleProcessor(),
            [OpenCvSampleMode.Yolo] = new YoloSampleProcessor(),
            [OpenCvSampleMode.SimpleBlobDetector] = new SimpleBlobDetectorSampleProcessor()
        };

    /// <summary>
    /// Gets the processor registered for the specified sample mode.
    /// </summary>
    /// <param name="mode">The sample mode to resolve.</param>
    /// <returns>The processor registered for the supplied mode.</returns>
    public static IOpenCvSampleProcessor GetProcessor(OpenCvSampleMode mode)
    {
        if (Processors.TryGetValue(mode, out var processor))
        {
            return processor;
        }

        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
    }

    /// <summary>
    /// Resets the processor state for the specified sample mode.
    /// </summary>
    /// <param name="mode">The sample mode whose processor state should be reset.</param>
    public static void ResetProcessor(OpenCvSampleMode mode)
    {
        // Most processors are stateless, but trackers and YOLO model state use this reset hook.
        GetProcessor(mode).Reset();
    }
}