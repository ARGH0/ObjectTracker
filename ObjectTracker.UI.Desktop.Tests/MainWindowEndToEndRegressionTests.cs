using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.UI.Desktop;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowEndToEndRegressionTests
{
    [Fact]
    public void WorkspaceAndRuntimeControls_EndToEnd_PreserveNavigationAndRuntimeValidity()
    {
        var cameraWorkspace = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Camera);
        var layersWorkspace = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Layers);
        var settingsWorkspace = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Settings);
        var stoppedMenu = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: false);
        var runningMenu = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: true);
        var pendingStatus = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            hasPendingVisionPipelineRestart: true);

        Assert.True(cameraWorkspace.CameraVisible);
        Assert.True(layersWorkspace.LayersVisible);
        Assert.True(settingsWorkspace.SettingsVisible);
        Assert.True(stoppedMenu.StartEnabled);
        Assert.False(stoppedMenu.StopEnabled);
        Assert.False(runningMenu.StartEnabled);
        Assert.True(runningMenu.StopEnabled);
        Assert.Equal("Pending restart: required", pendingStatus.PendingRestart);
    }

    [Fact]
    public void CameraWorkspaceControls_EndToEnd_ProjectCameraModesAndGuardDestructiveActions()
    {
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera("cam-raw", "Raw Camera", IsVisible: true, IsIncludedInVisionPipeline: false, DebugViewEnabled: true),
            new MainWindow.CameraWorkspaceCamera("cam-debug", "Debug Camera", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: true),
            new MainWindow.CameraWorkspaceCamera("cam-hidden", "Hidden Camera", IsVisible: false, IsIncludedInVisionPipeline: true, DebugViewEnabled: false)
        });
        var viewState = MainWindow.BuildCameraTileViewState(projection);
        var runningDestructiveState = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: true,
            hasSelectedCamera: true,
            cameraCount: 3);
        var stoppedDestructiveState = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: false,
            hasSelectedCamera: true,
            cameraCount: 1);

        Assert.Equal(new[] { "cam-raw", "cam-debug" }, viewState.CameraIds.ToArray());
        Assert.Equal(new[]
        {
            MainWindow.CameraRenderMode.RawFeed,
            MainWindow.CameraRenderMode.DebugView
        }, viewState.RenderModes.ToArray());
        Assert.False(runningDestructiveState.DeleteSelectedEnabled);
        Assert.False(runningDestructiveState.ClearAllEnabled);
        Assert.True(stoppedDestructiveState.DeleteSelectedEnabled);
        Assert.True(stoppedDestructiveState.ClearAllEnabled);
        Assert.True(stoppedDestructiveState.DeleteSelectedRequiresConfirmation);
        Assert.True(stoppedDestructiveState.ClearAllRequiresConfirmation);
    }

    [Fact]
    public void LayersAndSettings_EndToEnd_BlockInUseLayerTypeDeleteAndSignalPendingRestart()
    {
        var usage = MainWindow.BuildLayerTypeUsageProjection(
            "NO-VISION",
            new[]
            {
                new CameraZoneLayer(
                    "layer-bridge",
                    "zone-bridge",
                    "NO-VISION",
                    "Bridge No-Vision",
                    new[] { new CameraZoneRegion("region-1", "Under Bridge", null, Array.Empty<GridCell>()) })
            },
            new[] { new CameraZoneDefinition("zone-bridge", "Bridge Camera Zone") },
            new[] { new CameraZoneBinding("source-bridge", "zone-bridge") },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source-bridge"] = "Bridge Camera"
            });
        var deleteState = MainWindow.BuildLayerTypeDeleteState("NO-VISION", usage);
        var saveImpact = MainWindow.BuildSettingsSaveImpact(
            new AppSettings(GridColumns: 32, GridRows: 18),
            new AppSettings(GridColumns: 40, GridRows: 18),
            isVisionPipelineRunning: true);
        var bottomStatus = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            hasPendingVisionPipelineRestart: saveImpact.HasPendingVisionPipelineRestart);

        Assert.False(deleteState.CanDelete);
        Assert.Contains("Bridge Camera", deleteState.Message, StringComparison.Ordinal);
        Assert.Contains("Bridge No-Vision", deleteState.Message, StringComparison.Ordinal);
        Assert.True(saveImpact.RequiresVisionPipelineRestart);
        Assert.True(saveImpact.HasPendingVisionPipelineRestart);
        Assert.Equal("Settings: saved, pending Vision Pipeline restart", saveImpact.SettingsStatusText);
        Assert.Equal("Pending restart: required", bottomStatus.PendingRestart);
    }

    [Fact]
    public async Task UsbCameraSourceFlow_EndToEnd_PreservesFeedsAndProjectsOperatorDecisions()
    {
        var starts = new List<string>();
        var stops = new List<string>();
        await using var coordinator = new CameraTileFeedCoordinator(
            startConsumer: request =>
            {
                starts.Add($"{request.CameraId}:{request.Kind}");
                return new RecordingConsumer(request.CameraId, stops);
            });

        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile),
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);
        await coordinator.ApplyAsync(new[]
        {
            new CameraTileFeedRequest("file-a", CameraTileFeedKind.VideoFile),
            new CameraTileFeedRequest("usb:0:ANY", CameraTileFeedKind.Usb),
            new CameraTileFeedRequest("usb:1:ANY", CameraTileFeedKind.Usb)
        }, CancellationToken.None);

        var probed = new List<int>();
        var discovery = new UsbCameraDiscoveryService(
            maxUsbCameraIndex: 1,
            api: VideoCaptureAPIs.ANY,
            probe: index =>
            {
                probed.Add(index);
                return new UsbCameraProbeResult(IsAvailable: true, Width: 1280, Height: 720);
            });
        var discovered = discovery.DiscoverUsbCameraOptions(new[] { "usb:0:ANY" });

        var startingStatus = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: false,
            new UsbCameraRuntimeStatus(UsbCameraOwnerState.Starting, false, null, null, null, null, null));
        var staleStatus = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: false,
            new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, true, 4, 640, 480, 1500, null));
        var failedStatus = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: false,
            new UsbCameraRuntimeStatus(UsbCameraOwnerState.Failed, false, null, null, null, null, "camera unavailable"));
        var activeStatus = CameraSourceStatusProjection.BuildUsbStatus(
            isUsbCameraSource: true,
            isActivelyProcessedByVisionPipeline: true,
            new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, false, 5, 640, 480, 20, null));

        var settingsService = new UsbCaptureSettingsService(new Dictionary<string, UsbCaptureSettingsRequest>
        {
            ["usb:0:ANY"] = new(640, 480, 30)
        });
        settingsService.UpdateDraft("usb:0:ANY", new UsbCaptureSettingsRequest(1280, 720, 60));
        var applied = settingsService.ApplyDraft("usb:0:ANY");
        settingsService.UpdateDraft("usb:0:ANY", new UsbCaptureSettingsRequest(1920, 1080, 60));
        var reverted = settingsService.RevertDraft("usb:0:ANY");
        var modeProjection = UsbCaptureSettingsProjection.Build(
            isUsbCameraSource: true,
            isVisible: true,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, false, 6, 640, 480, 20, null, ActualFps: 20),
            requested: settingsService.GetRequestedSettings("usb:0:ANY"));
        var pendingDecision = UsbCaptureSettingsProjection.BuildApplyDecision(
            isUsbCameraSource: true,
            isVisible: true,
            isIncludedInVisionPipeline: true,
            isActivelyProcessedByVisionPipeline: true,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Running, false, 6, 640, 480, 20, null));
        var failedDecision = UsbCaptureSettingsProjection.BuildApplyDecision(
            isUsbCameraSource: true,
            isVisible: true,
            isIncludedInVisionPipeline: false,
            isActivelyProcessedByVisionPipeline: false,
            status: new UsbCameraRuntimeStatus(UsbCameraOwnerState.Failed, false, null, null, null, null, "camera unavailable"));

        Assert.Equal(new[]
        {
            "file-a:VideoFile",
            "usb:0:ANY:Usb",
            "usb:1:ANY:Usb"
        }, starts);
        Assert.Empty(stops);
        Assert.Equal(new[] { 1 }, probed);
        Assert.Collection(
            discovered,
            alreadyAdded =>
            {
                Assert.Equal("usb:0:ANY", alreadyAdded.Id);
                Assert.False(alreadyAdded.IsAvailable);
            },
            available =>
            {
                Assert.Equal("usb:1:ANY", available.Id);
                Assert.True(available.IsAvailable);
            });
        Assert.True(startingStatus.ShowPlaceholder);
        Assert.Equal("USB Camera Source: stale (1500 ms since last frame)", staleStatus.StatusText);
        Assert.True(failedStatus.ShowPlaceholder);
        Assert.False(activeStatus.RestartEnabled);
        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), applied.SettingsByCameraSourceId["usb:0:ANY"]);
        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), reverted);
        Assert.Equal("Requested: 1280x720@60; running: 640x480@20", modeProjection.ModeStatusText);
        Assert.True(pendingDecision.RequiresVisionPipelineRestart);
        Assert.False(pendingDecision.ShouldRestartCameraSource);
        Assert.False(failedDecision.ShouldRestartCameraSource);
    }

    private sealed class RecordingConsumer(string cameraId, List<string> stops) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            stops.Add(cameraId);
            return ValueTask.CompletedTask;
        }
    }
}
