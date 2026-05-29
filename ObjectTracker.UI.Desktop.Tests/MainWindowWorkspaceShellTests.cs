using ObjectTracker.UI.Desktop;
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
        var snapshot = MainWindow.BuildBottomStatusSnapshot(isVisionPipelineRunning: true, isAmbiguityActive: true);

        Assert.Equal("Vision Pipeline: running", snapshot.VisionPipeline);
        Assert.Equal("Ambiguity Alert: active", snapshot.AmbiguityAlert);
        Assert.Equal("Calibration: unknown", snapshot.Calibration);
        Assert.Equal("Pending restart: none", snapshot.PendingRestart);
    }

    [Fact]
    public void BottomStatusSnapshot_ShowsStoppedAndClearAmbiguityStates()
    {
        var snapshot = MainWindow.BuildBottomStatusSnapshot(isVisionPipelineRunning: false, isAmbiguityActive: false);

        Assert.Equal("Vision Pipeline: stopped", snapshot.VisionPipeline);
        Assert.Equal("Ambiguity Alert: clear", snapshot.AmbiguityAlert);
    }
}
