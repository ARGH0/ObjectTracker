using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Enums;

namespace ObjectTracker.UI.Desktop;

public readonly record struct CameraTileFeedRequest(string CameraId, CameraTileFeedKind Kind);

public sealed class CameraTileFeedCoordinator : IAsyncDisposable
{
    private readonly Func<CameraTileFeedRequest, IAsyncDisposable> startConsumer;
    private readonly TimeSpan stopGracePeriod;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Dictionary<string, ActiveConsumer> activeConsumers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> pendingStops = new(StringComparer.OrdinalIgnoreCase);

    public CameraTileFeedCoordinator(
        Func<CameraTileFeedRequest, IAsyncDisposable> startConsumer,
        TimeSpan? stopGracePeriod = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.startConsumer = startConsumer;
        this.stopGracePeriod = stopGracePeriod ?? TimeSpan.FromSeconds(2);
        this.delay = delay ?? Task.Delay;
    }

    public async Task ApplyAsync(IReadOnlyCollection<CameraTileFeedRequest> desiredFeeds, CancellationToken cancellationToken)
    {
        var desiredById = desiredFeeds.ToDictionary(feed => feed.CameraId, StringComparer.OrdinalIgnoreCase);

        foreach (var cameraId in activeConsumers.Keys.ToList())
        {
            var active = activeConsumers[cameraId];
            if (!desiredById.TryGetValue(cameraId, out var desired))
            {
                ScheduleStop(cameraId);
                continue;
            }

            if (active.Kind != desired.Kind)
            {
                CancelPendingStop(cameraId);
                activeConsumers.Remove(cameraId);
                await active.Consumer.DisposeAsync();
            }
        }

        foreach (var request in desiredFeeds)
        {
            if (activeConsumers.ContainsKey(request.CameraId))
            {
                CancelPendingStop(request.CameraId);
                continue;
            }

            activeConsumers[request.CameraId] = new ActiveConsumer(request.Kind, startConsumer(request));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync();
    }

    public async Task StopAllAsync()
    {
        foreach (var pendingStop in pendingStops.Values)
        {
            pendingStop.Cancel();
            pendingStop.Dispose();
        }

        pendingStops.Clear();

        foreach (var active in activeConsumers.Values)
        {
            await active.Consumer.DisposeAsync();
        }

        activeConsumers.Clear();
    }

    private void ScheduleStop(string cameraId)
    {
        if (pendingStops.ContainsKey(cameraId))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        pendingStops[cameraId] = cts;
        _ = StopAfterGracePeriodAsync(cameraId, cts);
    }

    private void CancelPendingStop(string cameraId)
    {
        if (!pendingStops.Remove(cameraId, out var cts))
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private async Task StopAfterGracePeriodAsync(string cameraId, CancellationTokenSource cts)
    {
        try
        {
            await delay(stopGracePeriod, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!pendingStops.Remove(cameraId))
        {
            return;
        }

        if (activeConsumers.Remove(cameraId, out var active))
        {
            await active.Consumer.DisposeAsync();
        }

        cts.Dispose();
    }

    private readonly record struct ActiveConsumer(CameraTileFeedKind Kind, IAsyncDisposable Consumer);
}
