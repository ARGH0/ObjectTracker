using ObjectTracker.UI.Desktop;
using ObjectTracker.Vision.Source;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraSourceStatusProjectionTests
{
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
