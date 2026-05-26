using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using OpenCvSharp;

namespace ObjectTracker.Vision;

public sealed class SessionCalibrationService
{
    private readonly Lock bakeSync = new ();
    private readonly Dictionary<string, Task<string>> bakeJobs = new (StringComparer.OrdinalIgnoreCase);

    public Task PreBakeBackgroundAsync(string videoPath, int sampleCount, int processMaxWidth, CancellationToken cancellationToken)
    {
        return PreBakeBackgroundInternalAsync(videoPath, sampleCount, processMaxWidth, cancellationToken);
    }

    public async Task<string> EnsureBakedBackgroundAsync(
        string videoPath,
        int sampleCount,
        int processMaxWidth,
        string? bakeImagePath,
        CancellationToken cancellationToken,
        Func<string, Task>? onStatus = null)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video file not found.", videoPath);
        }

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Unable to open video: {Path.GetFileName(videoPath)}");
        }

        var frameWidth = capture.FrameWidth;
        var frameHeight = capture.FrameHeight;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            throw new InvalidOperationException("Video has invalid dimensions.");
        }

        var processSize = BuildProcessSize(frameWidth, frameHeight, processMaxWidth);
        if (!string.IsNullOrWhiteSpace(bakeImagePath))
        {
            if (onStatus is not null)
            {
                await onStatus($"preparing baked background from image {Path.GetFileName(bakeImagePath)}...");
            }

            return await BuildBackgroundFromImageAsync(bakeImagePath, processSize, cancellationToken);
        }

        if (onStatus is not null)
        {
            await onStatus($"baking background for {Path.GetFileName(videoPath)}...");
        }

        return await BakeBackgroundAsync(videoPath, sampleCount, processSize, cancellationToken, onStatus);
    }

    private async Task PreBakeBackgroundInternalAsync(string videoPath, int sampleCount, int processMaxWidth, CancellationToken cancellationToken)
    {
        if (!File.Exists(videoPath))
        {
            return;
        }

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            return;
        }

        var frameWidth = capture.FrameWidth;
        var frameHeight = capture.FrameHeight;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return;
        }

        var processSize = BuildProcessSize(frameWidth, frameHeight, processMaxWidth);
        await BakeBackgroundAsync(videoPath, sampleCount, processSize, cancellationToken);
    }

    private Task<string> BakeBackgroundAsync(
        string videoPath,
        int sampleCount,
        Size processSize,
        CancellationToken cancellationToken,
        Func<string, Task>? onStatus = null)
    {
        var bakedPath = BuildBackgroundFilePath(videoPath, sampleCount, processSize);

        if (File.Exists(bakedPath))
        {
            return Task.FromResult(bakedPath);
        }

        lock (bakeSync)
        {
            if (bakeJobs.TryGetValue(bakedPath, out var running))
            {
                return running;
            }

            var bakeTask = BakeBackgroundCoreAsync(videoPath, sampleCount, processSize, bakedPath, cancellationToken, onStatus);
            bakeJobs[bakedPath] = bakeTask;
            _ = bakeTask.ContinueWith(_ =>
            {
                lock (bakeSync)
                {
                    bakeJobs.Remove(bakedPath);
                }
            }, TaskScheduler.Default);

            return bakeTask;
        }
    }

    private static async Task<string> BakeBackgroundCoreAsync(
        string videoPath,
        int sampleCount,
        Size processSize,
        string bakedPath,
        CancellationToken cancellationToken,
        Func<string, Task>? onStatus)
    {
        await Task.Yield();

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Unable to open video for baking: {Path.GetFileName(videoPath)}");
        }

        var frameCount = (int)Math.Max(0, capture.Get(VideoCaptureProperties.FrameCount));
        var progressStep = Math.Max(1, sampleCount / 10);
        var fileName = Path.GetFileName(videoPath);

        using var medianBackground = EstimateMedianBackground(
            capture,
            frameCount,
            sampleCount,
            processSize,
            cancellationToken,
            reportProgress: (completed, total) =>
            {
                if (onStatus is null)
                {
                    return;
                }

                if (completed % progressStep != 0 && completed != total)
                {
                    return;
                }

                onStatus($"baking {fileName}: sample {completed}/{total}").GetAwaiter().GetResult();
            });

        var directory = Path.GetDirectoryName(bakedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Cv2.ImWrite(bakedPath, medianBackground);
        return bakedPath;
    }

    private async Task<string> BuildBackgroundFromImageAsync(
        string imagePath,
        Size processSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Bake image not found.", imagePath);
        }

        var bakedPath = BuildBackgroundImageFilePath(imagePath, processSize);
        if (File.Exists(bakedPath))
        {
            return bakedPath;
        }

        await Task.Yield();

        using var background = LoadBackgroundImageMat(imagePath, processSize);
        var directory = Path.GetDirectoryName(bakedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Cv2.ImWrite(bakedPath, background);
        return bakedPath;
    }

    private static Mat LoadBackgroundImageMat(string imagePath, Size processSize)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Bake image not found.", imagePath);
        }

        using var source = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (source.Empty())
        {
            throw new InvalidOperationException($"Unable to load bake image: {Path.GetFileName(imagePath)}");
        }

        using var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        var resized = new Mat();
        Cv2.Resize(gray, resized, processSize, interpolation: InterpolationFlags.Area);
        return resized;
    }

    private static string BuildBackgroundFilePath(string videoPath, int sampleCount, Size processSize)
    {
        var info = new FileInfo(videoPath);
        var keyRaw = $"{videoPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{sampleCount}|{processSize.Width}|{processSize.Height}";
        var key = ComputeSha256Hex(keyRaw);
        return Path.Combine(GetBackgroundCacheRoot(), $"{key}.png");
    }

    private static string BuildBackgroundImageFilePath(string imagePath, Size processSize)
    {
        var info = new FileInfo(imagePath);
        var keyRaw = $"image|{imagePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{processSize.Width}|{processSize.Height}";
        var key = ComputeSha256Hex(keyRaw);
        return Path.Combine(GetBackgroundCacheRoot(), $"{key}.png");
    }

    private static string GetBackgroundCacheRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ObjectTracker",
            "background-cache");
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Size BuildProcessSize(int sourceWidth, int sourceHeight, int maxWidth)
    {
        if (sourceWidth <= maxWidth)
        {
            return new Size(sourceWidth, sourceHeight);
        }

        var scale = (double)maxWidth / sourceWidth;
        var targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new Size(maxWidth, targetHeight);
    }

    private static Mat EstimateMedianBackground(
        VideoCapture capture,
        int frameCount,
        int sampleCount,
        Size processSize,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress = null)
    {
        var validFrameCount = Math.Max(1, frameCount);
        var ids = Enumerable.Range(0, sampleCount)
            .Select(_ => Random.Shared.Next(0, validFrameCount))
            .ToArray();

        var sampledFrames = new List<Mat>(sampleCount);
        using var sampledFrame = new Mat();
        using var sampledGray = new Mat();

        var completedSamples = 0;
        foreach (var frameId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            capture.PosFrames = frameId;
            if (!capture.Read(sampledFrame) || sampledFrame.Empty())
            {
                continue;
            }

            Cv2.CvtColor(sampledFrame, sampledGray, ColorConversionCodes.BGR2GRAY);
            var resized = new Mat();
            Cv2.Resize(sampledGray, resized, processSize, interpolation: InterpolationFlags.Area);
            sampledFrames.Add(resized);

            completedSamples++;
            reportProgress?.Invoke(completedSamples, ids.Length);
        }

        if (sampledFrames.Count == 0)
        {
            throw new InvalidOperationException("No frames available to estimate background.");
        }

        return BuildMedianBackground(sampledFrames, processSize);
    }

    private static Mat BuildMedianBackground(List<Mat> sampledFrames, Size processSize)
    {
        var pixelCount = processSize.Width * processSize.Height;
        var samples = sampledFrames.Select(ToByteArray).ToArray();
        var median = new byte[pixelCount];
        var values = new byte[samples.Length];

        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                values[i] = samples[i][pixel];
            }

            Array.Sort(values);
            median[pixel] = values[values.Length / 2];
        }

        foreach (var mat in sampledFrames)
        {
            mat.Dispose();
        }

        var medianMat = new Mat(processSize.Height, processSize.Width, MatType.CV_8UC1);
        medianMat.SetArray(median);
        return medianMat;
    }

    private static byte[] ToByteArray(Mat mat)
    {
        var bytes = new byte[mat.Rows * mat.Cols];
        mat.GetArray(out byte[] raw);
        Buffer.BlockCopy(raw, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
