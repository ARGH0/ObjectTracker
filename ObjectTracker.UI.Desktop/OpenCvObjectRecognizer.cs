using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Provides a thin facade between media sources and the registered OpenCV sample processors.
/// </summary>
internal static class OpenCvObjectRecognizer
{
    /// <summary>
    /// Resets the processor associated with the specified sample mode.
    /// </summary>
    /// <param name="mode">The sample mode whose processor state should be cleared.</param>
    public static void Reset(OpenCvSampleMode mode)
    {
        OpenCvSampleProcessorRegistry.ResetProcessor(mode);
    }

    /// <summary>
    /// Loads and processes an image file with the selected sample processor.
    /// </summary>
    /// <param name="imagePath">The path of the image to process.</param>
    /// <param name="mode">The sample mode that determines which processor is used.</param>
    /// <returns>The annotated output and diagnostic details for the processed image.</returns>
    public static RecognitionResult RecognizeImage(string imagePath, OpenCvSampleMode mode)
    {
        // Load still images as color so all processors receive the same input format.
        using var source = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (source.Empty())
        {
            throw new InvalidOperationException("The selected image could not be loaded.");
        }

        return Process(source, Path.GetFileName(imagePath), mode);
    }

    /// <summary>
    /// Processes a captured frame with the selected sample processor.
    /// </summary>
    /// <param name="frame">The frame to analyze.</param>
    /// <param name="mode">The sample mode that determines which processor is used.</param>
    /// <param name="sourceName">The display name of the active source.</param>
    /// <returns>The annotated output and diagnostic details for the processed frame.</returns>
    public static RecognitionResult RecognizeFrame(Mat frame, OpenCvSampleMode mode, string sourceName = "camera")
    {
        if (frame.Empty())
        {
            throw new InvalidOperationException("The camera frame was empty.");
        }

        // Clone the frame because many processors mutate their input while producing overlays.
        using var source = frame.Clone();
        return Process(source, sourceName, mode);
    }

    /// <summary>
    /// Routes the supplied image data to the processor registered for the requested sample mode.
    /// </summary>
    /// <param name="source">The source image in OpenCV matrix form.</param>
    /// <param name="sourceName">The display name of the active source.</param>
    /// <param name="mode">The sample mode that determines which processor is used.</param>
    /// <returns>The processor output for the supplied source image.</returns>
    private static RecognitionResult Process(Mat source, string sourceName, OpenCvSampleMode mode)
    {
        return OpenCvSampleProcessorRegistry.GetProcessor(mode).Process(source, sourceName);
    }
}
