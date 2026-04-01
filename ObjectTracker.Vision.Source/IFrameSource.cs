using ObjectTracker.Core.Domain;

namespace ObjectTracker.Vision.Source;

public interface IFrameSource : IAsyncDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Diagnostics { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken);
    string? ConsumeDiagnosticEvent();
}
