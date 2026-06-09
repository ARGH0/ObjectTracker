using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

/// <summary>
/// Tests for BackgroundEstimationEngine.TrainDetected event emission.
/// </summary>
public sealed class BackgroundEstimationEngineTrainDetectedEventTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ObjectTracker.Test." + Guid.NewGuid());

    [Fact]
    public void GetMovingObjectRectangles_DetectsObjects_InMotionMask()
    {
        var method = GetGetMovingObjectRectanglesMethod();

        using var mask = new Mat(48, 64, MatType.CV_8UC1, Scalar.All(0));
        using var roi = new Mat(mask, new Rect(10, 10, 24, 24));
        roi.SetTo(new Scalar(255));

        var rects = (IReadOnlyList<Rect>)method!.Invoke(null, new object?[] { mask, 50 })!;

        Assert.Single(rects);
        Assert.Equal(10, rects[0].X);
        Assert.Equal(10, rects[0].Y);
        Assert.Equal(24, rects[0].Width);
        Assert.Equal(24, rects[0].Height);
    }

    [Fact]
    public void GetMovingObjectRectangles_ReturnsEmpty_WhenMaskIsBlank()
    {
        var method = GetGetMovingObjectRectanglesMethod();

        using var mask = new Mat(48, 64, MatType.CV_8UC1, Scalar.All(0));

        var rects = (IReadOnlyList<Rect>)method!.Invoke(null, new object?[] { mask, 50 })!;

        Assert.Empty(rects);
    }

    [Fact]
    public void GetMovingObjectRectangles_ReturnsEmpty_WhenObjectsBelowMinArea()
    {
        var method = GetGetMovingObjectRectanglesMethod();

        using var mask = new Mat(48, 64, MatType.CV_8UC1, Scalar.All(0));
        using var roi = new Mat(mask, new Rect(10, 10, 3, 3));
        roi.SetTo(new Scalar(255));

        var rects = (IReadOnlyList<Rect>)method!.Invoke(null, new object?[] { mask, 50 })!;

        Assert.Empty(rects);
    }

    /// <summary>
    /// Integration test: subscribes to OnTrainDetected event and runs engine on USB source simulation.
    /// Skipped when run in parallel with other OpenCvSharp-heavy tests due to native library contention.
    /// Run in isolation to verify full pipeline event emission.
    /// </summary>
    [Fact(Skip = "Requires isolated execution - skip when running full test suite due to OpenCvSharp native library contention")]
    public async Task ProcessUsbCameraSourceAsync_TrainDetectedEventFires_WithCorrectPayload()
    {
        var key = new UsbCameraKey(0, "ANY");
        var backend = new CountingUsbCaptureBackend(frameDelay: TimeSpan.FromMilliseconds(5));
        await using var manager = new UsbCameraOwnerManager(backend);
        var engine = new BackgroundEstimationEngine();
        var detectedEvents = new List<BackgroundEstimationEngine.TrainDetected>();
        var processedFrames = 0;

        engine.OnTrainDetected += (detection) => detectedEvents.Add(detection);

        var result = await engine.ProcessUsbCameraSourceAsync(
            manager,
            key,
            UsbCaptureSettings.Default,
            sourceLabel: "zone-camera-1",
            sampleCount: 5,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onFrame: _ =>
            {
                Interlocked.Increment(ref processedFrames);
                return Task.CompletedTask;
            },
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldStopEarly: () => Volatile.Read(ref processedFrames) >= 3,
            CancellationToken.None);

        await Task.Delay(100);

        Assert.True(result.Success);
        Assert.True(Volatile.Read(ref processedFrames) >= 2);
        Assert.True(detectedEvents.Count > 0);

        var first = detectedEvents[0];
        Assert.Equal("zone-camera-1", first.CameraZoneId);
        Assert.NotEqual(0, first.LocalTrainId);
        Assert.True(first.PositionX >= 0);
        Assert.True(first.PositionY >= 0);
        Assert.True(first.BoundingBoxWidth > 0);
        Assert.True(first.BoundingBoxHeight > 0);
        Assert.True(first.Confidence >= 0);
        Assert.True(first.FrameNumber > 0);
    }

    [Fact]
    public async Task ProcessUsbCameraSourceAsync_TrainDetectedEventDoesNotFire_WhenNoTrainsDetected()
    {
        var key = new UsbCameraKey(1, "ANY");
        var backend = new UniformBackgroundCaptureBackend();
        await using var manager = new UsbCameraOwnerManager(backend);
        var engine = new BackgroundEstimationEngine();
        var detectedEvents = new List<BackgroundEstimationEngine.TrainDetected>();
        int processedFrames = 0;

        engine.OnTrainDetected += (detection) => detectedEvents.Add(detection);

        var result = await engine.ProcessUsbCameraSourceAsync(
            manager,
            key,
            UsbCaptureSettings.Default,
            sourceLabel: "Uniform Camera",
            sampleCount: 5,
            threshold: 25,
            BackgroundEstimationEngine.ProcessingOptions.Default,
            bakeImagePath: null,
            onFrame: _ =>
            {
                processedFrames++;
                return Task.CompletedTask;
            },
            onStatus: _ => Task.CompletedTask,
            getLiveTuning: null,
            shouldStopEarly: () => processedFrames >= 5,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(detectedEvents);
    }

    private static MethodInfo GetGetMovingObjectRectanglesMethod()
    {
        var assembly = Assembly.Load("ObjectTracker.UI.Desktop");
        var engineType = assembly.GetType("ObjectTracker.UI.Desktop.BackgroundEstimationEngine");
        Assert.NotNull(engineType);

        var method = engineType!.GetMethod(
            "GetMovingObjectRectangles",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return method!;
    }

    private sealed class CountingUsbCaptureBackend(TimeSpan? frameDelay = null) : IUsbCaptureBackend
    {
        private readonly Dictionary<UsbCameraKey, int> _openCounts = new();

        public int GetOpenCount(UsbCameraKey key) => _openCounts.TryGetValue(key, out var count) ? count : 0;

        public long LastReturnedFrameVersion { get; private set; }

        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            _openCounts[key] = GetOpenCount(key) + 1;
            return ValueTask.FromResult<IUsbCaptureSession>(new CountingUsbCaptureSession(key, frameDelay, version => LastReturnedFrameVersion = version));
        }
    }

    private sealed class CountingUsbCaptureSession(UsbCameraKey key, TimeSpan? frameDelay, Action<long> onFrameReturned) : IUsbCaptureSession
    {
        private long _frameVersion;

        public async ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (frameDelay is not null)
            {
                await Task.Delay(frameDelay.Value, cancellationToken);
            }

            _frameVersion++;
            onFrameReturned(_frameVersion);
            return new UsbCapturedFrame(
                UsbCameraSourceId.Format(key),
                _frameVersion * 100,
                Width: 64,
                Height: 48,
                EncodedJpeg: CreateFrameJpeg(_frameVersion));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static byte[] CreateFrameJpeg(long frameVersion)
        {
            using var frame = new Mat(48, 64, MatType.CV_8UC3, new Scalar(10, 10, 10));
            var rectX = (int)(frameVersion * 7) % 35;
            var rectY = (int)(frameVersion * 5) % 25;
            Cv2.Rectangle(frame, new Rect(rectX, rectY, 30, 30), new Scalar(250, 250, 250), -1);
            Cv2.ImEncode(".jpg", frame, out var jpeg);
            return jpeg;
        }
    }

    private sealed class UniformBackgroundCaptureBackend : IUsbCaptureBackend
    {
        public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IUsbCaptureSession>(new UniformBackgroundSession());
        }
    }

    private sealed class UniformBackgroundSession : IUsbCaptureSession
    {
        private long _frameVersion;
        private byte[]? _jpeg;

        public UniformBackgroundSession()
        {
            CreateUniformFrame();
        }

        private void CreateUniformFrame()
        {
            using var frame = new Mat(48, 64, MatType.CV_8UC3, new Scalar(128, 128, 128));
            Cv2.ImEncode(".jpg", frame, out _jpeg);
        }

        public ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            _frameVersion++;
            return new ValueTask<UsbCapturedFrame?>(new UsbCapturedFrame(
                "uniform:0",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                64,
                48,
                _jpeg!));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
