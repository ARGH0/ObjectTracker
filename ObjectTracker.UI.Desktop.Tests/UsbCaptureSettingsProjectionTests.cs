using ObjectTracker.UI.Desktop;
using ObjectTracker.Vision.Source;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCaptureSettingsProjectionTests
{
    [Fact]
    public void Build_WhenSelectedSourceIsNotUsb_HidesUsbCaptureSettings()
    {
        var projection = UsbCaptureSettingsProjection.Build(
            isUsbCameraSource: false,
            isVisible: true,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Stopped, false, null, null, null, null, null),
            requested: new UsbCaptureSettingsRequest(640, 480, 30));

        Assert.False(projection.IsVisible);
    }

    [Fact]
    public void Build_WhenActualModeDiffersFromRequested_ShowsFallbackStatus()
    {
        var projection = UsbCaptureSettingsProjection.Build(
            isUsbCameraSource: true,
            isVisible: true,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, false, 1, 640, 480, 20, null, ActualFps: 20),
            requested: new UsbCaptureSettingsRequest(1280, 720, 60));

        Assert.True(projection.IsVisible);
        Assert.Equal("Requested: 1280x720@60; running: 640x480@20", projection.ModeStatusText);
    }

    [Fact]
    public void BuildApplyDecision_WhenVisibleRunningSource_RequestsRestartNow()
    {
        var decision = UsbCaptureSettingsProjection.BuildApplyDecision(
            isUsbCameraSource: true,
            isVisible: true,
            isIncludedInVisionPipeline: false,
            isActivelyProcessedByVisionPipeline: false,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, false, 1, 640, 480, 20, null));

        Assert.True(decision.ShouldRestartCameraSource);
    }

    [Fact]
    public void BuildApplyDecision_WhenSourceFailed_DoesNotAutoRestart()
    {
        var decision = UsbCaptureSettingsProjection.BuildApplyDecision(
            isUsbCameraSource: true,
            isVisible: true,
            isIncludedInVisionPipeline: false,
            isActivelyProcessedByVisionPipeline: false,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Failed, false, null, null, null, null, "camera unavailable"));

        Assert.False(decision.ShouldRestartCameraSource);
        Assert.Equal("Use Restart Camera Source to retry failed USB hardware.", decision.Message);
    }

    [Fact]
    public void BuildApplyDecision_WhenActivelyProcessedByVisionPipeline_MarksPendingRestartWithoutImmediateRestart()
    {
        var decision = UsbCaptureSettingsProjection.BuildApplyDecision(
            isUsbCameraSource: true,
            isVisible: true,
            isIncludedInVisionPipeline: true,
            isActivelyProcessedByVisionPipeline: true,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, false, 1, 640, 480, 20, null));

        Assert.False(decision.ShouldRestartCameraSource);
        Assert.True(decision.RequiresVisionPipelineRestart);
        Assert.Equal("USB capture settings saved, pending Vision Pipeline restart.", decision.Message);
    }

    [Fact]
    public void BuildApplyDecision_WhenHiddenAndExcluded_DefersUntilNextOwnerStart()
    {
        var decision = UsbCaptureSettingsProjection.BuildApplyDecision(
            isUsbCameraSource: true,
            isVisible: false,
            isIncludedInVisionPipeline: false,
            isActivelyProcessedByVisionPipeline: false,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Stopped, false, null, null, null, null, null));

        Assert.False(decision.ShouldRestartCameraSource);
        Assert.True(decision.ShouldDeferUntilNextOwnerStart);
        Assert.False(decision.RequiresVisionPipelineRestart);
    }
}
