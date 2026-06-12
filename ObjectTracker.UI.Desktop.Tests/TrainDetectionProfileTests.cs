using System;
using System.Linq;
using ObjectTracker.Core.Domain;
using OpenCvSharp;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class TrainDetectionProfileTests
{
    [Fact]
    public void FromConfiguredTrains_PreservesTrainIdentityAndCalibrationForDetection()
    {
        var trainId = Guid.NewGuid();
        var trains = new[]
        {
            new ConfiguredTrain(
                trainId,
                "Cargo Red",
                "PLC-17",
                0xFF8A0000,
                0xFFFF6060,
                new ColorCalibrationProfile("Cargo Red", 170, 10, 120, 255, 70, 255))
        };

        var profile = TrainDetectionProfile.FromConfiguredTrains(trains).Single();

        Assert.Equal(trainId, profile.TrainId);
        Assert.Equal("Cargo Red", profile.TrainName);
        Assert.Equal("PLC-17", profile.PlcId);
        Assert.Equal("Cargo Red", profile.Calibration.Name);
        Assert.Equal(170, profile.Calibration.HueLower);
        Assert.Equal(10, profile.Calibration.HueUpper);
        Assert.Equal(0x60, (byte)profile.OverlayColor.Val0);
        Assert.Equal(0x60, (byte)profile.OverlayColor.Val1);
        Assert.Equal(0xFF, (byte)profile.OverlayColor.Val2);
    }

    [Fact]
    public void ProcessingOptions_UsesConfiguredTrainProfilesForDetection()
    {
        var trainId = Guid.NewGuid();
        var train = new ConfiguredTrain(
            trainId,
            "Cargo Red",
            "PLC-17",
            0xFF8A0000,
            0xFFFF6060,
            new ColorCalibrationProfile("Cargo Red", 170, 10, 120, 255, 70, 255));
        var profiles = TrainDetectionProfile.FromConfiguredTrains(new[] { train });

        var options = new BackgroundEstimationEngine.ProcessingOptions(
            ProcessMaxWidth: 640,
            MinMotionArea: 220,
            MinColorPixels: 40,
            MorphKernelSize: 3,
            AdaptiveBackgroundSampleCount: 30,
            AdaptiveBackgroundUpdateIntervalFrames: 30,
            Trains: profiles);

        Assert.Equal(trainId, options.Trains.Single().TrainId);
        Assert.Equal("PLC-17", options.Trains.Single().PlcId);
    }

    [Fact]
    public void BuildTrainDetectionCandidates_WhenSameTrainMatchesMultipleMovingRects_EmitsOneCandidate()
    {
        var train = TrainStore.CreateDefaultTrains().Single(item => item.Name == "Red Train");
        var profile = TrainDetectionProfile.FromConfiguredTrains(new[] { train }).Single();
        var rects = new[]
        {
            new Rect(0, 0, 10, 10),
            new Rect(20, 0, 10, 10)
        };
        var matches = new TrainDetectionProfile?[] { profile, profile };

        var candidates = BackgroundEstimationEngine.BuildTrainDetectionCandidates(rects, matches);

        var candidate = Assert.Single(candidates);
        Assert.Equal(train.Id, candidate.Train.TrainId);
        Assert.Equal(rects[0], candidate.Rect);
    }

    [Fact]
    public void BuildTrainDetectionCandidates_WhenRectExceedsFormerTrainMaxDimensions_StillEmitsCandidate()
    {
        var train = new ConfiguredTrain(
            Guid.NewGuid(),
            "Cargo Red",
            "PLC-17",
            0xFF8A0000,
            0xFFFF6060,
            new ColorCalibrationProfile("Cargo Red", 170, 10, 120, 255, 70, 255));
        var profile = TrainDetectionProfile.FromConfiguredTrains(new[] { train }).Single();
        var rects = new[] { new Rect(0, 0, 30, 10) };
        var matches = new TrainDetectionProfile?[] { profile };

        var candidates = BackgroundEstimationEngine.BuildTrainDetectionCandidates(rects, matches);

        var candidate = Assert.Single(candidates);
        Assert.Equal(rects[0], candidate.Rect);
    }

    [Fact]
    public void BuildTrainDetectionCandidates_WhenTrainColorIsUnknown_DoesNotEmitCandidate()
    {
        var rects = new[] { new Rect(0, 0, 10, 10) };
        var matches = new TrainDetectionProfile?[] { null };

        var candidates = BackgroundEstimationEngine.BuildTrainDetectionCandidates(rects, matches);

        Assert.Empty(candidates);
    }

    [Fact]
    public void RenderColorDetections_WhenTrainColorIsUnknown_DoesNotDrawDetectionBox()
    {
        using var source = new Mat(24, 24, MatType.CV_8UC3, Scalar.All(20));
        using var destination = source.Clone();
        using var motionMask = new Mat(24, 24, MatType.CV_8UC1, Scalar.Black);
        using var hsv = new Mat();
        var rect = new Rect(4, 4, 10, 10);
        Cv2.Rectangle(motionMask, rect, Scalar.White, -1);

        BackgroundEstimationEngine.RenderColorDetections(
            destination,
            source,
            motionMask,
            hsv,
            new[] { rect },
            Array.Empty<TrainDetectionProfile>(),
            minColorPixels: 1);

        using var diff = new Mat();
        Cv2.Absdiff(source, destination, diff);

        Assert.Equal(0, Cv2.CountNonZero(diff.Reshape(1)));
    }
}
