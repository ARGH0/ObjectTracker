namespace ObjectTracker.UI.Desktop;

internal static class OpenCvSampleProcessorRegistry
{
    private static readonly IReadOnlyDictionary<OpenCvSampleMode, IOpenCvSampleProcessor> Processors =
        new Dictionary<OpenCvSampleMode, IOpenCvSampleProcessor>
        {
            [OpenCvSampleMode.BackgroundTracking] = new BackgroundTrackingSampleProcessor(),
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
            [OpenCvSampleMode.SimpleBlobDetector] = new SimpleBlobDetectorSampleProcessor()
        };

    public static IOpenCvSampleProcessor GetProcessor(OpenCvSampleMode mode)
    {
        if (Processors.TryGetValue(mode, out var processor))
        {
            return processor;
        }

        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
    }

    public static void ResetProcessor(OpenCvSampleMode mode)
    {
        GetProcessor(mode).Reset();
    }
}