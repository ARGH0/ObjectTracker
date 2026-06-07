using ObjectTracker.UI.Desktop;
using ObjectTracker.Vision.Source;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class UsbCaptureSettingsProjectionTests
{
    /// <summary>
    /// <description>Feature: UsbCaptureSettingsProjection.Build hides USB capture settings when the selected source is not a USB camera.
    /// 
    ///   Scenario: Building a projection with isUsbCameraSource=false should result in IsVisible being false regardless of other parameters.
    ///     Given Build is called with isUsbCameraSource=false, isVisible=true, status=Stopped, requested=640x480@30,
    ///     Then IsVisible should be false.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsProjection.Build shows fallback status when the actual mode differs from requested settings.
    /// 
    ///   Scenario: Building a projection with isUsbCameraSource=true, running at 640x480@20 but requesting 1280x720@60 should show both modes in ModeStatusText.
    ///     Given Build is called with isUsbCameraSource=true, isVisible=true, status=Running(640x480@20), requested=1280x720@60,
    ///     Then IsVisible should be true and ModeStatusText should be "Requested: 1280x720@60; running: 640x480@20".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsProjection.BuildApplyDecision requests immediate restart for a visible running USB source.
    /// 
    ///   Scenario: Building an apply decision for a visible, not-in-pipeline-running USB camera that is currently running should indicate it needs to be restarted now.
    ///     Given BuildApplyDecision is called with isUsbCameraSource=true, isVisible=true, isIncludedInVisionPipeline=false, isActivelyProcessedByVisionPipeline=false, status=Running(640x480@20),
    ///     Then ShouldRestartCameraSource should be true.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsProjection.BuildApplyDecision does not auto-restart a failed USB source.
    /// 
    ///   Scenario: Building an apply decision for a failed USB camera should indicate it should not be restarted automatically and provide guidance to use Restart Camera Source.
    ///     Given BuildApplyDecision is called with isUsbCameraSource=true, isVisible=true, isIncludedInVisionPipeline=false, isActivelyProcessedByVisionPipeline=false, status=Failed("camera unavailable"),
    ///     Then ShouldRestartCameraSource should be false and Message should be "Use Restart Camera Source to retry failed USB hardware.".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsProjection.BuildApplyDecision marks pending restart without immediate restart when the source is actively processed by Vision Pipeline.
    /// 
    ///   Scenario: Building an apply decision for a visible, included USB camera that is actively processed by the Vision Pipeline should defer the restart until the pipeline is restarted.
    ///     Given BuildApplyDecision is called with isUsbCameraSource=true, isVisible=true, isIncludedInVisionPipeline=true, isActivelyProcessedByVisionPipeline=true, status=Running(640x480@20),
    ///     Then ShouldRestartCameraSource should be false, RequiresVisionPipelineRestart should be true, and Message should be "USB capture settings saved, pending Vision Pipeline restart.".</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsProjection.BuildApplyDecision defers restart until next owner start when the source is hidden and excluded.
    /// 
    ///   Scenario: Building an apply decision for a visible=false, excluded USB camera in Stopped state should indicate it should defer until next owner start without requiring Vision Pipeline restart.
    ///     Given BuildApplyDecision is called with isUsbCameraSource=true, isVisible=false, isIncludedInVisionPipeline=false, isActivelyProcessedByVisionPipeline=false, status=Stopped,
    ///     Then ShouldRestartCameraSource should be false, ShouldDeferUntilNextOwnerStart should be true, and RequiresVisionPipelineRestart should be false.</description>
    /// </summary>
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

    /// <summary>
    /// <description>Feature: UsbCaptureSettingsProjection.BuildRawTileStartupSettings uses a stable baseline regardless of requested settings.
    /// 
    ///   Scenario: Building raw tile startup settings with any requested configuration should always return the same default 640x480@20 settings.
    ///     Given BuildRawTileStartupSettings is called with requested=1920x1080@60,
    ///     Then the result should be UsbCaptureSettings(640, 480, 20).</description>
    /// </summary>
    [Fact]
    public void BuildRawTileStartupSettings_UsesStableBaselineRegardlessOfRequestedSettings()
    {
        var startupSettings = UsbCaptureSettingsProjection.BuildRawTileStartupSettings(
            requested: new UsbCaptureSettingsRequest(1920, 1080, 60));

        Assert.Equal(new ObjectTracker.Vision.Source.UsbCaptureSettings(640, 480, 20), startupSettings);
    }
}
