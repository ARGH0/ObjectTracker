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
        var layersWorkspace = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Regions);
        var settingsWorkspace = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Settings);
        var stoppedMenu = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: false);
        var runningMenu = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: true);
        var pendingStatus = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            hasPendingVisionPipelineRestart: true);

        Assert.True(cameraWorkspace.CameraVisible);
        Assert.True(layersWorkspace.RegionsVisible);
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
        var viewState = MainWindow.BuildCameraTileViewState(projection, Array.Empty<bool>());
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
            MainWindow.FeedKind.RawFeed,
            MainWindow.FeedKind.DebugView
        }, viewState.FeedKinds.ToArray());
        Assert.False(runningDestructiveState.DeleteSelectedEnabled);
        Assert.False(runningDestructiveState.ClearAllEnabled);
        Assert.True(stoppedDestructiveState.DeleteSelectedEnabled);
        Assert.True(stoppedDestructiveState.ClearAllEnabled);
        Assert.True(stoppedDestructiveState.DeleteSelectedRequiresConfirmation);
        Assert.True(stoppedDestructiveState.ClearAllRequiresConfirmation);
    }

}
