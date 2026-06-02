using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.UI.Desktop;
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
}
