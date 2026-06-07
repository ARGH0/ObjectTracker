using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileFeedCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_WhenUsbCameraSourceIsAdded_KeepsExistingConsumerRunning()
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
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb),
            new CameraTileFeedRequest("usb:1:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);

        Assert.Equal(new[] { "usb:0:ANY", "usb:1:ANY" }, starts);
        Assert.Empty(stops);
    }

    [Fact]
    public async Task ApplyAsync_WhenFeedKindChanges_RestartsConsumer()
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
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.VideoFile)
        }, CancellationToken.None);

        Assert.Equal(new[] { "usb:0:ANY:Usb", "usb:0:ANY:VideoFile" }, starts);
        Assert.Equal(new[] { "usb:0:ANY" }, stops);
    }

    [Fact]
    public async Task ApplyAsync_WhenUsbCameraSourceIsRemoved_StopsAfterGracePeriod()
    {
        var delay = new ControlledDelay();
        var stops = new List<string>();
        await using var coordinator = new CameraTileFeedCoordinator(
            startConsumer: request => new RecordingConsumer(request.CameraId, stops),
            stopGracePeriod: TimeSpan.FromSeconds(2),
            delay: delay.WaitAsync);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);

        await coordinator.ApplyAsync(Array.Empty<CameraTileFeedRequest>(), CancellationToken.None);
        Assert.Empty(stops);

        delay.CompleteNextDelay();
        await WaitUntilAsync(() => stops.Count == 1);

        Assert.Equal(new[] { "usb:0:ANY" }, stops);
    }

    [Fact]
    public async Task ApplyAsync_WhenUsbCameraSourceReturnsBeforeGracePeriod_CancelsPendingStop()
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
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);
        await coordinator.ApplyAsync(Array.Empty<CameraTileFeedRequest>(), CancellationToken.None);

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);
        delay.CompleteNextDelay();
        await Task.Yield();

        Assert.Equal(new[] { "usb:0:ANY" }, starts);
        Assert.Empty(stops);
    }

    [Fact]
    public async Task ApplyAsync_WhenUsbCameraSourceIsAdded_KeepsExistingVideoFileConsumerRunning()
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
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);

        Assert.Equal(new[] { "file-a:VideoFile", "usb:0:ANY:Usb" }, starts);
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
