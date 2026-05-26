using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace ObjectTracker.Vision;

public sealed class YoloDetector : ICachedDetectionAlgorithm
{
    private readonly Net? _net;
    private readonly string _modelPath;
    private readonly bool _initialized;

    public DetectorMode Mode => DetectorMode.Yolo;
    public string Name => "YOLO v8";

    public YoloDetector(string modelPath)
    {
        _modelPath = modelPath;
        try
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"YOLO model not found: {modelPath}");
            }

            _net = CvDnn.ReadNetFromOnnx(modelPath);
            // Note: Backend and Target configuration depends on OpenCV build
            // Default backend/target will be used for compatibility
            _initialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"YoloDetector init failed: {ex.Message}");
            _initialized = false;
        }
    }

    public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_initialized || _net == null)
        {
            return Task.FromResult<IReadOnlyList<Detection>>([]);
        }

        try
        {
            using var image = Cv2.ImDecode(frame.EncodedJpeg, ImreadModes.Color);
            if (image.Empty())
            {
                return Task.FromResult<IReadOnlyList<Detection>>([]);
            }

            cancellationToken.ThrowIfCancellationRequested();

            const int inputSize = 640;
            using var blob = CvDnn.BlobFromImage(image, 1.0 / 255.0, new Size(inputSize, inputSize), new Scalar(0, 0, 0), swapRB: true, crop: false);
            _net.SetInput(blob);

            cancellationToken.ThrowIfCancellationRequested();

            var detections = new List<Detection>();
            using var output = _net.Forward();

            // YOLOv8 ONNX output: (1, 84, 8400) — 4 bbox + 80 classes
            var outputData = new Mat();
            output.Reshape(1, output.Size(2)).CopyTo(outputData);

            var stride = (float)image.Width / inputSize;
            var detectedBoxes = new List<(float x, float y, float w, float h, float conf, int cls)>();

            for (int i = 0; i < outputData.Rows; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = outputData.Row(i);
                float x = row.At<float>(0) * stride;
                float y = row.At<float>(1) * stride;
                float w = row.At<float>(2) * stride;
                float h = row.At<float>(3) * stride;
                float conf = row.At<float>(4);

                if (conf < 0.5f) continue;

                int bestClass = 0;
                float bestScore = row.At<float>(5);
                for (int j = 6; j < 84; j++)
                {
                    float score = row.At<float>(j);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = j - 5;
                    }
                }

                detectedBoxes.Add((x, y, w, h, conf * bestScore, bestClass));
            }

            // Simple NMS
            detectedBoxes.Sort((a, b) => b.conf.CompareTo(a.conf));
            var kept = new List<int>();
            for (int i = 0; i < detectedBoxes.Count; i++)
            {
                bool overlaps = false;
                for (int j = 0; j < kept.Count; j++)
                {
                    var iou = ComputeIoU(detectedBoxes[i], detectedBoxes[kept[j]]);
                    if (iou > 0.45f)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps) kept.Add(i);
            }

            foreach (var idx in kept)
            {
                var (x, y, w, h, conf, cls) = detectedBoxes[idx];
                detections.Add(new Detection(
                    $"yolo-{idx}",
                    x + w / 2,
                    y + h / 2,
                    x,
                    y,
                    w,
                    h,
                    conf,
                    $"class-{cls}",
                    frame.SourceId,
                    frame.TimestampUtcMs));
            }

            return Task.FromResult<IReadOnlyList<Detection>>(detections);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"YoloDetector error: {ex.Message}");
            return Task.FromResult<IReadOnlyList<Detection>>([]);
        }
    }

    public Task<IReadOnlyList<Detection>> DetectAsync(FramePacket frame, Mat decodedImage, CancellationToken cancellationToken)
    {
        // decodedImage is expected to be BGR (Color) Mat
        cancellationToken.ThrowIfCancellationRequested();

        if (!_initialized || _net == null || decodedImage.Empty())
        {
            return Task.FromResult<IReadOnlyList<Detection>>([]);
        }

        try
        {
            const int inputSize = 640;
            using var blob = CvDnn.BlobFromImage(decodedImage, 1.0 / 255.0, new Size(inputSize, inputSize), new Scalar(0, 0, 0), swapRB: true, crop: false);
            _net.SetInput(blob);

            cancellationToken.ThrowIfCancellationRequested();

            var detections = new List<Detection>();
            using var output = _net.Forward();

            // YOLOv8 ONNX output: (1, 84, 8400) — 4 bbox + 80 classes
            var outputData = new Mat();
            output.Reshape(1, output.Size(2)).CopyTo(outputData);

            var stride = (float)decodedImage.Width / inputSize;
            var detectedBoxes = new List<(float x, float y, float w, float h, float conf, int cls)>();

            for (int i = 0; i < outputData.Rows; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = outputData.Row(i);
                float x = row.At<float>(0) * stride;
                float y = row.At<float>(1) * stride;
                float w = row.At<float>(2) * stride;
                float h = row.At<float>(3) * stride;
                float conf = row.At<float>(4);

                if (conf < 0.5f) continue;

                int bestClass = 0;
                float bestScore = row.At<float>(5);
                for (int j = 6; j < 84; j++)
                {
                    float score = row.At<float>(j);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = j - 5;
                    }
                }

                detectedBoxes.Add((x, y, w, h, conf * bestScore, bestClass));
            }

            // Simple NMS
            detectedBoxes.Sort((a, b) => b.conf.CompareTo(a.conf));
            var kept = new List<int>();
            for (int i = 0; i < detectedBoxes.Count; i++)
            {
                bool overlaps = false;
                for (int j = 0; j < kept.Count; j++)
                {
                    var iou = ComputeIoU(detectedBoxes[i], detectedBoxes[kept[j]]);
                    if (iou > 0.45f)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps) kept.Add(i);
            }

            foreach (var idx in kept)
            {
                var (x, y, w, h, conf, cls) = detectedBoxes[idx];
                detections.Add(new Detection(
                    $"yolo-{idx}",
                    x + w / 2,
                    y + h / 2,
                    x,
                    y,
                    w,
                    h,
                    conf,
                    $"class-{cls}",
                    frame.SourceId,
                    frame.TimestampUtcMs));
            }

            outputData.Dispose();
            return Task.FromResult<IReadOnlyList<Detection>>(detections);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"YoloDetector error: {ex.Message}");
            return Task.FromResult<IReadOnlyList<Detection>>([]);
        }
    }

    private static float ComputeIoU((float x, float y, float w, float h, float conf, int cls) a, (float x, float y, float w, float h, float conf, int cls) b)
    {
        float x1 = Math.Max(a.x, b.x);
        float y1 = Math.Max(a.y, b.y);
        float x2 = Math.Min(a.x + a.w, b.x + b.w);
        float y2 = Math.Min(a.y + a.h, b.y + b.h);

        float inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        float union = a.w * a.h + b.w * b.h - inter;

        return union > 0 ? inter / union : 0;
    }
}
