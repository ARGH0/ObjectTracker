using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class FileCameraSourcePlaybackSessionTests
{
    [Fact]
    public void ReadNextFrame_StopsNoLoopSourcesAndRestartsLoopSourcesAtEndOfVideo()
    {
        var noLoopReader = new FakeVideoFrameReader(readsBeforeEnd: 1);
        var loopReader = new FakeVideoFrameReader(readsBeforeEnd: 1);
        var noLoop = new FileCameraSourcePlaybackSession(loopVideo: false, noLoopReader);
        var loop = new FileCameraSourcePlaybackSession(loopVideo: true, loopReader);

        Assert.Equal(FileCameraSourcePlaybackStep.FrameAvailable, noLoop.ReadNextFrame());
        Assert.Equal(FileCameraSourcePlaybackStep.Ended, noLoop.ReadNextFrame());

        Assert.Equal(FileCameraSourcePlaybackStep.FrameAvailable, loop.ReadNextFrame());
        Assert.Equal(FileCameraSourcePlaybackStep.FrameAvailable, loop.ReadNextFrame());
        Assert.Equal(1, loopReader.RestartCount);
    }

    private sealed class FakeVideoFrameReader(int readsBeforeEnd) : IVideoFrameReader
    {
        private int readCount;

        public int RestartCount { get; private set; }

        public bool TryReadFrame()
        {
            if (readCount >= readsBeforeEnd)
            {
                return false;
            }

            readCount++;
            return true;
        }

        public void Restart()
        {
            RestartCount++;
            readCount = 0;
        }
    }
}
