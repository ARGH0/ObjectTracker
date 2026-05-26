using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using OpenCvSharp;
using ObjectTracker.Core.Domain;

namespace ObjectTracker.Vision;

/// <summary>
/// Thread-safe cache for decoded frames. Reduces redundant JPEG decoding when multiple
/// detectors process the same frame. Uses frame content hash as key to detect frame changes.
/// </summary>
public sealed class FrameDecodeCache : IDisposable
{
    private sealed class CacheEntry : IDisposable
    {
        public Mat? ColorImage { get; set; }
        public Mat? GrayscaleImage { get; set; }
        public long FrameId { get; set; }
        public DateTime CreatedAt { get; set; }

        public void Dispose()
        {
            ColorImage?.Dispose();
            GrayscaleImage?.Dispose();
        }
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly object _cleanupLock = new();
    private long _frameCounter = 0;
    private const int MaxCacheSize = 3; // Keep last 3 frames
    private const int CacheTtlMs = 5000; // Expire entries after 5 seconds

    public FrameDecodeCache() { }

    /// <summary>
    /// Gets or decodes a frame as Color (BGR). Thread-safe and reusable across detectors.
    /// </summary>
    public Mat GetOrDecodeColor(FramePacket frame)
    {
        var frameHash = ComputeFrameHash(frame.EncodedJpeg);
        
        if (_cache.TryGetValue(frameHash, out var entry) && entry.ColorImage != null)
        {
            return entry.ColorImage;
        }

        var colorImage = Cv2.ImDecode(frame.EncodedJpeg, ImreadModes.Color);
        var cacheEntry = new CacheEntry
        {
            ColorImage = colorImage,
            FrameId = Interlocked.Increment(ref _frameCounter),
            CreatedAt = DateTime.UtcNow
        };

        _cache.TryAdd(frameHash, cacheEntry);
        CleanupIfNeeded();
        return colorImage;
    }

    /// <summary>
    /// Gets or decodes a frame as Grayscale. Thread-safe and reusable across detectors.
    /// </summary>
    public Mat GetOrDecodeGrayscale(FramePacket frame)
    {
        var frameHash = ComputeFrameHash(frame.EncodedJpeg);
        
        if (_cache.TryGetValue(frameHash, out var entry) && entry.GrayscaleImage != null)
        {
            return entry.GrayscaleImage;
        }

        var grayscaleImage = Cv2.ImDecode(frame.EncodedJpeg, ImreadModes.Grayscale);
        
        if (_cache.TryGetValue(frameHash, out var existing))
        {
            existing.GrayscaleImage = grayscaleImage;
        }
        else
        {
            var cacheEntry = new CacheEntry
            {
                GrayscaleImage = grayscaleImage,
                FrameId = Interlocked.Increment(ref _frameCounter),
                CreatedAt = DateTime.UtcNow
            };
            _cache.TryAdd(frameHash, cacheEntry);
        }

        CleanupIfNeeded();
        return grayscaleImage;
    }

    /// <summary>
    /// Marks a frame as processed so its entry can be reclaimed. Called after all detectors complete.
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
        // Use SHA256 for collision-resistant frame identity
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
