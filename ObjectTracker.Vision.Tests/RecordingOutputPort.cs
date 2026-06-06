using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.Vision.Tests;

internal sealed class RecordingOutputPort : IOutputPort
{
    private readonly List<PipelineSnapshot> snapshots = [];
    private readonly List<string> statuses = [];
    private readonly Lock sync = new();
    private readonly TaskCompletionSource<PipelineSnapshot> firstSnapshot = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<PipelineSnapshot> Snapshots
    {
        get
        {
            lock (sync)
            {
                return snapshots.ToList();
            }
        }
    }

    public IReadOnlyList<string> Statuses
    {
        get
        {
            lock (sync)
            {
                return statuses.ToList();
            }
        }
    }

    public Task PublishSnapshotAsync(PipelineSnapshot snapshot, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            snapshots.Add(snapshot);
        }

        firstSnapshot.TrySetResult(snapshot);
        return Task.CompletedTask;
    }

    public Task PublishStatusAsync(string status, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            statuses.Add(status);
        }

        return Task.CompletedTask;
    }

    public async Task<PipelineSnapshot> WaitForSnapshotAsync()
    {
        var completed = await Task.WhenAny(firstSnapshot.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        if (completed != firstSnapshot.Task)
        {
            throw new TimeoutException("Expected the Vision Pipeline to publish a snapshot.");
        }

        return await firstSnapshot.Task;
    }

    public async Task<IReadOnlyList<PipelineSnapshot>> WaitForSnapshotsAsync(int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            var current = Snapshots;
            if (current.Count >= count)
            {
                return current;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"Expected the Vision Pipeline to publish {count} snapshots.");
    }
}
