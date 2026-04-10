using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal static class OpenCvObjectRecognizer
{
    public static void Reset(OpenCvSampleMode mode)
    {
        OpenCvSampleProcessorRegistry.ResetProcessor(mode);
    }

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
        return OpenCvSampleProcessorRegistry.GetProcessor(mode).Process(source, sourceName);
    }
}
