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
}
