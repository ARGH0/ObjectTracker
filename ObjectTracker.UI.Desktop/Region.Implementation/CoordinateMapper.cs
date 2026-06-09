using System;
using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;

namespace ObjectTracker.UI.Desktop.Region.Implementation;

public sealed class CoordinateMapper : ObjectTracker.UI.Desktop.Region.Contracts.ICoordinateMapper
{
    public GridCell Map(float pixelX, float pixelXScale, float pixelY, float pixelYScale, int gridCols, int gridRows)
    {
        var col = (int)Math.Floor(pixelX * pixelXScale);
        var row = (int)Math.Floor(pixelY * pixelYScale);
        return new GridCell(col, row);
    }
}
