# ObjectTracker

Avalonia desktop app for hosting OpenCvSharp sample flows inside a native UI.

## Integrated samples

- Camera capture loop adapted for Avalonia preview updates
- Gaussian blur
- CLAHE
- Contours
- Canny edges
- Connected components
- Train collision risk monitoring for moving objects in a fixed camera view
- FAST corners
- Histogram rendering
- HOG people detection
- Hough lines
- Morphology
- MSER region extraction
- YOLO DNN inference from local ONNX models
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

## YOLO setup

The `YOLO DNN` mode looks for model assets in `ObjectTracker.UI.Desktop/Models/` and copies them to the build output automatically.

- Supported model filenames: `yolov8n.onnx`, `yolo11n.onnx`, `yolo.onnx`, or `model.onnx`
- Optional label filenames: `coco.names`, `labels.txt`, or `classes.txt`

If no model file is present, the UI will keep running and show a clear status message instead of crashing.
*** Add File: /home/aloon/dev/github/ObjectTracker/ObjectTracker.UI.Desktop/Processing/YoloSampleProcessor.cs
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace ObjectTracker.UI.Desktop;

internal sealed class YoloSampleProcessor : IOpenCvSampleProcessor
{
	private static readonly string[] ModelFileNames =
	[
		"yolov8n.onnx",
		"yolo11n.onnx",
		"yolo.onnx",
		"model.onnx"
	];

	private static readonly string[] LabelFileNames =
	[
		"coco.names",
		"labels.txt",
		"classes.txt"
	];

	private readonly object _sync = new();
	private DetectionModel? _model;
	private Net? _net;
	private string? _loadedModelPath;
	private string[] _labels = [];

	public OpenCvSampleMode Mode => OpenCvSampleMode.Yolo;

	public RecognitionResult Process(Mat source, string sourceName)
	{
		using var annotated = source.Clone();

		var modelsDirectory = Path.Combine(AppContext.BaseDirectory, "Models");
		var modelPath = TryFindFirstExistingFile(modelsDirectory, ModelFileNames);
		if (modelPath is null)
		{
			return CreateMissingAssetResult(
				annotated,
				sourceName,
				"YOLO model missing.",
				[
					$"Looked in: {modelsDirectory}",
					$"Expected one of: {string.Join(", ", ModelFileNames)}",
					"Add an ONNX export such as yolov8n.onnx to enable this sample."
				]);
		}

		var labelPath = TryFindFirstExistingFile(modelsDirectory, LabelFileNames);

		try
		{
			var model = EnsureModel(modelPath, labelPath);
			model.Detect(source, out var classIds, out var confidences, out var boxes, 0.35f, 0.45f);

			var details = new List<string>
			{
				$"Model: {Path.GetFileName(modelPath)}",
				$"Detections: {boxes.Length}"
			};

			for (var index = 0; index < boxes.Length; index++)
			{
				var rect = boxes[index];
				var classId = index < classIds.Length ? classIds[index] : -1;
				var confidence = index < confidences.Length ? confidences[index] : 0f;
				var label = ResolveLabel(classId);
				var color = CreateColorForClass(classId);

				Cv2.Rectangle(annotated, rect, color, 3);
				Cv2.PutText(
					annotated,
					$"{label} {confidence:P0}",
					new Point(rect.X, Math.Max(24, rect.Y - 8)),
					HersheyFonts.HersheySimplex,
					0.7,
					color,
					2,
					LineTypes.AntiAlias);

				if (index < 12)
				{
					details.Add($"{label}: {confidence:P1}, {rect.Width}x{rect.Height} at ({rect.X}, {rect.Y})");
				}
			}

			if (boxes.Length > 12)
			{
				details.Add($"... plus {boxes.Length - 12} more detection(s)");
			}

			if (labelPath is not null)
			{
				details.Insert(1, $"Labels: {Path.GetFileName(labelPath)} ({_labels.Length} classes)");
			}

			var status = boxes.Length == 0
				? $"Processed {sourceName} with YOLO. No detections exceeded the confidence threshold."
				: $"Processed {sourceName} with YOLO. Detected {boxes.Length} object(s).";

			return OpenCvSampleProcessingHelpers.CreateResult(status, details, annotated);
		}
		catch (Exception ex)
		{
			return CreateMissingAssetResult(
				annotated,
				sourceName,
				$"YOLO inference failed: {ex.Message}",
				[
					$"Model: {modelPath}",
					labelPath is null ? "Labels: none" : $"Labels: {labelPath}",
					"Verify that the ONNX export is supported by the OpenCV DNN runtime."
				]);
		}
	}

	public void Reset()
	{
		lock (_sync)
		{
			_net?.Dispose();
			_net = null;
			_model = null;
			_loadedModelPath = null;
			_labels = [];
		}
	}

	private DetectionModel EnsureModel(string modelPath, string? labelPath)
	{
		lock (_sync)
		{
			if (_model is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
			{
				return _model;
			}

			_net?.Dispose();
			_net = CvDnn.ReadNetFromOnnx(modelPath);
			_net.SetPreferableBackend(Backend.OPENCV);
			_net.SetPreferableTarget(Target.CPU);

			_model = new DetectionModel(_net);
			_model.SetInputParams(scale: 1d / 255d, size: new Size(640, 640), mean: new Scalar(), swapRB: true, crop: false);

			_loadedModelPath = modelPath;
			_labels = labelPath is null
				? []
				: File.ReadAllLines(labelPath)
					.Select(line => line.Trim())
					.Where(line => !string.IsNullOrWhiteSpace(line))
					.ToArray();

			return _model;
		}
	}

	private static string? TryFindFirstExistingFile(string directoryPath, IEnumerable<string> fileNames)
	{
		foreach (var fileName in fileNames)
		{
			var candidatePath = Path.Combine(directoryPath, fileName);
			if (File.Exists(candidatePath))
			{
				return candidatePath;
			}
		}

		return null;
	}

	private RecognitionResult CreateMissingAssetResult(Mat annotated, string sourceName, string overlayText, IReadOnlyList<string> details)
	{
		Cv2.PutText(
			annotated,
			overlayText,
			new Point(20, 36),
			HersheyFonts.HersheySimplex,
			0.8,
			new Scalar(48, 82, 214),
			2,
			LineTypes.AntiAlias);

		return OpenCvSampleProcessingHelpers.CreateResult($"Processed {sourceName} with YOLO setup warnings.", details, annotated);
	}

	private string ResolveLabel(int classId)
	{
		if (classId >= 0 && classId < _labels.Length)
		{
			return _labels[classId];
		}

		return classId >= 0 ? $"Class {classId}" : "Object";
	}

	private static Scalar CreateColorForClass(int classId)
	{
		var normalized = Math.Max(0, classId) + 1;
		return new Scalar((normalized * 53) % 255, (normalized * 97) % 255, (normalized * 193) % 255);
	}
}
