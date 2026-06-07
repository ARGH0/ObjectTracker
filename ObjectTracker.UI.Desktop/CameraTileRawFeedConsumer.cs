using System;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectTracker.UI.Desktop;

public readonly record struct CameraTileRawFrameSnapshot(
    string CameraId,
    long FrameVersion,
    int Width,
    int Height,
    byte[] EncodedJpeg);

public interface ICameraTileRawFrameFeed
{
    CameraTileRawFrameSnapshot? LatestFrame { get; }

    Task<CameraTileRawFrameSnapshot?> WaitForNextFrameAsync(long previousVersion, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class CameraTileRawFeedConsumer
{
    private static readonly TimeSpan FrameWaitTimeout = TimeSpan.FromMilliseconds(250);

    public async Task<long> RenderNextFrameAsync(
        ICameraTileRawFrameFeed feed,
        long previousVersion,
        Func<CameraTileRawFrameSnapshot, Task> onFrame,
        CancellationToken cancellationToken)
    {
        var snapshot = await feed.WaitForNextFrameAsync(previousVersion, FrameWaitTimeout, cancellationToken)
            ?? feed.LatestFrame;
        if (snapshot is null)
        {
            return previousVersion;
        }

        await onFrame(snapshot.Value);
        return snapshot.Value.FrameVersion;
    }
}
