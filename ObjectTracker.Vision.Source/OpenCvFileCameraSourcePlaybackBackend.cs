using OpenCvSharp;
using VideoCapture = OpenCvSharp.VideoCapture;
using VideoCaptureProperties = OpenCvSharp.VideoCaptureProperties;

namespace ObjectTracker.Vision.Source;

public sealed class OpenCvFileCameraSourcePlaybackBackend : IFileCameraSourcePlaybackBackend
{
    public ValueTask<IFileCameraSourcePlaybackSession> OpenAsync(
        FileCameraSourceKey key,
        FileCameraSourcePlaybackSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key.VideoPath) || !File.Exists(key.VideoPath))
        {
            throw new FileNotFoundException("Video Camera Source file not found.", key.VideoPath);
        }

        var capture = new VideoCapture(key.VideoPath);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            throw new InvalidOperationException($"Unable to open video Camera Source: {Path.GetFileName(key.VideoPath)}");
        }

        return ValueTask.FromResult<IFileCameraSourcePlaybackSession>(new OpenCvFileCameraSourcePlaybackSession(key, settings, capture));
    }
}

internal sealed class OpenCvFileCameraSourcePlaybackSession : IFileCameraSourcePlaybackSession
{
    private readonly FileCameraSourceKey key;
    private readonly FileCameraSourcePlaybackSettings settings;
    private readonly VideoCapture capture;
    private readonly Mat frame = new();
    private readonly int frameIntervalMs;
    private bool firstFrame = true;

    public OpenCvFileCameraSourcePlaybackSession(
        FileCameraSourceKey key,
        FileCameraSourcePlaybackSettings settings,
        VideoCapture capture)
    {
        this.key = key;
        this.settings = settings;
        this.capture = capture;
        var sourceFps = capture.Get(VideoCaptureProperties.Fps);
        frameIntervalMs = sourceFps > 0.1
            ? Math.Max(1, (int)Math.Round(1000d / sourceFps))
            : 33;
    }

    public async ValueTask<FileCameraSourceFrame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (!firstFrame)
        {
            if (await DelayUntilNextFrameOrCancellationAsync(frameIntervalMs, cancellationToken))
            {
                return null;
            }
        }

        firstFrame = false;

        if (!capture.Read(frame) || frame.Empty())
        {
            if (!settings.LoopVideo)
            {
                return null;
            }

            capture.Set(VideoCaptureProperties.PosFrames, 0);
            if (!capture.Read(frame) || frame.Empty())
            {
                return null;
            }
        }

        Cv2.ImEncode(".jpg", frame, out var encoded);
        return new FileCameraSourceFrame(
            key.CameraId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            frame.Width,
            frame.Height,
            encoded);
    }

    private static async Task<bool> DelayUntilNextFrameOrCancellationAsync(int delayMs, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        var cancellationSignal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = cancellationToken.UnsafeRegister(
            static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
            cancellationSignal);
        var completed = await Task.WhenAny(Task.Delay(delayMs), cancellationSignal.Task);
        return completed == cancellationSignal.Task;
    }

    public ValueTask DisposeAsync()
    {
        frame.Dispose();
        capture.Dispose();
        return ValueTask.CompletedTask;
    }
}
