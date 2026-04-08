using ObjectTracker.Core.Domain;

namespace ObjectTracker.Core.Ports;

public interface IOutputPort
{
    Task PublishSnapshotAsync(PipelineSnapshot snapshot, CancellationToken cancellationToken);
    Task PublishStatusAsync(string status, CancellationToken cancellationToken);
}