using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision.Source;

public sealed class OpenCvUsbFrameSource : IFrameSource
{
    private const VideoCaptureProperties OrientationMetaProperty = VideoCaptureProperties.OrientationMeta;
    private const VideoCaptureProperties OrientationAutoProperty = VideoCaptureProperties.OrientationAuto;
    private const VideoCaptureProperties BackendProperty = VideoCaptureProperties.Backend;

    private readonly int cameraIndex;
    private readonly VideoCaptureAPIs api;
    private VideoCapture? capture;
    private string? pendingDiagnosticEvent;
    private double? lastOrientationAuto;
    private double? lastOrientationMeta;
    private int frameCounter;

    public OpenCvUsbFrameSource(int cameraIndex)
        : this(cameraIndex, VideoCaptureAPIs.ANY)
    {
    }

    public OpenCvUsbFrameSource(int cameraIndex, VideoCaptureAPIs api)
    {
        this.cameraIndex = cameraIndex;
        this.api = api;
        Id = $"usb:{cameraIndex}:{api.ToString().ToUpperInvariant()}";
        DisplayName = $"USB camera {cameraIndex} ({api})";
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Diagnostics { get; private set; } = "capture not started";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        capture = new VideoCapture(cameraIndex, api);
        capture.Set(VideoCaptureProperties.FrameWidth, 640);
        capture.Set(VideoCaptureProperties.FrameHeight, 480);
        capture.Set(VideoCaptureProperties.Fps, 20);
        capture.Set(VideoCaptureProperties.BufferSize, 1);
        capture.Set(OrientationAutoProperty, 0);

        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"USB camera {cameraIndex} kon niet worden geopend.");
        }

        var width = capture.Get(VideoCaptureProperties.FrameWidth);
        var height = capture.Get(VideoCaptureProperties.FrameHeight);
        var fps = capture.Get(VideoCaptureProperties.Fps);
        var backend = capture.Get(BackendProperty);
        lastOrientationAuto = capture.Get(OrientationAutoProperty);
        lastOrientationMeta = capture.Get(OrientationMetaProperty);
        frameCounter = 0;

        Diagnostics =
            $"source={Id} | backend={backend:F0} ({api}) | {width:F0}x{height:F0}@{fps:F1} | orientation_auto={lastOrientationAuto:F0} | orientation_meta={lastOrientationMeta:F0}";

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        capture?.Release();
        return Task.CompletedTask;
    }

    public Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (capture is null || !capture.IsOpened())
        {
            return Task.FromResult<FramePacket?>(null);
        }

        using var frame = new Mat();
        if (!capture.Read(frame) || frame.Empty())
        {
            return Task.FromResult<FramePacket?>(null);
        }

        frameCounter++;
        if (frameCounter % 60 == 0)
        {
            var orientationAuto = capture.Get(OrientationAutoProperty);
            var orientationMeta = capture.Get(OrientationMetaProperty);

            if (lastOrientationAuto is not null && lastOrientationMeta is not null)
            {
                if (!NearlyEqual(lastOrientationAuto.Value, orientationAuto) || !NearlyEqual(lastOrientationMeta.Value, orientationMeta))
                {
                    pendingDiagnosticEvent =
                        $"Camera orientation property change gedetecteerd: auto {lastOrientationAuto:F0}->{orientationAuto:F0}, meta {lastOrientationMeta:F0}->{orientationMeta:F0}";
                }
            }

            lastOrientationAuto = orientationAuto;
            lastOrientationMeta = orientationMeta;
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
        var message = pendingDiagnosticEvent;
        pendingDiagnosticEvent = null;
        return message;
    }

    public ValueTask DisposeAsync()
    {
        capture?.Dispose();
        capture = null;
        return ValueTask.CompletedTask;
    }

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.0001;
}
