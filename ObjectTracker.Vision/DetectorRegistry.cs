using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision;

/// <summary>
/// Registry for creating and managing detection and tracking components.
/// This enables dynamic instantiation of detectors and trackers for pipeline use.
/// </summary>
public static class DetectorRegistry
{
    /// <summary>
    /// Creates a DetectorManager with all available detection algorithms.
    /// </summary>
    /// <param name="yoloModelPath">Optional path to YOLO ONNX model. If null or file doesn't exist, YOLO detector is skipped.</param>
    /// <param name="defaultMode">Default detection mode. Must be available in the algorithms list.</param>
    /// <returns>Configured DetectorManager instance</returns>
    public static IDetectorManager CreateDetectorManager(string? yoloModelPath = null, DetectorMode defaultMode = DetectorMode.Hybrid)
    {
        var algorithms = new List<IDetectionAlgorithm>
        {
            new OpenCvArucoDetector(),
            new OpenCvColorDetector(),
            new AlternativeNoOpDetector(),
        };

        // Optionally add YOLO detector if model is available
        if (!string.IsNullOrEmpty(yoloModelPath) && File.Exists(yoloModelPath))
        {
            try
            {
                algorithms.Add(new YoloDetector(yoloModelPath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load YOLO detector: {ex.Message}");
                // Continue without YOLO detector if loading fails
            }
        }

        return new DetectorManager(algorithms, defaultMode);
    }

    /// <summary>
    /// Creates an IouTracker with default configuration.
    /// </summary>
    /// <returns>Configured IouTracker instance</returns>
    public static ITracker CreateDefaultTracker()
    {
        return new IouTracker();
    }

    /// <summary>
    /// Attempts to locate the best YOLO model file, preferring INT8 quantized versions.
    /// INT8 models are ~2.5x faster than FP32 with comparable accuracy.
    /// </summary>
    /// <returns>Path to best available model if found; null otherwise</returns>
    public static string? FindYoloModel()
    {
        var candidates = FindYoloModelCandidates();
        
        // Prefer INT8 quantized models for better performance
        var quantized = candidates.FirstOrDefault(YoloModelSelector.IsQuantized);
        if (quantized != null)
        {
            System.Diagnostics.Debug.WriteLine($"YOLO: Using INT8 quantized model: {quantized}");
            return quantized;
        }

        var best = candidates.FirstOrDefault();
        if (best != null)
        {
            System.Diagnostics.Debug.WriteLine($"YOLO: Using FP32 model: {best}");
        }

        return best;
    }

    /// <summary>
    /// Finds all available YOLO model candidates in priority order.
    /// </summary>
    /// <returns>Ordered list of discovered model paths.</returns>
    public static IReadOnlyList<string> FindYoloModelCandidates()
    {
        var candidateFileNames = new[]
        {
            "yolo11n.onnx",
            "yolo11s.onnx",
            "yolo26n.onnx",
            "yolov8n.onnx",
        };

        var candidateDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void AddAncestorDirectories(HashSet<string> dirs, string? startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return;
            }

            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                dirs.Add(current.FullName);
                dirs.Add(Path.Combine(current.FullName, "yolomodels"));
                dirs.Add(Path.Combine(current.FullName, "yolomodels", "onnx"));
                dirs.Add(Path.Combine(current.FullName, "models"));
                current = current.Parent;
            }
        }

        AddAncestorDirectories(candidateDirectories, AppContext.BaseDirectory);
        AddAncestorDirectories(candidateDirectories, Environment.CurrentDirectory);

        // User AppData
        candidateDirectories.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ObjectTracker",
            "models"));

        var matches = new List<string>();
        foreach (var directory in candidateDirectories)
        {
            foreach (var fileName in candidateFileNames)
            {
                var path = Path.Combine(directory, fileName);
                if (File.Exists(path))
                {
                    matches.Add(Path.GetFullPath(path));
                }
            }
        }

        return matches;
    }
}
