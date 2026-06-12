using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class CameraSettingsStoreTests
{
    [Fact]
    public void Load_WhenOldFileContainsColorCalibrations_IgnoresCameraOwnedCalibration()
    {
        var path = BuildTempFilePath();
        File.WriteAllText(path, """
        {
          "Items": [
            {
              "CameraId": "camera-a",
              "SampleCount": 30,
              "Threshold": 90,
              "MotionArea": 300,
              "ColorMinPixels": 44,
              "MorphKernelSize": 5,
              "ProcessMaxWidth": 800,
              "BakeSourceMode": "Samples",
              "BakeImagePath": "",
              "ColorCalibrations": [
                {
                  "Name": "RED",
                  "HueLower": 1,
                  "HueUpper": 2,
                  "SaturationLower": 3,
                  "SaturationUpper": 4,
                  "ValueLower": 5,
                  "ValueUpper": 6
                }
              ]
            }
          ]
        }
        """);

        var settings = new CameraSettingsStore(path).Load()["camera-a"];

        Assert.Equal(30, settings.SampleCount);
        Assert.Equal(90, settings.Threshold);
        Assert.Equal(300, settings.MotionArea);
        Assert.Equal(44, settings.ColorMinPixels);
        Assert.Equal(5, settings.MorphKernelSize);
        Assert.Equal(800, settings.ProcessMaxWidth);
    }

    [Fact]
    public void Save_DoesNotWriteCameraOwnedColorCalibrations()
    {
        var path = BuildTempFilePath();
        var store = new CameraSettingsStore(path);

        store.Save(new Dictionary<string, MainWindow.RuntimeProcessingSettings>
        {
            ["camera-a"] = new MainWindow.RuntimeProcessingSettings(
                SampleCount: 30,
                Threshold: 90,
                MotionArea: 300,
                ColorMinPixels: 44,
                MorphKernelSize: 5,
                ProcessMaxWidth: 800,
                BakeSourceMode: MainWindow.BakeSourceMode.Samples,
                BakeImagePath: string.Empty)
        });

        var json = File.ReadAllText(path);

        Assert.DoesNotContain("ColorCalibrations", json, StringComparison.Ordinal);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "camera-settings.json");
    }
}
