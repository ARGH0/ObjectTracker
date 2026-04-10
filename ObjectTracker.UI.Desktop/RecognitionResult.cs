namespace ObjectTracker.UI.Desktop;

internal sealed record RecognitionResult(string Status, IReadOnlyList<string> Details, byte[] AnnotatedImageBytes);