using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Domain.Enums;
using ObjectTracker.Core.Ports;
using ObjectTracker.UI.Desktop;
using ObjectTracker.UI.Desktop.Enums;
using ObjectTracker.Vision;
using ObjectTracker.Vision.Source;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowEndToEndRegressionTests
{
    /// <summary>
    /// <description>Feature: MainWindow workspace visibility and runtime controls preserve navigation state and validity across all workspaces.
    /// 
    ///   Scenario: Building workspace visibility for Camera, Layers, and Settings workspaces with stopped Vision Pipeline menu and pending restart status should produce correct booleans and text.
    ///     Given BuildWorkspaceVisibility is called for Workspace.Camera, Workspace.Layers, and Workspace.Settings,
    ///      And BuildVisionPipelineMenuState is called for both running=false and running=true,
    ///      And BuildBottomStatusSnapshot is called with isAmbiguityActive=false and hasPendingVisionPipelineRestart=true,
    ///     Then all three workspace visibility booleans should be true (CameraVisible, LayersVisible, SettingsVisible),
    ///      And stoppedMenu.StartEnabled should be true and StopEnabled should be false,
    ///      And runningMenu.StartEnabled should be false and StopEnabled should be true,
    ///      And pendingStatus.PendingRestart should be "Pending restart: required",
    ///      And IsWorkspaceNavigationAllowedDuringAmbiguity() should return true.</description>
    /// </summary>
    [Fact]
    public void WorkspaceAndRuntimeControls_EndToEnd_PreserveNavigationAndRuntimeValidity()
    {
        var cameraWorkspace = MainWindow.BuildWorkspaceVisibility(Workspace.Camera);
        var layersWorkspace = MainWindow.BuildWorkspaceVisibility(Workspace.Layers);
        var settingsWorkspace = MainWindow.BuildWorkspaceVisibility(Workspace.Settings);
        var stoppedMenu = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: false);
        var runningMenu = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: true);
        var pendingStatus = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            isAmbiguityActive: false,
            hasPendingVisionPipelineRestart: true);

        Assert.True(cameraWorkspace.CameraVisible);
        Assert.True(layersWorkspace.LayersVisible);
        Assert.True(settingsWorkspace.SettingsVisible);
        Assert.True(stoppedMenu.StartEnabled);
        Assert.False(stoppedMenu.StopEnabled);
        Assert.False(runningMenu.StartEnabled);
        Assert.True(runningMenu.StopEnabled);
        Assert.Equal("Pending restart: required", pendingStatus.PendingRestart);
        Assert.True(MainWindow.IsWorkspaceNavigationAllowedDuringAmbiguity());
    }

    /// <summary>
    /// <description>Feature: MainWindow camera workspace controls project correct render modes and guard destructive actions based on pipeline state.
    /// 
    ///   Scenario: Building camera grid projection with raw, debug, and hidden cameras, then computing view state and destructive action states should produce correct results for both running and stopped conditions.
    ///     Given BuildCameraGridProjection is called with "cam-raw" (visible, excluded), "cam-debug" (visible, included, debug enabled), and "cam-hidden" (hidden),
    ///      And BuildCameraTileViewState is called on that projection,
    ///      And BuildCameraDestructiveActionsState is called for both running=true and running=false with selected camera and counts 3 and 1 respectively,
    ///     Then viewState.CameraIds should be ["cam-raw", "cam-debug"],
    ///      And viewState.RenderModes should be [RawFeed, DebugView],
    ///      And runningDestructiveState.DeleteSelectedEnabled should be false and ClearAllEnabled should be false,
    ///      And stoppedDestructiveState.DeleteSelectedEnabled should be true and ClearAllEnabled should be true,
    ///      And stoppedDestructiveState.DeleteSelectedRequiresConfirmation should be true and ClearAllRequiresConfirmation should be true.</description>
    /// </summary>
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
            isAmbiguityActive: false,
            hasSelectedCamera: true,
            cameraCount: 3);
        var stoppedDestructiveState = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: false,
            isAmbiguityActive: false,
            hasSelectedCamera: true,
            cameraCount: 1);

        Assert.Equal(new[] { "cam-raw", "cam-debug" }, viewState.CameraIds.ToArray());
        Assert.Equal(new[]
        {
            CameraRenderMode.RawFeed,
            CameraRenderMode.DebugView
        }, viewState.RenderModes.ToArray());
        Assert.False(runningDestructiveState.DeleteSelectedEnabled);
        Assert.False(runningDestructiveState.ClearAllEnabled);
        Assert.True(stoppedDestructiveState.DeleteSelectedEnabled);
        Assert.True(stoppedDestructiveState.ClearAllEnabled);
        Assert.True(stoppedDestructiveState.DeleteSelectedRequiresConfirmation);
        Assert.True(stoppedDestructiveState.ClearAllRequiresConfirmation);
    }

    /// <summary>
    /// <description>Feature: MainWindow file-based camera source projection preserves video path and per-source loop behavior.
    /// 
    ///   Scenario: Building a file camera source projection with two sources (one looping, one not) should produce correct projected properties for each source.
    ///     Given BuildFileCameraSourceProjection is called with "source-bridge" (/videos/bridge.mp4, LoopVideo=true) and "source-yard" (/videos/yard.mp4, LoopVideo=false),
    ///     Then sources[0] should have CameraId "source-bridge", DisplayName "Bridge Camera", VideoPath "/videos/bridge.mp4", and LoopVideo true,
    ///      And sources[1] should have CameraId "source-yard", DisplayName "Yard Camera", VideoPath "/videos/yard.mp4", and LoopVideo false.</description>
    /// </summary>
    [Fact]
    public void FileBasedCameraSources_EndToEnd_ProjectOneVideoPathAndPerSourceLoopBehavior()
    {
        var projection = MainWindow.BuildFileCameraSourceProjection(new[]
        {
            new MainWindow.FileCameraSource("source-bridge", "Bridge Camera", "/videos/bridge.mp4", LoopVideo: true),
            new MainWindow.FileCameraSource("source-yard", "Yard Camera", "/videos/yard.mp4", LoopVideo: false)
        });

        Assert.Collection(
            projection.Sources,
            bridge =>
            {
                Assert.Equal("source-bridge", bridge.CameraId);
                Assert.Equal("Bridge Camera", bridge.DisplayName);
                Assert.Equal("/videos/bridge.mp4", bridge.VideoPath);
                Assert.True(bridge.LoopVideo);
            },
            yard =>
            {
                Assert.Equal("source-yard", yard.CameraId);
                Assert.Equal("Yard Camera", yard.DisplayName);
                Assert.Equal("/videos/yard.mp4", yard.VideoPath);
                Assert.False(yard.LoopVideo);
            });
    }

    /// <summary>
    /// <description>Feature: MainWindow layers and settings end-to-end blocks in-use layer type deletion and signals pending restart.
    /// 
    ///   Scenario: Building layer type usage projection, delete state, settings save impact, and bottom status snapshot should correctly block deletion of used layer types and signal pending Vision Pipeline restart for grid changes.
    ///     Given BuildLayerTypeUsageProjection is called for NO-VISION with a bridge no-vision layer on zone-bridge bound to source-bridge,
    ///      And BuildLayerTypeDeleteState("NO-VISION", usage) is called,
    ///      And BuildSettingsSaveImpact is called with grid change from 32x18 to 40x18 while running=true,
    ///      And BuildBottomStatusSnapshot is called with hasPendingVisionPipelineRestart=saveImpact.RequiresVisionPipelineRestart,
    ///     Then deleteState.CanDelete should be false,
    ///      And deleteState.Message should contain "Bridge Camera" and "Bridge No-Vision",
    ///      And saveImpact.RequiresVisionPipelineRestart should be true and HasPendingVisionPipelineRestart should be true,
    ///      And saveImpact.SettingsStatusText should be "Settings: saved, pending Vision Pipeline restart",
    ///      And bottomStatus.PendingRestart should be "Pending restart: required".</description>
    /// </summary>
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
            isAmbiguityActive: false,
            hasPendingVisionPipelineRestart: saveImpact.HasPendingVisionPipelineRestart);

        Assert.False(deleteState.CanDelete);
        Assert.Contains("Bridge Camera", deleteState.Message, StringComparison.Ordinal);
        Assert.Contains("Bridge No-Vision", deleteState.Message, StringComparison.Ordinal);
        Assert.True(saveImpact.RequiresVisionPipelineRestart);
        Assert.True(saveImpact.HasPendingVisionPipelineRestart);
        Assert.Equal("Settings: saved, pending Vision Pipeline restart", saveImpact.SettingsStatusText);
        Assert.Equal("Pending restart: required", bottomStatus.PendingRestart);
    }

    /// <summary>
    /// <description>Feature: MainWindow USB camera source flow end-to-end preserves feeds and projects operator decisions across discovery, status projection, settings service, and capture mode projection.
    /// 
    ///   Scenario: Starting a CameraTileFeedCoordinator with file and USB sources, adding another USB source, discovering available cameras, projecting various runtime statuses (starting, stale, failed, active), applying and reverting settings, and building mode/apply decisions should produce consistent results across all projections.
    ///     Given a CameraTileFeedCoordinator is started with "file-a:VideoFile" and "usb:0:ANY:Usb", then a third source "usb:1:ANY:Usb" is added,
    ///      And UsbCameraDiscoveryService discovers usb:0:ANY (already added) and usb:1:ANY (available at 1280x720),
    ///      And BuildUsbStatus is called for starting, stale, failed, and active states,
    ///      And UsbCaptureSettingsService applies a draft from 640x480@30 to 1280x720@60 then reverts it,
    ///      And Build mode projection with requested 1280x720@60 and running 640x480@20,
    ///      And BuildApplyDecision is called for a running source and a failed source,
    ///     Then starts should be ["file-a:VideoFile", "usb:0:ANY:Usb", "usb:1:ANY:Usb"] with no stops,
    ///      And probed should contain [1],
    ///      And discovered[0] is usb:0:ANY (not available), discovered[1] is usb:1:ANY (available),
    ///      And startingStatus.ShowPlaceholder should be true, staleStatus.StatusText should show "stale (1500 ms since last frame)", failedStatus.ShowPlaceholder should be true and RaisesAmbiguityAlert false, activeStatus.RestartEnabled should be false,
    ///      And applied settings should be 1280x720@60 for usb:0:ANY, reverted should be 1280x720@60 (the draft before revert),
    ///      And modeProjection.ModeStatusText should be "Requested: 1280x720@60; running: 640x480@20",
    ///      And pendingDecision.RequiresVisionPipelineRestart should be true and ShouldRestartCameraSource false, failedDecision.ShouldRestartCameraSource should be false.</description>
    /// </summary>
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
        Assert.False(failedStatus.RaisesAmbiguityAlert);
        Assert.False(activeStatus.RestartEnabled);
        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), applied.SettingsByCameraSourceId["usb:0:ANY"]);
        Assert.Equal(new UsbCaptureSettingsRequest(1280, 720, 60), reverted);
        Assert.Equal("Requested: 1280x720@60; running: 640x480@20", modeProjection.ModeStatusText);
        Assert.True(pendingDecision.RequiresVisionPipelineRestart);
        Assert.False(pendingDecision.ShouldRestartCameraSource);
        Assert.False(failedDecision.ShouldRestartCameraSource);
    }

    /// <summary>
    /// <description>Feature: MainWindow Vision Pipeline snapshot flow end-to-end routes frames, statuses, train tracking state, and runtime changes through the full pipeline.
    /// 
    ///   Scenario: Starting with file camera source projection, building camera grid projections for stopped and running states, rendering pipeline snapshots (annotated then debug), changing vision pipeline inclusion to excluded, computing stale lane status, stopping the Vision Pipeline, and verifying all frame routing, train state, ambiguity alerts, and display overlays should produce consistent results across the full flow.
    ///     Given BuildFileCameraSourceProjection for "file-bridge" with LoopVideo=true,
    ///      And BuildCameraGridProjection and BuildCameraTileFrameRouting are called for stopped (isVisionPipelineRunning=false) and running states with 4 cameras (visible included, visible excluded, hidden included, hidden excluded),
    ///      And CameraTileDisplayProjection.Build is called for a starting lane placeholder,
    ///      And PipelineController starts with ControlledFrameSources for "file-bridge" and "handoff-hidden", Vision Pipeline inclusion set true for both, FPS set to 12,
    ///      And snapshots are waited for and an Ambiguity Alert status appears,
    ///      And CameraTilePipelineSnapshotRenderer renders the annotated frame for "file-bridge" using running routing,
    ///      And debug view is enabled on "file-bridge", a new frame is enqueued, and a debug snapshot is rendered with debug routing,
    ///      And Vision Pipeline inclusion is set to false for "file-bridge", waiting for the source to stop,
    ///      And stale snapshot display and status panel are built from running camera source status and stale lane status,
    ///      And StopAsync is called on the controller, then stopped routing is rebuilt,
    ///     Then fileSourceProjection should have one source "file-bridge" with LoopVideo=true,
    ///      And all stopped routes should be RawCameraSourceFeed,
    ///      And running projection tiles should contain only "file-bridge" and "usb:0:ANY",
    ///      And running routing should have file-bridge routed to PipelineSnapshotAnnotatedFrame and usb:0:ANY to RawCameraSourceFeed,
    ///      And first snapshot placeholder FrameDisplay should be Placeholder,
    ///      And snapshots should contain one for "handoff-hidden",
    ///      And single annotated frame should exist with CameraId "file-bridge",
    ///      And debug frames should contain a "train-tracking" frame at 12 FPS with LocalTrainId "train-001",
    ///      And output statuses should contain an Ambiguity Alert message,
    ///      And excluded routing for file-bridge should be RawCameraSourceFeed,
    ///      And staleSnapshotDisplay.ShowVisionPipelineLaneStatusOverlay should be true and ShowCameraSourceStatusOverlay false,
    ///      And statusPanel CameraSourceStatusText should be "running" and VisionPipelineLaneStatusText should be "stale",
    ///      And stopped again routing should all be RawCameraSourceFeed,
    ///      And fileSource.StopCount should be 1 and handoffSource.StopCount should be 1.</description>
    /// </summary>
    [Fact]
    public async Task VisionPipelineSnapshotFlow_EndToEnd_RoutesFramesStatusesTrainTrackingAndRuntimeChanges()
    {
        var fileSourceProjection = MainWindow.BuildFileCameraSourceProjection(new[]
        {
            new MainWindow.FileCameraSource("file-bridge", "Bridge File Camera Source", "/videos/bridge.mp4", LoopVideo: true)
        });
        var cameras = new[]
        {
            new MainWindow.CameraWorkspaceCamera("file-bridge", "Bridge", IsVisible: true, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("usb:0:ANY", "USB Yard", IsVisible: true, IsIncludedInVisionPipeline: false, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("handoff-hidden", "Hidden Handoff", IsVisible: false, IsIncludedInVisionPipeline: true, DebugViewEnabled: false),
            new MainWindow.CameraWorkspaceCamera("spare-hidden", "Spare Hidden", IsVisible: false, IsIncludedInVisionPipeline: false, DebugViewEnabled: false)
        };
        var stoppedRouting = MainWindow.BuildCameraTileFrameRouting(MainWindow.BuildCameraGridProjection(cameras, isVisionPipelineRunning: false));
        var runningProjection = MainWindow.BuildCameraGridProjection(cameras, isVisionPipelineRunning: true);
        var runningRouting = MainWindow.BuildCameraTileFrameRouting(runningProjection);
        var firstSnapshotPlaceholder = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: false,
            frameSource: CameraTileFrameSource.PipelineSnapshotAnnotatedFrame,
            visionPipelineLaneStatus: new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Starting, LatestSnapshotVersion: null, FailureMessage: null));

        var fileSource = new ControlledFrameSource("file-bridge", [Frame("file-bridge", 1000)]);
        var handoffSource = new ControlledFrameSource("handoff-hidden", [Frame("handoff-hidden", 6000)]);
        var output = new RecordingOutputPort();
        await using var controller = new PipelineController(
            new MultiFrameSourceFactory(fileSource, new ControlledFrameSource("usb:0:ANY", []), handoffSource),
            new SourceAwareVisualObservationPipeline(new Dictionary<string, Detection[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["file-bridge"] = [Detection("file-bridge", 1000, 10, 10, "Red")],
                ["handoff-hidden"] = [Detection("handoff-hidden", 6000, 20, 12, "Red")]
            }),
            new DuplicateLocalTrainIdTracker(),
            [output],
            new IncrementingClock());
        controller.SetVisionPipelineInclusion("file-bridge", included: true);
        controller.SetVisionPipelineInclusion("handoff-hidden", included: true);
        controller.SetTargetFramesPerSecond(12);

        await controller.StartAsync(CancellationToken.None);
        var snapshots = await output.WaitForSnapshotsAsync(2);
        await WaitUntilAsync(() => output.Statuses.Any(status => status.Contains("Ambiguity Alert", StringComparison.OrdinalIgnoreCase)));

        var renderer = new CameraTilePipelineSnapshotRenderer();
        var annotatedFrames = new List<CameraTileRawFrameSnapshot>();
        var visibleSnapshot = snapshots.First(snapshot => snapshot.CameraSourceId == "file-bridge");
        await renderer.RenderSnapshotAsync(visibleSnapshot, runningRouting, frame =>
        {
            annotatedFrames.Add(frame);
            return Task.CompletedTask;
        }, CancellationToken.None);

        controller.SetDebugViewEnabled("file-bridge", enabled: true);
        fileSource.Enqueue(Frame("file-bridge", 7000));
        var debugSnapshot = (await output.WaitForSnapshotsAsync(3)).Last(snapshot => snapshot.CameraSourceId == "file-bridge");
        var debugRouting = MainWindow.BuildCameraTileFrameRouting(MainWindow.BuildCameraGridProjection(new[]
        {
            cameras[0] with { DebugViewEnabled = true },
            cameras[1],
            cameras[2],
            cameras[3]
        }, isVisionPipelineRunning: true));
        var debugFrames = new List<CameraTileDebugFrameSnapshot>();
        await renderer.RenderSnapshotAsync(debugSnapshot, debugRouting, _ => Task.CompletedTask, frame =>
        {
            debugFrames.Add(frame);
            return Task.CompletedTask;
        }, CancellationToken.None);

        controller.SetVisionPipelineInclusion("file-bridge", included: false);
        await WaitUntilAsync(() => fileSource.StopCount == 1);
        var excludedRouting = MainWindow.BuildCameraTileFrameRouting(MainWindow.BuildCameraGridProjection(new[]
        {
            cameras[0] with { IsIncludedInVisionPipeline = false, DebugViewEnabled = false },
            cameras[1],
            cameras[2],
            cameras[3]
        }, isVisionPipelineRunning: true));
        var staleSnapshotDisplay = CameraTileDisplayProjection.Build(
            hasCurrentFrame: false,
            hasLastFrame: true,
            frameSource: CameraTileFrameSource.PipelineSnapshotAnnotatedFrame,
            cameraSourceStatus: new CameraSourceStatus(CameraSourceStatusState.Running, LatestFrameVersion: 4, FailureMessage: null),
            visionPipelineLaneStatus: new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion: debugSnapshot.Timing.TimestampUtcMs, FailureMessage: null));
        var statusPanel = CameraPanelStatusProjection.Build(
            new CameraSourceStatus(CameraSourceStatusState.Running, LatestFrameVersion: 4, FailureMessage: null),
            new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion: debugSnapshot.Timing.TimestampUtcMs, FailureMessage: null));

        await controller.StopAsync(CancellationToken.None);
        var stoppedAgainRouting = MainWindow.BuildCameraTileFrameRouting(MainWindow.BuildCameraGridProjection(cameras, isVisionPipelineRunning: false));

        Assert.Equal("file-bridge", Assert.Single(fileSourceProjection.Sources).CameraId);
        Assert.True(fileSourceProjection.Sources[0].LoopVideo);
        Assert.All(stoppedRouting.Routes, route => Assert.Equal(CameraTileFrameSource.RawCameraSourceFeed, route.FrameSource));
        Assert.Equal(new[] { "file-bridge", "usb:0:ANY" }, runningProjection.Tiles.Select(tile => tile.CameraId).ToArray());
        Assert.Contains(runningRouting.Routes, route => route.CameraId == "file-bridge" && route.FrameSource == CameraTileFrameSource.PipelineSnapshotAnnotatedFrame);
        Assert.Contains(runningRouting.Routes, route => route.CameraId == "usb:0:ANY" && route.FrameSource == CameraTileFrameSource.RawCameraSourceFeed);
        Assert.Equal(CameraTileFrameDisplay.Placeholder, firstSnapshotPlaceholder.FrameDisplay);
        Assert.Contains(snapshots, snapshot => snapshot.CameraSourceId == "handoff-hidden");
        Assert.Single(annotatedFrames);
        Assert.Equal("file-bridge", annotatedFrames[0].CameraId);
        Assert.Contains(debugFrames, frame => frame.Name == "train-tracking");
        Assert.Equal(12, debugSnapshot.Timing.TargetFramesPerSecond);
        Assert.Equal("train-001", Assert.Single(visibleSnapshot.TrainStates).LocalTrainId);
        Assert.Equal("train-001", Assert.Single(snapshots.First(snapshot => snapshot.CameraSourceId == "handoff-hidden").TrainStates).LocalTrainId);
        Assert.Contains(output.Statuses, status => status.Contains("Ambiguity Alert", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(excludedRouting.Routes, route => route.CameraId == "file-bridge" && route.FrameSource == CameraTileFrameSource.RawCameraSourceFeed);
        Assert.True(staleSnapshotDisplay.ShowVisionPipelineLaneStatusOverlay);
        Assert.False(staleSnapshotDisplay.ShowCameraSourceStatusOverlay);
        Assert.Equal("Camera Source Status: running", statusPanel.CameraSourceStatusText);
        Assert.Equal("Vision Pipeline Lane Status: stale", statusPanel.VisionPipelineLaneStatusText);
        Assert.All(stoppedAgainRouting.Routes, route => Assert.Equal(CameraTileFrameSource.RawCameraSourceFeed, route.FrameSource));
        Assert.Equal(1, fileSource.StopCount);
        Assert.Equal(1, handoffSource.StopCount);
    }

    private sealed class RecordingConsumer(string cameraId, List<string> stops) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            stops.Add(cameraId);
            return ValueTask.CompletedTask;
        }
    }

    private static FramePacket Frame(string sourceId, long timestamp) => new(sourceId, timestamp, 2, 2, [1, 2, 3]);

    private static Detection Detection(string sourceId, long timestamp, float x, float y, string trainColor) => new(
        $"{sourceId}-{trainColor}-{timestamp}",
        x,
        y,
        x - 2,
        y - 2,
        4,
        4,
        0.9f,
        trainColor,
        sourceId,
        timestamp);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Expected condition to become true.");
    }

    private sealed class MultiFrameSourceFactory(params ControlledFrameSource[] sources) : IFrameSourceFactory
    {
        private readonly Dictionary<string, ControlledFrameSource> sourcesById = sources.ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<FrameSourceInfo> GetAvailableSources() => sources.Select(source => new FrameSourceInfo(source.Id, source.DisplayName)).ToList();

        public IFrameSource Create(string sourceId) => sourcesById[sourceId];
    }

    private sealed class ControlledFrameSource(string id, IReadOnlyList<FramePacket> frames) : IFrameSource
    {
        private readonly Lock sync = new();
        private readonly List<FramePacket> frames = frames.ToList();
        private int nextFrameIndex;

        public string Id => id;

        public string DisplayName => id;

        public string Diagnostics => "test source";

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task<FramePacket?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            lock (sync)
            {
                if (nextFrameIndex >= frames.Count)
                {
                    return Task.FromResult<FramePacket?>(null);
                }

                return Task.FromResult<FramePacket?>(frames[nextFrameIndex++]);
            }
        }

        public void Enqueue(FramePacket frame)
        {
            lock (sync)
            {
                frames.Add(frame);
            }
        }

        public string? ConsumeDiagnosticEvent() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SourceAwareDetectorManager(IReadOnlyDictionary<string, Detection[]> detectionsBySourceId) : IDetectorManager
    {
        public IReadOnlyList<string> AvailableColorFilters => [];

        public IReadOnlyList<string> EnabledColorFilters => [];

        public IReadOnlyList<ColorCalibrationProfile> ColorCalibrations => [];

        public void SetEnabledColorFilters(IEnumerable<string> colors) { }

        public void SetColorCalibrations(IEnumerable<ColorCalibrationProfile> calibrations) { }

        public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Detection>>(
                detectionsBySourceId.TryGetValue(frame.SourceId, out var detections) ? detections : []);
        }
    }

    private sealed class SourceAwareVisualObservationPipeline(IReadOnlyDictionary<string, Detection[]> detectionsBySourceId) : IVisualObservationPipeline
    {
        public Task<VisualObservationResult> ObserveAsync(
            FramePacket sourceFrame,
            VisualObservationSettings settings,
            CancellationToken cancellationToken)
        {
            var trainObservations = detectionsBySourceId.TryGetValue(sourceFrame.SourceId, out var detections)
                ? detections.Select(ToTrainObservation).ToList()
                : [];
            return Task.FromResult(new VisualObservationResult([], trainObservations, []));
        }

        private static TrainObservation ToTrainObservation(Detection detection) => new(
            detection.SourceId,
            detection.TimestampUtcMs,
            detection.Kind,
            detection.X,
            detection.Y,
            detection.BoxX,
            detection.BoxY,
            detection.BoxWidth,
            detection.BoxHeight,
            detection.Confidence);
    }

    private sealed class DuplicateLocalTrainIdTracker : ITracker
    {
        public IReadOnlyList<TrainState> Update(IReadOnlyList<Detection> detections, long frameTimestampUtcMs)
        {
            return detections
                .Select(detection => new TrainState(
                    "train-001",
                    detection.Kind,
                    detection.X,
                    detection.Y,
                    0,
                    0,
                    detection.Confidence,
                    TrainMotionState.Moving,
                    CollisionWarningState.None,
                    detection.SourceId,
                    detection.TimestampUtcMs))
                .ToList();
        }

        public void Reset() { }
    }

    private sealed class IncrementingClock : IClock
    {
        private long value = 1000;

        public long UtcNowMs() => Interlocked.Increment(ref value);
    }

    private sealed class RecordingOutputPort : IOutputPort
    {
        private readonly List<PipelineSnapshot> snapshots = [];
        private readonly List<string> statuses = [];
        private readonly Lock sync = new();

        public IReadOnlyList<PipelineSnapshot> Snapshots
        {
            get
            {
                lock (sync)
                {
                    return snapshots.ToList();
                }
            }
        }

        public IReadOnlyList<string> Statuses
        {
            get
            {
                lock (sync)
                {
                    return statuses.ToList();
                }
            }
        }

        public Task PublishSnapshotAsync(PipelineSnapshot snapshot, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                snapshots.Add(snapshot);
            }

            return Task.CompletedTask;
        }

        public Task PublishStatusAsync(string status, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                statuses.Add(status);
            }

            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<PipelineSnapshot>> WaitForSnapshotsAsync(int count)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                var current = Snapshots;
                if (current.Count >= count)
                {
                    return current;
                }

                await Task.Delay(10);
            }

            throw new TimeoutException($"Expected the Vision Pipeline to publish {count} snapshots.");
        }
    }
}
