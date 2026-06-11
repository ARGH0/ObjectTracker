using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal interface IVideoSource : IAsyncDisposable
{
    UsbFrameSnapshot? ReadLatestFrame();

    string SourceLabel { get; }
    int? Fps { get; }
}

internal sealed class VideoFileSource : IVideoSource
{
    private readonly VideoCapture _capture;
    private readonly string _label;
    private bool _disposed;
    private long _frameVersion;
    private double _fps;
    private DateTime _lastReadTime;

    public VideoFileSource(string path, string label)
    {
        _label = label;
        _capture = new VideoCapture(path);
        _fps = _capture.IsOpened() ? _capture.Fps : 0;
    }

    public UsbFrameSnapshot? ReadLatestFrame()
    {
        if (_disposed) return null;

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            _capture.Set(VideoCaptureProperties.PosFrames, 0);
            return null;
        }

        Cv2.ImEncode(".jpg", frame, out var jpeg);
        var snapshot = new UsbFrameSnapshot(
            _label,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            frame.Width,
            frame.Height,
            jpeg,
            Interlocked.Increment(ref _frameVersion));

        if (_fps > 0.1)
        {
            var targetInterval = TimeSpan.FromMilliseconds(1000.0 / _fps);
            var elapsed = DateTime.UtcNow - _lastReadTime;
            var remaining = targetInterval - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                Thread.Sleep(remaining);
            }
            _lastReadTime = DateTime.UtcNow;
        }

        return snapshot;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _capture.Dispose();
        return ValueTask.CompletedTask;
    }

    public string SourceLabel => _label;
    public int? Fps => _fps > 0 ? (int)_fps : null;
}

internal sealed class UsbVideoSource(UsbCameraOwnerManager manager, UsbCameraKey key) : IVideoSource
{
    private readonly UsbCameraOwnerManager _manager = manager;
    private readonly UsbCameraKey _key = key;
    private bool _disposed;

    public UsbFrameSnapshot? ReadLatestFrame()
    {
        if (_disposed) return null;
        return _manager.GetLatestFrame(_key);
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _ = _manager.ReleaseAsync(_key);
        }
        return ValueTask.CompletedTask;
    }

    public string SourceLabel => $"USB camera {key.CameraIndex}";
    public int? Fps => _manager.GetActualFps(_key) is { } fps ? (int)fps : null;
}
