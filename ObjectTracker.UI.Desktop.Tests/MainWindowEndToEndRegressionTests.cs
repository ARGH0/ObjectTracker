using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using ObjectTracker.UI.Desktop;
using ObjectTracker.Vision;
using ObjectTracker.Vision.Source;
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
            frameSource: MainWindow.CameraTileFrameSource.PipelineSnapshotAnnotatedFrame,
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
            frameSource: MainWindow.CameraTileFrameSource.PipelineSnapshotAnnotatedFrame,
            cameraSourceStatus: new CameraSourceStatus(CameraSourceStatusState.Running, LatestFrameVersion: 4, FailureMessage: null),
            visionPipelineLaneStatus: new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion: debugSnapshot.Timing.TimestampUtcMs, FailureMessage: null));
        var statusPanel = CameraPanelStatusProjection.Build(
            new CameraSourceStatus(CameraSourceStatusState.Running, LatestFrameVersion: 4, FailureMessage: null),
            new VisionPipelineLaneStatus(VisionPipelineLaneStatusState.Stale, LatestSnapshotVersion: debugSnapshot.Timing.TimestampUtcMs, FailureMessage: null));

        await controller.StopAsync(CancellationToken.None);
        var stoppedAgainRouting = MainWindow.BuildCameraTileFrameRouting(MainWindow.BuildCameraGridProjection(cameras, isVisionPipelineRunning: false));

        Assert.Equal("file-bridge", Assert.Single(fileSourceProjection.Sources).CameraId);
        Assert.True(fileSourceProjection.Sources[0].LoopVideo);
        Assert.All(stoppedRouting.Routes, route => Assert.Equal(MainWindow.CameraTileFrameSource.RawCameraSourceFeed, route.FrameSource));
        Assert.Equal(new[] { "file-bridge", "usb:0:ANY" }, runningProjection.Tiles.Select(tile => tile.CameraId).ToArray());
        Assert.Contains(runningRouting.Routes, route => route.CameraId == "file-bridge" && route.FrameSource == MainWindow.CameraTileFrameSource.PipelineSnapshotAnnotatedFrame);
        Assert.Contains(runningRouting.Routes, route => route.CameraId == "usb:0:ANY" && route.FrameSource == MainWindow.CameraTileFrameSource.RawCameraSourceFeed);
        Assert.Equal(CameraTileFrameDisplay.Placeholder, firstSnapshotPlaceholder.FrameDisplay);
        Assert.Contains(snapshots, snapshot => snapshot.CameraSourceId == "handoff-hidden");
        Assert.Single(annotatedFrames);
        Assert.Equal("file-bridge", annotatedFrames[0].CameraId);
        Assert.Contains(debugFrames, frame => frame.Name == "train-tracking");
        Assert.Equal(12, debugSnapshot.Timing.TargetFramesPerSecond);
        Assert.Equal("train-001", Assert.Single(visibleSnapshot.TrainStates).LocalTrainId);
        Assert.Equal("train-001", Assert.Single(snapshots.First(snapshot => snapshot.CameraSourceId == "handoff-hidden").TrainStates).LocalTrainId);
        Assert.Contains(output.Statuses, status => status.Contains("Ambiguity Alert", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(excludedRouting.Routes, route => route.CameraId == "file-bridge" && route.FrameSource == MainWindow.CameraTileFrameSource.RawCameraSourceFeed);
        Assert.True(staleSnapshotDisplay.ShowVisionPipelineLaneStatusOverlay);
        Assert.False(staleSnapshotDisplay.ShowCameraSourceStatusOverlay);
        Assert.Equal("Camera Source Status: running", statusPanel.CameraSourceStatusText);
        Assert.Equal("Vision Pipeline Lane Status: stale", statusPanel.VisionPipelineLaneStatusText);
        Assert.All(stoppedAgainRouting.Routes, route => Assert.Equal(MainWindow.CameraTileFrameSource.RawCameraSourceFeed, route.FrameSource));
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
