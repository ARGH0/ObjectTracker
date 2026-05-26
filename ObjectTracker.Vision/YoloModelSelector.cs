using System;
using System.IO;
using System.Linq;

namespace ObjectTracker.Vision;

/// <summary>
/// YOLO model selection utility that prioritizes quantized INT8 models for performance.
/// INT8 models are typically 2-3x faster than FP32 while maintaining acceptable accuracy.
/// </summary>
public static class YoloModelSelector
{
    private static readonly string[] QuantizedSuffixes = { "int8", "quantized", "q" };
    private static readonly string[] ModelExtensions = { ".onnx", ".pt" };

    /// <summary>
    /// Finds the best available YOLO model, preferring INT8 quantized versions.
    /// </summary>
    /// <param name="modelPath">Primary model path to check</param>
    /// <returns>Path to best available model, or null if none found</returns>
    public static string? FindBestModel(string? modelPath)
    {
        // If explicit path provided and exists, check for INT8 alternative first
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            var int8Alternative = FindInt8Alternative(modelPath);
            if (int8Alternative != null)
            {
                System.Diagnostics.Debug.WriteLine($"YOLO: Using INT8 quantized model: {int8Alternative}");
                return int8Alternative;
            }

            System.Diagnostics.Debug.WriteLine($"YOLO: Using FP32 model: {modelPath}");
            return modelPath;
        }

        // Search for any YOLO model in model directory
        var modelDir = Path.Combine(AppContext.BaseDirectory, "yolomodels");
        if (!Directory.Exists(modelDir))
        {
            return null;
        }

        // Prefer INT8 models first
        var int8Models = Directory.GetFiles(modelDir, "*int8*.onnx")
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();

        if (int8Models != null)
        {
            System.Diagnostics.Debug.WriteLine($"YOLO: Found INT8 model: {int8Models}");
            return int8Models;
        }

        // Fall back to any YOLO model
        var anyModels = Directory.GetFiles(modelDir, "*.onnx")
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();

        if (anyModels != null)
        {
            System.Diagnostics.Debug.WriteLine($"YOLO: Found FP32 model: {anyModels}");
            return anyModels;
        }

        return null;
    }

    /// <summary>
    /// Checks if a model is INT8 quantized.
    /// </summary>
    public static bool IsQuantized(string modelPath)
    {
        return QuantizedSuffixes.Any(suffix => 
            modelPath.Contains(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Attempts to find INT8 alternative for a given model path.
    /// </summary>
    private static string? FindInt8Alternative(string modelPath)
    {
        if (IsQuantized(modelPath))
        {
            return modelPath; // Already quantized
        }

        var directory = Path.GetDirectoryName(modelPath) ?? ".";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(modelPath);
        var ext = Path.GetExtension(modelPath);

        // Try common INT8 naming patterns
        var candidates = new[]
        {
            Path.Combine(directory, $"{nameWithoutExt}-int8{ext}"),
            Path.Combine(directory, $"{nameWithoutExt}_int8{ext}"),
            Path.Combine(directory, $"{nameWithoutExt}-quantized{ext}"),
            Path.Combine(directory, $"{nameWithoutExt}_quantized{ext}"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Gets a performance estimate multiplier for the model type.
    /// INT8 is ~2.5x faster than FP32 on typical hardware.
    /// </summary>
    public static float GetPerformanceMultiplier(string modelPath)
    {
        return IsQuantized(modelPath) ? 2.5f : 1.0f;
    }
}
