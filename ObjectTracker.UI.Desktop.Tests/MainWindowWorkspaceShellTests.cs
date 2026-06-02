using ObjectTracker.UI.Desktop;
using Avalonia.Controls;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class MainWindowWorkspaceShellTests
{
    [Fact]
    public void BuildWorkspaceVisibility_Camera_ShowsOnlyCameraWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Camera);

        Assert.True(visibility.CameraVisible);
        Assert.False(visibility.LayersVisible);
        Assert.False(visibility.SettingsVisible);
    }

    [Fact]
    public void BuildWorkspaceVisibility_Layers_ShowsOnlyLayersWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Layers);

        Assert.False(visibility.CameraVisible);
        Assert.True(visibility.LayersVisible);
        Assert.False(visibility.SettingsVisible);
    }

    [Fact]
    public void BuildWorkspaceVisibility_Settings_ShowsOnlySettingsWorkspace()
    {
        var visibility = MainWindow.BuildWorkspaceVisibility(MainWindow.Workspace.Settings);

        Assert.False(visibility.CameraVisible);
        Assert.False(visibility.LayersVisible);
        Assert.True(visibility.SettingsVisible);
    }

    [Fact]
    public void WorkspaceNavigation_IsAllowed_DuringAmbiguityAlert()
    {
        Assert.True(MainWindow.IsWorkspaceNavigationAllowedDuringAmbiguity());
    }

    [Fact]
    public void RuntimeLog_IsVisible_OnlyInCameraWorkspace()
    {
        Assert.True(MainWindow.IsRuntimeLogVisibleForWorkspace(MainWindow.Workspace.Camera));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(MainWindow.Workspace.Layers));
        Assert.False(MainWindow.IsRuntimeLogVisibleForWorkspace(MainWindow.Workspace.Settings));
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsRunningAndActiveAmbiguityStates()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            isAmbiguityActive: true,
            hasPendingVisionPipelineRestart: false);

        Assert.Equal("Vision Pipeline: running", snapshot.VisionPipeline);
        Assert.Equal("Ambiguity Alert: active", snapshot.AmbiguityAlert);
        Assert.Equal("Calibration: unknown", snapshot.Calibration);
        Assert.Equal("Pending restart: none", snapshot.PendingRestart);
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsStoppedAndClearAmbiguityStates()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: false,
            isAmbiguityActive: false,
            hasPendingVisionPipelineRestart: false);

        Assert.Equal("Vision Pipeline: stopped", snapshot.VisionPipeline);
        Assert.Equal("Ambiguity Alert: clear", snapshot.AmbiguityAlert);
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsPendingRestartWhenRequired()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(
            isVisionPipelineRunning: true,
            isAmbiguityActive: false,
            hasPendingVisionPipelineRestart: true);

        Assert.Equal("Pending restart: required", snapshot.PendingRestart);
    }

    [Fact]
    public void VisionPipelineMenuState_WhenRunning_DisablesStartAndEnablesStop()
    {
        var state = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: true);

        Assert.False(state.StartEnabled);
        Assert.True(state.StopEnabled);
    }

    [Fact]
    public void VisionPipelineMenuState_WhenStopped_EnablesStartAndDisablesStop()
    {
        var state = MainWindow.BuildVisionPipelineMenuState(isVisionPipelineRunning: false);

        Assert.True(state.StartEnabled);
        Assert.False(state.StopEnabled);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenPinnedAndOpen_ClaimsLayoutSpace()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: true, isPinned: true);

        Assert.True(state.IsOpen);
        Assert.True(state.IsPinned);
        Assert.Equal(SplitViewDisplayMode.Inline, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(0, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenOverlayAndOpen_DoesNotClaimLayoutSpace()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: true, isPinned: false);

        Assert.True(state.IsOpen);
        Assert.False(state.IsPinned);
        Assert.Equal(SplitViewDisplayMode.CompactOverlay, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(48, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenPinned_IgnoresClosedStateAndStaysOpen()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: false, isPinned: true);

        Assert.True(state.IsOpen);
        Assert.Equal(SplitViewDisplayMode.Inline, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(0, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraPanelLayoutState_WhenClosedAndUnpinned_UsesOverlayWithoutCompactPane()
    {
        var state = MainWindow.BuildCameraPanelLayoutState(isOpen: false, isPinned: false);

        Assert.False(state.IsOpen);
        Assert.Equal(SplitViewDisplayMode.CompactOverlay, state.DisplayMode);
        Assert.Equal(340, state.CameraPanelWidth);
        Assert.Equal(48, state.CompactPaneWidth);
    }

    [Fact]
    public void CameraListSelectionMode_IsSingle()
    {
        Assert.Equal(SelectionMode.Single, MainWindow.GetCameraListSelectionMode());
    }

    [Fact]
    public void CameraDestructiveActionsState_WhenVisionPipelineRunning_DisablesClearAndDelete()
    {
        var state = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: true,
            isAmbiguityActive: false,
            hasSelectedCamera: true,
            cameraCount: 2);

        Assert.False(state.DeleteSelectedEnabled);
        Assert.False(state.ClearAllEnabled);
        Assert.True(state.DeleteSelectedRequiresConfirmation);
        Assert.True(state.ClearAllRequiresConfirmation);
    }

    [Fact]
    public void CameraDestructiveActionsState_WhenStoppedAndSelectionExists_EnablesDeleteAndClear()
    {
        var state = MainWindow.BuildCameraDestructiveActionsState(
            isVisionPipelineRunning: false,
            isAmbiguityActive: false,
            hasSelectedCamera: true,
            cameraCount: 1);

        Assert.True(state.DeleteSelectedEnabled);
        Assert.True(state.ClearAllEnabled);
    }

    [Fact]
    public void DeleteCameraConfirmationMessage_UsesSelectedCameraName()
    {
        var message = MainWindow.BuildDeleteCameraConfirmationMessage("Camera A");

        Assert.Equal("Delete camera 'Camera A' from this Session?", message);
    }

    [Fact]
    public void ClearCamerasConfirmationMessage_UsesCameraCountAndWarning()
    {
        var message = MainWindow.BuildClearCamerasConfirmationMessage(3);

        Assert.Equal("Clear all 3 cameras from this Session? This cannot be undone.", message);
    }
}
