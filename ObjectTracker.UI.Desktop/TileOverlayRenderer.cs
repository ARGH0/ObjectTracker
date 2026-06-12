using System;
using System.Collections.Generic;
using ObjectTracker.UI.Desktop.Region.Model;
using OpenCvSharp;

namespace ObjectTracker.UI.Desktop;

public static class TileOverlayRenderer
{
    public static byte[] DrawRegions(
        byte[] rawJpeg,
        int gridColumns,
        int gridRows,
        IReadOnlyList<RegionOverlayInfo> regions)
    {
        using var frame = Cv2.ImDecode(rawJpeg, ImreadModes.Color);
        if (frame.Empty() || regions.Count == 0)
            return rawJpeg;

        var result = new Mat();
        frame.CopyTo(result);

        var scaleW = (double)frame.Width / gridColumns;
        var scaleH = (double)frame.Height / gridRows;
        var cellSize = Math.Min(scaleW, scaleH);
        var offsetX = (frame.Width - cellSize * gridColumns) / 2;
        var offsetY = (frame.Height - cellSize * gridRows) / 2;

        foreach (var region in regions)
        {
            var color = region.Type switch
            {
                20 => new Scalar(0, 255, 0),
                30 => new Scalar(0, 0, 255),
                _ => new Scalar(200, 200, 200)
            };

            foreach (var cell in region.Cells)
            {
                var x = offsetX + cell.Column * cellSize;
                var y = offsetY + cell.Row * cellSize;
                Cv2.Rectangle(result, new OpenCvSharp.Point((int)x, (int)y),
                    new OpenCvSharp.Point((int)(x + cellSize), (int)(y + cellSize)),
                    color, 1);
            }
        }

        return EncodeFrame(result);
    }

    private static byte[] EncodeFrame(Mat frame)
    {
        Cv2.ImEncode(".jpg", frame, out var jpegBytes, new[] { (int)ImwriteFlags.JpegQuality, 85 });
        return jpegBytes;
    }
}

public readonly record struct RegionOverlayInfo(
    string Name,
    int Type,
    IReadOnlyList<GridCell> Cells);
