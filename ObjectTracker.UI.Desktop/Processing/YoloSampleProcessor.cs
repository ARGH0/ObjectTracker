using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace ObjectTracker.UI.Desktop;

internal sealed class YoloSampleProcessor : IOpenCvSampleProcessor
{
    private const int InputImageSize = 640;
    private const float ConfidenceThreshold = 0.35f;
    private const float NmsThreshold = 0.45f;

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
            return CreateWarningResult(
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
            var net = EnsureNet(modelPath, labelPath);
            var detections = RunInference(net, source);

            var details = new List<string>
            {
                $"Model: {Path.GetFileName(modelPath)}",
                $"Detections: {detections.Count}"
            };

            for (var index = 0; index < detections.Count; index++)
            {
                var detection = detections[index];
                var rect = detection.Bounds;
                var classId = detection.ClassId;
                var confidence = detection.Confidence;
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

            if (detections.Count > 12)
            {
                details.Add($"... plus {detections.Count - 12} more detection(s)");
            }

            if (labelPath is not null)
            {
                details.Insert(1, $"Labels: {Path.GetFileName(labelPath)} ({_labels.Length} classes)");
            }

            var status = detections.Count == 0
                ? $"Processed {sourceName} with YOLO. No detections exceeded the confidence threshold."
                : $"Processed {sourceName} with YOLO. Detected {detections.Count} object(s).";

            return OpenCvSampleProcessingHelpers.CreateResult(status, details, annotated);
        }
        catch (Exception ex)
        {
            return CreateWarningResult(
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
            _loadedModelPath = null;
            _labels = [];
        }
    }

    private Net EnsureNet(string modelPath, string? labelPath)
    {
        lock (_sync)
        {
            if (_net is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return _net;
            }

            _net?.Dispose();
            var loadedNet = CvDnn.ReadNetFromOnnx(modelPath);
            if (loadedNet is null)
            {
                throw new InvalidOperationException($"OpenCV could not load the YOLO model at '{modelPath}'.");
            }

            loadedNet.SetPreferableBackend(Backend.OPENCV);
            loadedNet.SetPreferableTarget(Target.CPU);
            _net = loadedNet;

            _loadedModelPath = modelPath;
            _labels = labelPath is null
                ? []
                : File.ReadAllLines(labelPath)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();

            return _net!;
        }
    }

    private static List<YoloDetection> RunInference(Net net, Mat source)
    {
        using var blob = CvDnn.BlobFromImage(source, 1d / 255d, new Size(InputImageSize, InputImageSize), new Scalar(), swapRB: true, crop: false);
        net.SetInput(blob);

        var outputNames = net.GetUnconnectedOutLayersNames()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
        if (outputNames.Length == 0)
        {
            return [];
        }

        var outputBlobs = outputNames.Select(_ => new Mat()).ToArray();

        try
        {
            net.Forward(outputBlobs, outputNames);

            if (outputBlobs.Length == 0)
            {
                return [];
            }

            return ParseDetections(outputBlobs[0], source.Size());
        }
        finally
        {
            foreach (var outputBlob in outputBlobs)
            {
                outputBlob.Dispose();
            }
        }
    }

    private static List<YoloDetection> ParseDetections(Mat output, Size imageSize)
    {
        using var candidates = ReshapeOutput(output);

        var rows = candidates.Rows;
        var cols = candidates.Cols;
        if (rows <= 0 || cols <= 5)
        {
            return [];
        }

        var boxes = new List<Rect>();
        var confidences = new List<float>();
        var classIds = new List<int>();

        var xScale = imageSize.Width / (double)InputImageSize;
        var yScale = imageSize.Height / (double)InputImageSize;
        var classStartIndex = DetermineClassStartIndex(cols);

        for (var row = 0; row < rows; row++)
        {
            var centerX = candidates.At<float>(row, 0);
            var centerY = candidates.At<float>(row, 1);
            var width = candidates.At<float>(row, 2);
            var height = candidates.At<float>(row, 3);

            if (width <= 0 || height <= 0)
            {
                continue;
            }

            var bestClassScore = 0f;
            var bestClassId = -1;
            for (var col = classStartIndex; col < cols; col++)
            {
                var score = candidates.At<float>(row, col);
                if (score > bestClassScore)
                {
                    bestClassScore = score;
                    bestClassId = col - classStartIndex;
                }
            }

            var objectness = classStartIndex == 5 ? candidates.At<float>(row, 4) : 1f;
            var confidence = objectness * bestClassScore;
            if (confidence < ConfidenceThreshold)
            {
                continue;
            }

            var left = (int)Math.Round((centerX - width / 2f) * xScale);
            var top = (int)Math.Round((centerY - height / 2f) * yScale);
            var boxWidth = (int)Math.Round(width * xScale);
            var boxHeight = (int)Math.Round(height * yScale);

            var rect = ClampRect(new Rect(left, top, boxWidth, boxHeight), imageSize);
            if (rect.Width <= 1 || rect.Height <= 1)
            {
                continue;
            }

            boxes.Add(rect);
            confidences.Add(confidence);
            classIds.Add(bestClassId);
        }

        if (boxes.Count == 0)
        {
            return [];
        }

        CvDnn.NMSBoxes(boxes, confidences, ConfidenceThreshold, NmsThreshold, out var keptIndices);

        return keptIndices
            .Select(index => new YoloDetection(classIds[index], confidences[index], boxes[index]))
            .OrderByDescending(detection => detection.Confidence)
            .ToList();
    }

    private static Mat ReshapeOutput(Mat output)
    {
        if (output.Dims == 2)
        {
            return output.Clone();
        }

        if (output.Dims != 3)
        {
            throw new InvalidOperationException($"Unsupported YOLO output dims: {output.Dims}.");
        }

        var dim1 = output.Size(1);
        var dim2 = output.Size(2);

        if (dim1 <= 0 || dim2 <= 0)
        {
            throw new InvalidOperationException("YOLO output tensor shape was empty.");
        }

        using var reshaped = output.Reshape(1, [dim1, dim2]);
        if (dim1 >= dim2)
        {
            return reshaped.Clone();
        }

        var transposed = new Mat();
        Cv2.Transpose(reshaped, transposed);
        return transposed;
    }

    private static int DetermineClassStartIndex(int columnCount)
    {
        return columnCount >= 85 ? 5 : 4;
    }

    private static Rect ClampRect(Rect rect, Size imageSize)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, imageSize.Width - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(rect.X + rect.Width, x + 1, imageSize.Width);
        var bottom = Math.Clamp(rect.Y + rect.Height, y + 1, imageSize.Height);
        return new Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
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

    private static RecognitionResult CreateWarningResult(Mat annotated, string sourceName, string overlayText, IReadOnlyList<string> details)
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

    private sealed record YoloDetection(int ClassId, float Confidence, Rect Bounds);
}