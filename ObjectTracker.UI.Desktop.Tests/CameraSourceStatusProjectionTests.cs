using ObjectTracker.UI.Desktop;
using ObjectTracker.Vision.Source;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraSourceStatusProjectionTests
{
    /// <summary>
    /// <description>Feature: CameraSourceStatusProjection.BuildUsbStatus projects correct status text and flags for a running USB camera source.
    /// 
    ///   Scenario: A USB camera is running normally with no staleness, frame age should be hidden and restart enabled.
    ///     Given a UsbCameraRuntimeStatus in Running state with IsStale false, LatestFrameVersion 12, dimensions 640x480, FrameAgeMs 20, and null failure message,
    ///      And BuildUsbStatus is called with isActivelyProcessedByVisionPipeline set to false,
    ///     Then StatusText should be "USB Camera Source: running",
    ///      And ShowFrameAge should be false,
    ///      And RestartEnabled should be true,
    ///      And ShowPlaceholder should be false.</description>
    /// </summary>
    [Fact]
    public void BuildUsbStatus_WhenRunningNormally_HidesFrameAgeAndEnablesRestart()
    {
        var status = new UsbCameraRuntimeStatus(
            UsbCameraOwnerState.Running,
            IsStale: false,
            LatestFrameVersion: 12,
            Width: 640,
            Height: 480,
            FrameAgeMs: 20,
            FailureMessage: null);

        var projection = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: false,
            status);

        Assert.Equal("USB Camera Source: running", projection.StatusText);
        Assert.False(projection.ShowFrameAge);
        Assert.True(projection.RestartEnabled);
        Assert.False(projection.ShowPlaceholder);
    }

    /// <summary>
    /// <description>Feature: CameraSourceStatusProjection.BuildUsbStatus shows frame age when the camera source is stale.
    /// 
    ///   Scenario: A USB camera is running but its frames are stale.
    ///     Given a UsbCameraRuntimeStatus in Running state with IsStale true, LatestFrameVersion 12, dimensions 640x480, FrameAgeMs 1500, and null failure message,
    ///      And BuildUsbStatus is called with isActivelyProcessedByVisionPipeline set to false,
    ///     Then StatusText should be "USB Camera Source: stale (1500 ms since last frame)",
    ///      And ShowFrameAge should be true.</description>
    /// </summary>
    [Fact]
    public void BuildUsbStatus_WhenStale_ShowsFrameAge()
    {
        var status = new UsbCameraRuntimeStatus(
            UsbCameraOwnerState.Running,
            IsStale: true,
            LatestFrameVersion: 12,
            Width: 640,
            Height: 480,
            FrameAgeMs: 1500,
            FailureMessage: null);

        var projection = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: false,
            status);

        Assert.Equal("USB Camera Source: stale (1500 ms since last frame)", projection.StatusText);
        Assert.True(projection.ShowFrameAge);
    }

    /// <summary>
    /// <description>Feature: CameraSourceStatusProjection.BuildUsbStatus shows placeholder and failure message for a failed USB camera source.
    /// 
    ///   Scenario: A USB camera has entered the Failed state with a failure message.
    ///     Given a UsbCameraRuntimeStatus in Failed state with null LatestFrameVersion, null dimensions, null FrameAgeMs, and FailureMessage "camera unavailable",
    ///      And BuildUsbStatus is called with isActivelyProcessedByVisionPipeline set to false,
    ///     Then StatusText should be "USB Camera Source: failed - camera unavailable",
    ///      And ShowPlaceholder should be true,
    ///      And RaisesAmbiguityAlert should be false.</description>
    /// </summary>
    [Fact]
    public void BuildUsbStatus_WhenFailed_ShowsPlaceholderAndFailureWithoutAmbiguityAlert()
    {
        var status = new UsbCameraRuntimeStatus(
            UsbCameraOwnerState.Failed,
            IsStale: false,
            LatestFrameVersion: null,
            Width: null,
            Height: null,
            FrameAgeMs: null,
            FailureMessage: "camera unavailable");

        var projection = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: false,
            status);

        Assert.Equal("USB Camera Source: failed - camera unavailable", projection.StatusText);
        Assert.True(projection.ShowPlaceholder);
        Assert.False(projection.RaisesAmbiguityAlert);
    }

    /// <summary>
    /// <description>Feature: CameraSourceStatusProjection.BuildUsbStatus disables restart when the camera source is actively processed by Vision Pipeline.
    /// 
    ///   Scenario: A USB camera is running but currently being actively processed by the Vision Pipeline.
    ///     Given a UsbCameraRuntimeStatus in Running state with IsStale false, LatestFrameVersion 1, dimensions 640x480, FrameAgeMs 20, and null failure message,
    ///      And BuildUsbStatus is called with isActivelyProcessedByVisionPipeline set to true,
    ///     Then RestartEnabled should be false,
    ///      And RestartDisabledReason should be "Stop Vision Pipeline to restart this camera source.".</description>
    /// </summary>
    [Fact]
    public void BuildUsbStatus_WhenActivelyProcessedByVisionPipeline_DisablesRestart()
    {
        var projection = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: true,
            new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, false, 1, 640, 480, 20, null));

        Assert.False(projection.RestartEnabled);
        Assert.Equal("Stop Vision Pipeline to restart this camera source.", projection.RestartDisabledReason);
    }

    /// <summary>
    /// <description>Feature: CameraSourceStatusProjection.BuildUsbStatus handles stopped state correctly.
    /// 
    ///   Scenario: A USB camera is in the Stopped state.
    ///     Given a UsbCameraRuntimeStatus in Stopped state with null LatestFrameVersion, null dimensions, null FrameAgeMs, and null failure message,
    ///      And BuildUsbStatus is called with isActivelyProcessedByVisionPipeline set to false,
    ///     Then StatusText should be "USB Camera Source: stopped",
    ///      And ShowPlaceholder should be false.</description>
    /// </summary>
    [Fact]
    public void BuildUsbStatus_WhenStopped_DoesNotShowTilePlaceholder()
    {
        var projection = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: false,
            new UsbCameraRuntimeStatus(UsbCameraOwnerState.Stopped, false, null, null, null, null, null));

        Assert.Equal("USB Camera Source: stopped", projection.StatusText);
        Assert.False(projection.ShowPlaceholder);
    }

    /// <summary>
    /// <description>Feature: CameraSourceStatusProjection.BuildFileStatus shows frame age when a file camera source is stale.
    /// 
    ///   Scenario: A file-based camera source is running but its frames are stale.
    ///     Given a FileCameraSourceRuntimeStatus in Running state with IsStale true, LatestFrameVersion 3, dimensions 640x480, FrameAgeMs 1500, and null failure message,
    ///      And BuildFileStatus is called with isFileCameraSource set to true,
    ///     Then StatusText should be "File Camera Source: stale (1500 ms since last frame)",
    ///      And ShowFrameAge should be true,
    ///      And RaisesAmbiguityAlert should be false.</description>
    /// </summary>
    [Fact]
    public void BuildFileStatus_WhenStale_ShowsFrameAgeWithoutAmbiguityAlert()
    {
        var projection = CameraSourceStatusProjection.BuildFileStatus(
            isFileCameraSource: true,
            new FileCameraSourceRuntimeStatus(FileCameraSourceFeedState.Running, true, 3, 640, 480, 1500, null));

        Assert.Equal("File Camera Source: stale (1500 ms since last frame)", projection.StatusText);
        Assert.True(projection.ShowFrameAge);
        Assert.False(projection.RaisesAmbiguityAlert);
    }
}
