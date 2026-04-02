using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;

namespace ObjectTracker.Vision.Source;

public sealed class MockFrameSource : IFrameSource
{
    private int _tick;
    private bool _running;

    public string Id => "mock";
    public string DisplayName => "Mock Source";
    public string Diagnostics => "mock source | synthetic frames | orientation=n/a";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;
        _tick = 0;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        return Task.CompletedTask;
    }

    public async Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (!_running)
        {
            return null;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        await Task.Delay(33);

        if (!_running || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        using var image = new Mat(new Size(640, 480), MatType.CV_8UC3, new Scalar(30, 30, 30));
        var x = 50 + (_tick % 540);
        var y = 240 + (int)(Math.Sin(_tick / 25.0) * 120);
        Cv2.Circle(image, new Point(x, y), 25, Scalar.Red, -1);
        Cv2.PutText(image, "MOCK", new Point(20, 40), HersheyFonts.HersheySimplex, 1.0, Scalar.White, 2);
        _tick += 4;

        Cv2.ImEncode(".jpg", image, out var bytes, [new ImageEncodingParam(ImwriteFlags.JpegQuality, 80)]);

        return new FramePacket(
            Id,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            image.Width,
            image.Height,
            bytes);
    }

    public string? ConsumeDiagnosticEvent() => null;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}