using OpenCvSharp;

namespace ObjectTracker.Vision.Source;

public sealed class OpenCvUsbCaptureBackend : IUsbCaptureBackend
{
    public ValueTask<IUsbCaptureSession> OpenAsync(UsbCameraKey key, UsbCaptureSettings settings, CancellationToken cancellationToken)
    {
        var api = Enum.TryParse<VideoCaptureAPIs>(key.Api, ignoreCase: true, out var parsedApi)
            ? parsedApi
            : VideoCaptureAPIs.ANY;

        var capture = new VideoCapture(key.CameraIndex, api);
        capture.Set(VideoCaptureProperties.FrameWidth, settings.Width);
        capture.Set(VideoCaptureProperties.FrameHeight, settings.Height);
        capture.Set(VideoCaptureProperties.Fps, settings.TargetFps);
        capture.Set(VideoCaptureProperties.BufferSize, 1);

        if (!capture.IsOpened())
        {
            capture.Dispose();
            throw new InvalidOperationException($"USB camera {key.CameraIndex} could not be opened.");
        }

        return ValueTask.FromResult<IUsbCaptureSession>(new OpenCvUsbCaptureSession(key, capture));
    }
}

internal sealed class OpenCvUsbCaptureSession : IUsbCaptureSession
{
    private readonly UsbCameraKey key;
    private readonly VideoCapture capture;

    public OpenCvUsbCaptureSession(UsbCameraKey key, VideoCapture capture)
    {
        this.key = key;
        this.capture = capture;
    }

    public ValueTask<UsbCapturedFrame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (!capture.IsOpened())
        {
            return ValueTask.FromResult<UsbCapturedFrame?>(null);
        }

        using var frame = new Mat();
        if (!capture.Read(frame) || frame.Empty())
        {
            return ValueTask.FromResult<UsbCapturedFrame?>(null);
        }

        Cv2.ImEncode(".jpg", frame, out var bytes, [new ImageEncodingParam(ImwriteFlags.JpegQuality, 80)]);
        return ValueTask.FromResult<UsbCapturedFrame?>(new UsbCapturedFrame(
            UsbCameraSourceId.Format(key),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            frame.Width,
            frame.Height,
            bytes));
    }

    public ValueTask DisposeAsync()
    {
        capture.Release();
        capture.Dispose();
        return ValueTask.CompletedTask;
    }
}
