using ObjectTracker.Vision.Source;
using Xunit;

namespace ObjectTracker.Vision.Tests;

public sealed class FileCameraSourceFeedManagerTests
{
    [Fact]
    public async Task AcquireAsync_SameCameraSourceTwice_UsesOnePlaybackFeedTimeline()
    {
        var backend = new FakeFileCameraSourcePlaybackBackend();
        await using var manager = new FileCameraSourceFeedManager(backend);
        var source = new FileCameraSourceKey("source-bridge", "/videos/bridge.mp4");

        await using var firstLease = await manager.AcquireAsync(source, new FileCameraSourcePlaybackSettings(LoopVideo: true), CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(source, new FileCameraSourcePlaybackSettings(LoopVideo: true), CancellationToken.None);

        var firstFrame = await firstLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await secondLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, backend.GetOpenCount(source));
        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);
        Assert.Equal(firstFrame.Value.FrameVersion, secondFrame.Value.FrameVersion);
        Assert.Equal("source-bridge", firstFrame.Value.SourceId);
    }

    [Fact]
    public async Task GetStatus_AfterCameraSourceProducesFrame_ReportsRunningWithLatestFrameMetadata()
    {
        var backend = new FakeFileCameraSourcePlaybackBackend();
        await using var manager = new FileCameraSourceFeedManager(backend);
        var source = new FileCameraSourceKey("source-bridge", "/videos/bridge.mp4");

        await using var lease = await manager.AcquireAsync(source, new FileCameraSourcePlaybackSettings(LoopVideo: true), CancellationToken.None);
        var frame = await lease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        var status = manager.GetStatus(source, nowUtcMs: frame?.TimestampUtcMs ?? 0);

        Assert.Equal(FileCameraSourceFeedState.Running, status.State);
        Assert.False(status.IsStale);
        Assert.Equal(frame?.FrameVersion, status.LatestFrameVersion);
        Assert.Equal(frame?.Width, status.Width);
        Assert.Equal(frame?.Height, status.Height);
    }

    [Fact]
    public async Task AcquireAsync_DifferentCameraSources_StartsIndependentPlaybackFeeds()
    {
        var backend = new FakeFileCameraSourcePlaybackBackend();
        await using var manager = new FileCameraSourceFeedManager(backend);
        var firstSource = new FileCameraSourceKey("source-bridge", "/videos/bridge.mp4");
        var secondSource = new FileCameraSourceKey("source-yard", "/videos/yard.mp4");

        await using var firstLease = await manager.AcquireAsync(firstSource, new FileCameraSourcePlaybackSettings(LoopVideo: true), CancellationToken.None);
        await using var secondLease = await manager.AcquireAsync(secondSource, new FileCameraSourcePlaybackSettings(LoopVideo: false), CancellationToken.None);

        var firstFrame = await firstLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondFrame = await secondLease.WaitForNextFrameAsync(previousVersion: 0, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, backend.GetOpenCount(firstSource));
        Assert.Equal(1, backend.GetOpenCount(secondSource));
        Assert.Equal("source-bridge", firstFrame?.SourceId);
        Assert.Equal("source-yard", secondFrame?.SourceId);
    }

    private sealed class FakeFileCameraSourcePlaybackBackend : IFileCameraSourcePlaybackBackend
    {
        private readonly Dictionary<FileCameraSourceKey, int> openCounts = new();

        public int GetOpenCount(FileCameraSourceKey key) => openCounts.TryGetValue(key, out var count) ? count : 0;

        public ValueTask<IFileCameraSourcePlaybackSession> OpenAsync(
            FileCameraSourceKey key,
            FileCameraSourcePlaybackSettings settings,
            CancellationToken cancellationToken)
        {
            openCounts[key] = GetOpenCount(key) + 1;
            return ValueTask.FromResult<IFileCameraSourcePlaybackSession>(new FakeFileCameraSourcePlaybackSession(key));
        }
    }

    private sealed class FakeFileCameraSourcePlaybackSession(FileCameraSourceKey key) : IFileCameraSourcePlaybackSession
    {
        private bool returnedFrame;

        public ValueTask<FileCameraSourceFrame?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (returnedFrame)
            {
                return ValueTask.FromResult<FileCameraSourceFrame?>(null);
            }

            returnedFrame = true;
            return ValueTask.FromResult<FileCameraSourceFrame?>(new FileCameraSourceFrame(
                key.CameraId,
                TimestampUtcMs: 1,
                Width: 2,
                Height: 2,
                EncodedJpeg: [1, 2, 3]));
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
