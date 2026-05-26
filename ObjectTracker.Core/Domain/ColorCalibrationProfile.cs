namespace ObjectTracker.Core.Domain;

public readonly record struct ColorCalibrationProfile(
    string Name,
    int HueLower,
    int HueUpper,
    int SaturationLower,
    int SaturationUpper,
    int ValueLower,
    int ValueUpper);
