using System.Collections.Generic;
using System.Linq;
using ObjectTracker.UI.Desktop;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraPipelineFilteringTests
{
    [Fact]
    public void GetIncludedCameraProfiles_ReturnsOnlyIncludedCameras()
    {
        var cam1 = MainWindow.CameraProfile.CreateVideo("cam-1", "Included Video", new List<string> { "/path/video1.mp4" });
        var cam2 = MainWindow.CameraProfile.CreateVideo("cam-2", "Excluded Video", new List<string> { "/path/video2.mp4" });
        cam2 = cam2 with { IsIncludedInVisionPipeline = false };
        var cam3 = MainWindow.CameraProfile.CreateVideo("cam-3", "Also Included", new List<string> { "/path/video3.mp4" });

        var cameras = new List<MainWindow.CameraProfile> { cam1, cam2, cam3 };

        var included = MainWindow.GetIncludedCameraProfiles(cameras);

        Assert.Equal(2, included.Count);
        Assert.Equal(new[] { "cam-1", "cam-3" }, included.Select(c => c.Id).OrderBy(id => id));
    }

    [Fact]
    public void GetIncludedCameraProfiles_ReturnsEmptyWhenNoneIncluded()
    {
        var cam1 = MainWindow.CameraProfile.CreateVideo("cam-1", "Excluded", new List<string> { "/path/video1.mp4" });
        cam1 = cam1 with { IsIncludedInVisionPipeline = false };

        var cameras = new List<MainWindow.CameraProfile> { cam1 };

        var included = MainWindow.GetIncludedCameraProfiles(cameras);

        Assert.Empty(included);
    }

    [Fact]
    public void GetIncludedCameraProfiles_ReturnsAllWhenAllIncluded()
    {
        var cameras = new List<MainWindow.CameraProfile>
        {
            MainWindow.CameraProfile.CreateVideo("cam-1", "Video A", new List<string> { "/path/video1.mp4" }),
            MainWindow.CameraProfile.CreateVideo("cam-2", "Video B", new List<string> { "/path/video2.mp4" }),
        };

        var included = MainWindow.GetIncludedCameraProfiles(cameras);

        Assert.Equal(2, included.Count);
        Assert.Equal(new[] { "cam-1", "cam-2" }, included.Select(c => c.Id).OrderBy(id => id));
    }

}
