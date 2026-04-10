namespace ObjectTracker.UI.Desktop;

/// <summary>
/// Represents the status text, detail lines, and preview image returned by a sample processor.
/// </summary>
internal sealed record RecognitionResult(string Status, IReadOnlyList<string> Details, byte[] AnnotatedImageBytes);