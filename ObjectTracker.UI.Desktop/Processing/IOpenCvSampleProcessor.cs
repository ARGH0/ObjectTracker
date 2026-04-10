using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal interface IOpenCvSampleProcessor
{
    OpenCvSampleMode Mode { get; }

    RecognitionResult Process(Mat source, string sourceName);

    void Reset()
    {
    }
}