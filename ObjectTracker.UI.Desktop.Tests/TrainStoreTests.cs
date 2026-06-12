using System;
using System.IO;
using System.Linq;
using ObjectTracker.Core.Domain;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class TrainStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultConfiguredTrains()
    {
        var store = new TrainStore(BuildTempFilePath());

        var trains = store.Load();

        Assert.Contains(trains, train => train.Name == "Red Train" && train.PlcId == "RED");
        Assert.Contains(trains, train => train.Name == "Green Train" && train.PlcId == "GREEN");
        Assert.Contains(trains, train => train.Name == "Blue Train" && train.PlcId == "BLUE");
        Assert.Contains(trains, train => train.Name == "White Train" && train.PlcId == "WHITE");
    }

    [Fact]
    public void SaveThenLoad_PreservesTrainIdentityAndCalibration()
    {
        var path = BuildTempFilePath();
        var store = new TrainStore(path);
        var trainId = Guid.NewGuid();

        store.Save(new[]
        {
            new ConfiguredTrain(
                trainId,
                "Cargo Red",
                "PLC-17",
                0xFF8A0000,
                0xFFFF6060,
                new ColorCalibrationProfile("Cargo Red", 170, 10, 120, 255, 70, 255))
        });

        var loaded = store.Load().Single();

        Assert.Equal(trainId, loaded.Id);
        Assert.Equal("Cargo Red", loaded.Name);
        Assert.Equal("PLC-17", loaded.PlcId);
        Assert.Equal(0xFF8A0000u, loaded.MinColor);
        Assert.Equal(0xFFFF6060u, loaded.MaxColor);
        Assert.Equal(170, loaded.Calibration.HueLower);
        Assert.Equal(10, loaded.Calibration.HueUpper);
        Assert.Equal(120, loaded.Calibration.SaturationLower);
        Assert.Equal(255, loaded.Calibration.SaturationUpper);
        Assert.Equal(70, loaded.Calibration.ValueLower);
        Assert.Equal(255, loaded.Calibration.ValueUpper);
    }

    private static string BuildTempFilePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ObjectTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "trains.json");
    }
}
