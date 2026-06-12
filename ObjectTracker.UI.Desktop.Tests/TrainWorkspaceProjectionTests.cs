using System;
using ObjectTracker.Core.Domain;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class TrainWorkspaceProjectionTests
{
    [Fact]
    public void BuildTrainEditorProjection_SelectedTrainShowsIdentityColorsAndAdvancedCalibration()
    {
        var trainId = Guid.NewGuid();
        var train = new ConfiguredTrain(
            trainId,
            "Cargo Red",
            "PLC-17",
            0xFF8A0000,
            0xFFFF6060,
            new ColorCalibrationProfile("Cargo Red", 170, 10, 120, 255, 70, 255));

        var projection = MainWindow.BuildTrainEditorProjection(train);

        Assert.Equal(trainId, projection.TrainId);
        Assert.Equal("Cargo Red", projection.Name);
        Assert.Equal("PLC-17", projection.PlcId);
        Assert.Equal(0xFF8A0000u, projection.MinColor);
        Assert.Equal(0xFFFF6060u, projection.MaxColor);
        Assert.Equal("170", projection.HueLower);
        Assert.Equal("10", projection.HueUpper);
        Assert.Equal("120", projection.SaturationLower);
        Assert.Equal("255", projection.SaturationUpper);
        Assert.Equal("70", projection.ValueLower);
        Assert.Equal("255", projection.ValueUpper);
    }

    [Theory]
    [InlineData("Red", 0xFF8A0000u, 0xFFFF6060u, 170, 10, 120, 255, 70, 255)]
    [InlineData("Green", 0xFF006A20u, 0xFF70FF70u, 35, 85, 80, 255, 60, 255)]
    [InlineData("Blue", 0xFF003C8Fu, 0xFF60B0FFu, 90, 130, 100, 255, 60, 255)]
    [InlineData("White", 0xFFC8C8C8u, 0xFFFFFFFFu, 0, 180, 0, 50, 190, 255)]
    public void ApplyTrainCalibrationPreset_ReturnsEditableStartingColorsAndAdvancedCalibration(
        string preset,
        uint minColor,
        uint maxColor,
        int hueLower,
        int hueUpper,
        int saturationLower,
        int saturationUpper,
        int valueLower,
        int valueUpper)
    {
        var projection = MainWindow.ApplyTrainCalibrationPreset(preset);

        Assert.Equal(minColor, projection.MinColor);
        Assert.Equal(maxColor, projection.MaxColor);
        Assert.Equal(hueLower.ToString(), projection.HueLower);
        Assert.Equal(hueUpper.ToString(), projection.HueUpper);
        Assert.Equal(saturationLower.ToString(), projection.SaturationLower);
        Assert.Equal(saturationUpper.ToString(), projection.SaturationUpper);
        Assert.Equal(valueLower.ToString(), projection.ValueLower);
        Assert.Equal(valueUpper.ToString(), projection.ValueUpper);
    }
}
