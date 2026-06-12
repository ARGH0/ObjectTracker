using System;
using System.Collections.Generic;
using System.Linq;
using ObjectTracker.Core.Domain;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

public readonly record struct TrainDetectionProfile(
    ConfiguredTrain Train,
    Guid TrainId,
    string TrainName,
    string PlcId,
    ColorCalibrationProfile Calibration,
    Scalar OverlayColor)
{
    public static IReadOnlyList<TrainDetectionProfile> FromConfiguredTrains(IReadOnlyList<ConfiguredTrain> trains)
    {
        return trains
            .Where(train => !string.IsNullOrWhiteSpace(train.Name))
            .Select(train => new TrainDetectionProfile(
                train,
                train.Id,
                train.Name.Trim(),
                train.PlcId.Trim(),
                train.Calibration,
                ToBgrScalar(train.MaxColor)))
            .ToList();
    }

    private static Scalar ToBgrScalar(uint argb)
    {
        var red = (byte)((argb >> 16) & 0xFF);
        var green = (byte)((argb >> 8) & 0xFF);
        var blue = (byte)(argb & 0xFF);
        return new Scalar(blue, green, red);
    }
}
