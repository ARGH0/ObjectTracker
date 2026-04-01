using System;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.UI.Desktop;

public sealed class AvaloniaOutputPort : IOutputPort
{
    public event Action<PipelineSnapshot>? SnapshotPublished;
    public event Action<string>? StatusPublished;

    public Task PublishSnapshotAsync(PipelineSnapshot snapshot, CancellationToken cancellationToken)
    {
        SnapshotPublished?.Invoke(snapshot);
        return Task.CompletedTask;
    }

    public Task PublishStatusAsync(string status, CancellationToken cancellationToken)
    {
        StatusPublished?.Invoke(status);
        return Task.CompletedTask;
    }
}