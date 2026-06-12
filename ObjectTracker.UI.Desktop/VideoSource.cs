using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

internal interface IVideoSource : IAsyncDisposable
{
    VideoFrameSnapshot? ReadLatestFrame();

    ValueTask<VideoFrameSnapshot?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ReadLatestFrame());
    }

    string SourceLabel { get; }
    int? Fps { get; }
}

internal readonly record struct VideoFrameSnapshot(
    string SourceId,
    long TimestampUtcMs,
    int Width,
    int Height,
    byte[] EncodedJpeg,
    long FrameVersion);

internal sealed class VideoFileSource : IVideoSource
{
    private readonly VideoCapture _capture;
    private readonly string _label;
    private bool _disposed;
    private long _frameVersion;
    private readonly double _fps;
    private DateTime _lastReadTime;

    public VideoFileSource(string path, string label)
    {
        _label = label;
        _capture = new VideoCapture(path);
        _fps = _capture.IsOpened() ? _capture.Fps : 0;
    }

    public VideoFrameSnapshot? ReadLatestFrame()
    {
        if (_disposed)
            return null;

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            _capture.Set(VideoCaptureProperties.PosFrames, 0);
            return null;
        }

        Cv2.ImEncode(".jpg", frame, out var jpeg);
        var snapshot = new VideoFrameSnapshot(
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

internal sealed class UsbCameraSource : IVideoSource
{
    private readonly int _deviceIndex;
    private readonly string _label;
    private VideoCapture? _capture;
    private double _fps;
    private bool _disposed;
    private bool _triedToOpen;
    private long _frameVersion;

    public UsbCameraSource(int deviceIndex, string label)
    {
        _deviceIndex = deviceIndex;
        _label = label;
    }

    private VideoCapture? Capture
    {
        get
        {
            if (_disposed)
            {
                return null;
            }

            if (_capture is not null && _capture.IsOpened())
            {
                return _capture;
            }

            if (!_triedToOpen)
            {
                _triedToOpen = true;
                _capture = new VideoCapture(_deviceIndex);
                if (_capture.IsOpened())
                {
                    _capture.Set(VideoCaptureProperties.FrameWidth, 1280);
                    _capture.Set(VideoCaptureProperties.FrameHeight, 720);
                    _fps = _capture.Fps > 0 ? _capture.Fps : 30;
                }
            }

            return _capture;
        }
    }

    public VideoFrameSnapshot? ReadLatestFrame()
    {
        if (_disposed)
        {
            return null;
        }

        var capture = Capture;
        if (capture is null || !capture.IsOpened())
        {
            return null;
        }

        using var frame = new Mat();
        if (!capture.Read(frame) || frame.Empty())
        {
            return null;
        }

        Cv2.ImEncode(".jpg", frame, out var jpeg);
        return new VideoFrameSnapshot(
            _label,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            frame.Width,
            frame.Height,
            jpeg,
            Interlocked.Increment(ref _frameVersion));
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _capture?.Dispose();
        return ValueTask.CompletedTask;
    }

    public string SourceLabel => _label;
    public int? Fps => _fps > 0 ? (int)_fps : null;
}
