using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraTileFeedRequestUsbTests
{
    [Fact]
    public void BuildCameraTileFeedRequests_ForVisibleUsbCameraProfile_RequestsUsbCameraFeed()
    {
        var device = new UsbCameraDevice("usb:/dev/video0", "USB Camera", "USB Camera", 0, "/dev/video0");
        var camera = MainWindow.CameraProfile.CreateUsbCamera(device);
        var projection = MainWindow.BuildCameraGridProjection(new[]
        {
            new MainWindow.CameraWorkspaceCamera(
                CameraId: camera.Id,
                DisplayName: camera.DisplayName,
                IsVisible: camera.IsVisible,
                IsIncludedInVisionPipeline: camera.IsIncludedInVisionPipeline,
                DebugViewEnabled: camera.DebugViewEnabled,
                DebugViewFrameType: camera.DebugViewFrameType,
                ShowAnnotationsEnabled: camera.ShowAnnotationsEnabled,
                ShowRegionsEnabled: camera.ShowRegionsEnabled)
        });

        var requests = MainWindow.BuildCameraTileFeedRequests(
            new[] { camera },
            projection,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { camera.Id },
            isVisionPipelineRunning: false);

        var request = Assert.Single(requests);
        Assert.Equal(camera.Id, request.CameraId);
        Assert.Equal(CameraTileFeedKind.UsbCamera, request.Kind);
    }
}
