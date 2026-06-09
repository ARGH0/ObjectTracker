using System;
using System.Collections.Generic;

namespace ObjectTracker.UI.Desktop.Region.Model;

public readonly record struct RegionDefinition(
    Guid Id,
    string Name,
    RegionType Type,
    CameraZoneId CameraZoneId,
    IReadOnlyCollection<GridCell> Cells,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<string>? OverlappingZoneIds);
