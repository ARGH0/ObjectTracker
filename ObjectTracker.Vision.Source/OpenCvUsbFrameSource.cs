using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision.Source;

public sealed class OpenCvUsbFrameSource : IFrameSource
{
    private const VideoCaptureProperties OrientationMetaProperty = (VideoCaptureProperties)48;
    private const VideoCaptureProperties OrientationAutoProperty = (VideoCaptureProperties)49;
    private const VideoCaptureProperties BackendProperty = (VideoCaptureProperties)42;

    private readonly int _cameraIndex;
    private readonly VideoCaptureAPIs _api;
    private VideoCapture? _capture;
    private string? _pendingDiagnosticEvent;
    private double? _lastOrientationAuto;
    private double? _lastOrientationMeta;
    private int _frameCounter;

    public OpenCvUsbFrameSource(int cameraIndex)
        : this(cameraIndex, VideoCaptureAPIs.ANY)
    {
    }

    public OpenCvUsbFrameSource(int cameraIndex, VideoCaptureAPIs api)
    {
        _cameraIndex = cameraIndex;
        _api = api;
        Id = $"usb:{cameraIndex}:{api.ToString().ToLowerInvariant()}";
        DisplayName = $"USB camera {cameraIndex} ({api})";
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Diagnostics { get; private set; } = "capture not started";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _capture = new VideoCapture(_cameraIndex, _api);
        _capture.Set(VideoCaptureProperties.FrameWidth, 640);
        _capture.Set(VideoCaptureProperties.FrameHeight, 480);
        _capture.Set(VideoCaptureProperties.Fps, 20);
        _capture.Set(VideoCaptureProperties.BufferSize, 1);
        _capture.Set(OrientationAutoProperty, 0);

        if (!_capture.IsOpened())
        {
            throw new InvalidOperationException($"USB camera {_cameraIndex} kon niet worden geopend.");
        }

        var width = _capture.Get(VideoCaptureProperties.FrameWidth);
        var height = _capture.Get(VideoCaptureProperties.FrameHeight);
        var fps = _capture.Get(VideoCaptureProperties.Fps);
        var backend = _capture.Get(BackendProperty);
        _lastOrientationAuto = _capture.Get(OrientationAutoProperty);
        _lastOrientationMeta = _capture.Get(OrientationMetaProperty);
        _frameCounter = 0;

        Diagnostics =
            $"source={Id} | backend={backend:F0} ({_api}) | {width:F0}x{height:F0}@{fps:F1} | orientation_auto={_lastOrientationAuto:F0} | orientation_meta={_lastOrientationMeta:F0}";

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _capture?.Release();
        return Task.CompletedTask;
    }

    public Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (_capture is null || !_capture.IsOpened())
        {
            return Task.FromResult<FramePacket?>(null);
        }

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            return Task.FromResult<FramePacket?>(null);
        }

        _frameCounter++;
        if (_frameCounter % 60 == 0)
        {
            var orientationAuto = _capture.Get(OrientationAutoProperty);
            var orientationMeta = _capture.Get(OrientationMetaProperty);

            if (_lastOrientationAuto is not null && _lastOrientationMeta is not null)
            {
                if (!NearlyEqual(_lastOrientationAuto.Value, orientationAuto) || !NearlyEqual(_lastOrientationMeta.Value, orientationMeta))
                {
                    _pendingDiagnosticEvent =
                        $"Camera orientation property change gedetecteerd: auto {_lastOrientationAuto:F0}->{orientationAuto:F0}, meta {_lastOrientationMeta:F0}->{orientationMeta:F0}";
                }
            }

            _lastOrientationAuto = orientationAuto;
            _lastOrientationMeta = orientationMeta;
        }

        Cv2.ImEncode(".jpg", frame, out var bytes, [new ImageEncodingParam(ImwriteFlags.JpegQuality, 80)]);
        var packet = new FramePacket(
            Id,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            frame.Width,
            frame.Height,
            bytes);

        return Task.FromResult<FramePacket?>(packet);
    }

    public string? ConsumeDiagnosticEvent()
    {
        var message = _pendingDiagnosticEvent;
        _pendingDiagnosticEvent = null;
        return message;
    }

    public ValueTask DisposeAsync()
    {
        _capture?.Dispose();
        _capture = null;
        return ValueTask.CompletedTask;
    }

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.0001;
}