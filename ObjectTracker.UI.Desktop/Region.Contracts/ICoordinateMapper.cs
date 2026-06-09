using GridCell = ObjectTracker.UI.Desktop.Region.Model.GridCell;

namespace ObjectTracker.UI.Desktop.Region.Contracts;

public interface ICoordinateMapper
{
    GridCell Map(float pixelX, float pixelXScale, float pixelY, float pixelYScale, int gridCols, int gridRows);
}
