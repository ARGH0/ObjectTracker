# ObjectTracker

Avalonia desktop app for hosting OpenCvSharp sample flows inside a native UI.

## Integrated samples

- Camera capture loop adapted for Avalonia preview updates
- Gaussian blur
- CLAHE
- Contours
- Canny edges
- Connected components
- FAST corners
- Histogram rendering
- HOG people detection
- Hough lines
- Morphology
- MSER region extraction
- Simple blob detector

This is the first step toward hosting the full upstream sample catalog inside one Avalonia shell. Samples that depend on external models, multiple source images, file-writing workflows, or interactive OpenCV windows still need dedicated UI and asset handling.

## Structure

- `ObjectTracker.UI.Desktop/`: the only remaining project in the solution
- `ObjectTracker.slnx`: solution containing the single Avalonia app

## Commands

```bash
dotnet restore ObjectTracker.UI.Desktop/ObjectTracker.UI.Desktop.csproj
dotnet build ObjectTracker.UI.Desktop/ObjectTracker.UI.Desktop.csproj
dotnet run --project ObjectTracker.UI.Desktop/ObjectTracker.UI.Desktop.csproj
```

Use the mode picker in the desktop app to switch between imported sample flows, then either open an image or start the default camera.
