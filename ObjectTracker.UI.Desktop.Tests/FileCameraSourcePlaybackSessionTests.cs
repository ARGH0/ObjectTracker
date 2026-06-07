using ObjectTracker.UI.Desktop.Enums;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class FileCameraSourcePlaybackSessionTests
{
    /// <summary>
    /// <description>Feature: FileCameraSourcePlaybackSession.ReadNextFrame stops non-looping sources and restarts looping ones at end of video.
    /// 
    ///   Scenario: A non-looping source ends after one frame, while a looping source restarts when it reaches the end.
    ///     Given a FakeVideoFrameReader that yields 1 read before ending for both noLoopReader and loopReader,
    ///      And a FileCameraSourcePlaybackSession with loopVideo=false using noLoopReader,
    ///      And a FileCameraSourcePlaybackSession with loopVideo=true using loopReader,
    ///     When ReadNextFrame() is called twice on the non-looping session,
    ///     Then the first call should return FrameAvailable and the second Ended,
    ///      And the looping session should return FrameAvailable for both calls,
    ///      And loopReader.RestartCount should be 1.</description>
    /// </summary>
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
