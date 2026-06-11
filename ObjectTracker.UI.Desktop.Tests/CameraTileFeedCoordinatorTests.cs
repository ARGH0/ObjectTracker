using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileFeedCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_WhenVideoFileSourceIsAdded_KeepsExistingConsumerRunning()
    {
        var starts = new List<string>();
        var stops = new List<string>();
        await using var coordinator = new CameraTileFeedCoordinator(
            startConsumer: request =>
            {
                starts.Add(request.CameraId);
                return new RecordingConsumer(request.CameraId, stops);
            });

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile),
            new CameraTileFeedRequest("file-b", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);

        Assert.Equal(new[] { "file-a", "file-b" }, starts);
        Assert.Empty(stops);
    }

    [Fact]
    public async Task ApplyAsync_WhenVideoFileSourceIsRemoved_StopsAfterGracePeriod()
    {
        var delay = new ControlledDelay();
        var stops = new List<string>();
        await using var coordinator = new CameraTileFeedCoordinator(
            startConsumer: request => new RecordingConsumer(request.CameraId, stops),
            stopGracePeriod: TimeSpan.FromSeconds(2),
            delay: delay.WaitAsync);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);

        await coordinator.ApplyAsync(Array.Empty<CameraTileFeedRequest>(), CancellationToken.None);
        Assert.Empty(stops);

        delay.CompleteNextDelay();
        await WaitUntilAsync(() => stops.Count == 1);

        Assert.Equal(new[] { "file-a" }, stops);
    }

    [Fact]
    public async Task ApplyAsync_WhenVideoFileSourceReturnsBeforeGracePeriod_CancelsPendingStop()
    {
        var delay = new ControlledDelay();
        var starts = new List<string>();
        var stops = new List<string>();
        await using var coordinator = new CameraTileFeedCoordinator(
            startConsumer: request =>
            {
                starts.Add(request.CameraId);
                return new RecordingConsumer(request.CameraId, stops);
            },
            stopGracePeriod: TimeSpan.FromSeconds(2),
            delay: delay.WaitAsync);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);
        await coordinator.ApplyAsync(Array.Empty<CameraTileFeedRequest>(), CancellationToken.None);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);
        delay.CompleteNextDelay();
        await Task.Yield();

        Assert.Equal(new[] { "file-a" }, starts);
        Assert.Empty(stops);
    }

    [Fact]
    public async Task ApplyAsync_WhenVideoFileSourceIsAdded_KeepsExistingVideoFileConsumerRunning()
    {
        var starts = new List<string>();
        var stops = new List<string>();
        await using var coordinator = new CameraTileFeedCoordinator(
            startConsumer: request =>
            {
                starts.Add($"{request.CameraId}:{request.Kind}");
                return new RecordingConsumer(request.CameraId, stops);
            });

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile),
            new CameraTileFeedRequest("file-b", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);

        Assert.Equal(new[] { "file-a:VideoFile", "file-b:VideoFile" }, starts);
        Assert.Empty(stops);
    }

    private sealed class RecordingConsumer(string cameraId, List<string> stops) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            stops.Add(cameraId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ControlledDelay
    {
        private readonly Queue<TaskCompletionSource<object?>> pendingDelays = new();

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            pendingDelays.Enqueue(tcs);
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        public void CompleteNextDelay()
        {
            pendingDelays.Dequeue().TrySetResult(null);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }
    }
}
