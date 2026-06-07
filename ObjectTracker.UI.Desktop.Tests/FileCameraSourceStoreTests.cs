using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class FileCameraSourceStoreTests
{
    [Fact]
    public void SaveThenLoad_PreservesOneVideoPathAndPerSourceLoopBehavior()
    {
        var store = new FileCameraSourceStore(BuildTempFilePath());

        store.Save(new[]
        {
            new MainWindow.FileCameraSource("source-bridge", "Bridge Camera", "/videos/bridge.mp4", LoopVideo: true),
            new MainWindow.FileCameraSource("source-yard", "Yard Camera", "/videos/yard.mp4", LoopVideo: false)
        });

        var loaded = store.Load();

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, source =>
            source.CameraId == "source-bridge" &&
            source.DisplayName == "Bridge Camera" &&
            source.VideoPath == "/videos/bridge.mp4" &&
            source.LoopVideo);
        Assert.Contains(loaded, source =>
            source.CameraId == "source-yard" &&
            source.DisplayName == "Yard Camera" &&
            source.VideoPath == "/videos/yard.mp4" &&
            !source.LoopVideo);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "file-camera-sources.json");
    }
}
