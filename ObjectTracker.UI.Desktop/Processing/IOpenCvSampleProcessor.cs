using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Defines the contract implemented by every OpenCV sample processor.
/// </summary>
internal interface IOpenCvSampleProcessor
{
    /// <summary>
    /// Gets the sample mode served by this processor.
    /// </summary>
    OpenCvSampleMode Mode { get; }

    /// <summary>
    /// Processes a single image or frame.
    /// </summary>
    /// <param name="source">The source image to analyze.</param>
    /// <param name="sourceName">The display name of the source being processed.</param>
    /// <returns>The annotated output and diagnostic details for the supplied image.</returns>
    RecognitionResult Process(Mat source, string sourceName);

    /// <summary>
    /// Resets any processor state when the active source restarts or the selected mode changes.
    /// </summary>
    void Reset()
    {
    }
}