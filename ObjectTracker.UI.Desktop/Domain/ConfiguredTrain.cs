using System;
using ObjectTracker.Core.Domain;

namespace ObjectTracker.UI.Desktop;

public readonly record struct ConfiguredTrain(
    Guid Id,
    string Name,
    string PlcId,
    uint MinColor,
    uint MaxColor,
    ColorCalibrationProfile Calibration);
