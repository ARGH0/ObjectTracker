using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using OpenCvSharp;
using ObjectTracker.Core.Domain;

namespace ObjectTracker.Vision;

/// <summary>
/// Thread-safe cache for preprocessed images (color space conversions, etc.).
/// Reduces redundant HSV conversions and other expensive operations across detectors.
/// </summary>
public sealed class ImagePreprocessingCache : IDisposable
{
    private sealed class PreprocessedEntry : IDisposable
    {
        public long FrameId { get; set; }
        public Mat? HsvImage { get; set; }
        public DateTime CreatedAt { get; set; }

        public void Dispose()
        {
            HsvImage?.Dispose();
        }
    }

    private readonly ConcurrentDictionary<string, PreprocessedEntry> _cache = new();
    private readonly object _cleanupLock = new();
    private long _frameCounter = 0;
    private const int MaxCacheSize = 3;
    private const int CacheTtlMs = 5000;

    public ImagePreprocessingCache() { }

    /// <summary>
    /// Gets or converts a BGR image to HSV. Thread-safe and reusable across detectors.
    /// </summary>
    public Mat GetOrConvertToHsv(FramePacket frame, Mat bgrImage)
    {
        var frameHash = ComputeFrameHash(frame.EncodedJpeg);
        
        if (_cache.TryGetValue(frameHash, out var entry) && entry.HsvImage != null)
        {
            return entry.HsvImage;
        }

        var hsvImage = new Mat();
        Cv2.CvtColor(bgrImage, hsvImage, ColorConversionCodes.BGR2HSV);

        if (_cache.TryGetValue(frameHash, out var existing))
        {
            existing.HsvImage = hsvImage;
        }
        else
        {
            var preprocessed = new PreprocessedEntry
            {
                FrameId = Interlocked.Increment(ref _frameCounter),
                HsvImage = hsvImage,
                CreatedAt = DateTime.UtcNow
            };
            _cache.TryAdd(frameHash, preprocessed);
        }

        CleanupIfNeeded();
        return hsvImage;
    }

    /// <summary>
    /// Marks a frame's preprocessed data for removal. Called after all detectors complete.
    /// </summary>
    public void MarkProcessed(FramePacket frame)
    {
        var frameHash = ComputeFrameHash(frame.EncodedJpeg);
        
        if (_cache.TryRemove(frameHash, out var entry))
        {
            entry.Dispose();
        }
    }

    private void CleanupIfNeeded()
    {
        lock (_cleanupLock)
        {
            if (_cache.Count <= MaxCacheSize)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var entriesToRemove = _cache
                .Where(kvp => (now - kvp.Value.CreatedAt).TotalMilliseconds > CacheTtlMs)
                .Take(_cache.Count - MaxCacheSize)
                .ToList();

            foreach (var (key, entry) in entriesToRemove)
            {
                if (_cache.TryRemove(key, out var removed))
                {
                    removed.Dispose();
                }
            }
        }
    }

    private static string ComputeFrameHash(byte[] jpegData)
    {
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(jpegData);
        return Convert.ToBase64String(hashBytes);
    }

    public void Dispose()
    {
        foreach (var entry in _cache.Values)
        {
            entry.Dispose();
        }
        _cache.Clear();
    }
}
