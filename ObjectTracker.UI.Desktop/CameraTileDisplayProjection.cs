namespace ObjectTracker.UI.Desktop;

public enum CameraTileMissingFrameBehavior
{
    RepeatLastFrame,
    BlackFrame
}

public enum CameraTileFrameDisplay
{
    CurrentFrame,
    LastFrame,
    BlackFrame,
    Placeholder
}

public readonly record struct CameraTileDisplayView(
    CameraTileMissingFrameBehavior MissingFrameBehavior,
    CameraTileFrameDisplay FrameDisplay,
    string PlaceholderText,
    bool ShowCameraSourceStatusOverlay,
    bool ShowVisionPipelineLaneStatusOverlay);

public static class CameraTileDisplayProjection
{
    public static CameraTileDisplayView Build(
        bool hasCurrentFrame,
        bool hasLastFrame,
        CameraTileMissingFrameBehavior missingFrameBehavior = CameraTileMissingFrameBehavior.RepeatLastFrame,
        MainWindow.CameraTileFrameSource frameSource = MainWindow.CameraTileFrameSource.RawCameraSourceFeed,
        CameraSourceStatus? cameraSourceStatus = null,
        VisionPipelineLaneStatus? visionPipelineLaneStatus = null)
    {
        var frameDisplay = GetFrameDisplay(hasCurrentFrame, hasLastFrame, missingFrameBehavior);

        return new CameraTileDisplayView(
            missingFrameBehavior,
            frameDisplay,
            frameDisplay == CameraTileFrameDisplay.Placeholder ? "Waiting for first frame" : string.Empty,
            ShowCameraSourceStatusOverlay: IsOverlayState(cameraSourceStatus?.State),
            ShowVisionPipelineLaneStatusOverlay: frameSource != MainWindow.CameraTileFrameSource.RawCameraSourceFeed && IsOverlayState(visionPipelineLaneStatus?.State));
    }

    private static bool IsOverlayState(CameraSourceStatusState? state)
    {
        return state is CameraSourceStatusState.Stale or CameraSourceStatusState.Failed;
    }

    private static bool IsOverlayState(VisionPipelineLaneStatusState? state)
    {
        return state is VisionPipelineLaneStatusState.Stale or VisionPipelineLaneStatusState.Failed;
    }

    private static CameraTileFrameDisplay GetFrameDisplay(
        bool hasCurrentFrame,
        bool hasLastFrame,
        CameraTileMissingFrameBehavior missingFrameBehavior)
    {
        if (hasCurrentFrame)
        {
            return CameraTileFrameDisplay.CurrentFrame;
        }

        if (!hasLastFrame)
        {
            return CameraTileFrameDisplay.Placeholder;
        }

        return missingFrameBehavior == CameraTileMissingFrameBehavior.BlackFrame
            ? CameraTileFrameDisplay.BlackFrame
            : CameraTileFrameDisplay.LastFrame;
    }
}
