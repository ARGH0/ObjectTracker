using ObjectTracker.UI.Desktop.Enums;

namespace ObjectTracker.UI.Desktop;

internal interface IVideoFrameReader
{
    bool TryReadFrame();

    void Restart();
}

internal sealed class FileCameraSourcePlaybackSession(bool loopVideo, IVideoFrameReader reader)
{
    public FileCameraSourcePlaybackStep ReadNextFrame()
    {
        if (reader.TryReadFrame())
        {
            return FileCameraSourcePlaybackStep.FrameAvailable;
        }

        if (!loopVideo)
        {
            return FileCameraSourcePlaybackStep.Ended;
        }

        reader.Restart();
        return reader.TryReadFrame()
            ? FileCameraSourcePlaybackStep.FrameAvailable
            : FileCameraSourcePlaybackStep.Ended;
    }
}
