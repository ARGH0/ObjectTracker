using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileFeedCoordinatorTests
{
    /// <summary>
    /// <description>Feature: CameraTileFeedCoordinator.ApplyAsync keeps existing consumers running when the same USB camera source is added again.
    /// 
    ///   Scenario: A new USB camera source is added to an existing set of requests, the original consumer should not be stopped and a new one started for the additional source.
    ///     Given a CameraTileFeedCoordinator with start/stop tracking callbacks,
    ///      And ApplyAsync is called once with "usb:0:ANY",
    ///      And ApplyAsync is called again with "usb:0:ANY" and "usb:1:ANY",
    ///     Then starts should contain both "usb:0:ANY" and "usb:1:ANY",
    ///      And stops should be empty.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraTileFeedCoordinator.ApplyAsync restarts a consumer when the feed kind changes.
    /// 
    ///   Scenario: A USB camera source request is replaced with a video file request for the same camera ID, the old consumer should be stopped and a new one started.
    ///     Given a CameraTileFeedCoordinator with start/stop tracking callbacks,
    ///      And ApplyAsync is called once with "usb:0:ANY" as Usb,
    ///      And ApplyAsync is called again with "usb:0:ANY" as VideoFile,
    ///     Then starts should contain both the Usb and VideoFile variants of "usb:0:ANY",
    ///      And stops should contain "usb:0:ANY".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraTileFeedCoordinator.ApplyAsync stops consumers after a grace period when the camera source is removed.
    /// 
    ///   Scenario: A USB camera source request is removed from all requests, the consumer should be stopped only after the configured grace period elapses.
    ///     Given a CameraTileFeedCoordinator with a ControlledDelay and a stop grace period of 2 seconds,
    ///      And ApplyAsync is called once with "usb:0:ANY",
    ///      And ApplyAsync is called again with an empty list,
    ///      And the next controlled delay is completed,
    ///     Then stops should contain "usb:0:ANY" after waiting for the condition.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraTileFeedCoordinator.ApplyAsync cancels pending stop when a camera source returns before the grace period expires.
    /// 
    ///   Scenario: A USB camera source is removed and then re-added within the grace period, no consumer should be stopped.
    ///     Given a CameraTileFeedCoordinator with a ControlledDelay and a stop grace period of 2 seconds,
    ///      And ApplyAsync is called once with "usb:0:ANY",
    ///      And ApplyAsync is called again with an empty list,
    ///      And ApplyAsync is called again with "usb:0:ANY" before the delay completes,
    ///     Then starts should contain only one entry for "usb:0:ANY",
    ///      And stops should be empty.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: CameraTileFeedCoordinator.ApplyAsync keeps existing consumers running when a video file camera source is added again.
    /// 
    ///   Scenario: A new video file camera source is added to an existing set of requests, the original consumer should not be stopped and a new one started for the additional source.
    ///     Given a CameraTileFeedCoordinator with start/stop tracking callbacks,
    ///      And ApplyAsync is called once with "file-a" as VideoFile,
    ///      And ApplyAsync is called again with "file-a" as VideoFile and "usb:0:ANY" as Usb,
    ///     Then starts should contain both "file-a:VideoFile" and "usb:0:ANY:Usb",
    ///      And stops should be empty.</description>
    /// </summary>
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
