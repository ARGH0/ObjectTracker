using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : Window
{
    private const int MaxLogEntries = 300;
    private const int PreviewIntervalMs = 33;

    private readonly object _cameraSync = new();
    private readonly object _settingsSync = new();
    private readonly List<CameraProfile> _cameras = new();
    private readonly Dictionary<string, RuntimeProcessingSettings> _cameraSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<string> _logEntries = new();

    private readonly BackgroundEstimationEngine _engine = new();
    private readonly CameraSettingsStore _cameraSettingsStore = new();

    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private long _lastPreviewRenderTick;
    private int _previewRenderBusy;
    private int _selectedCameraIndex = -1;
    private int _requestedCameraIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        LogListBox.ItemsSource = _logEntries;

        foreach (var (cameraId, settings) in _cameraSettingsStore.Load())
        {
            _cameraSettings[cameraId] = settings;
        }

        HookEvents();
        RefreshCameraUi();
        AppendLog("Application initialized.");
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        await StopProcessingAsync();
        PersistCameraSettings();
        base.OnClosing(e);
    }

    private void HookEvents()
    {
        AddVideosButton.Click += AddCamerasButtonOnClick;
        RemoveSelectedButton.Click += RemoveCameraButtonOnClick;
        ClearPlaylistButton.Click += ClearCamerasButtonOnClick;
        PreviousVideoButton.Click += PreviousCameraButtonOnClick;
        NextVideoButton.Click += NextCameraButtonOnClick;
        StartStopButton.Click += StartStopButtonOnClick;
        OpenBakedMaskButton.Click += OpenBakedMaskButtonOnClick;
        PlaylistListBox.SelectionChanged += CameraSelectionChanged;

        SampleCountTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ThresholdTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MotionAreaTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ColorMinPixelsTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MorphKernelSizeTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ProcessWidthTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
    }

    private async void AddCamerasButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select mock camera video files",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Video files") { Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.m4v" } }
            }
        });

        var added = 0;
        lock (_cameraSync)
        {
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path) || _cameras.Any(c => string.Equals(c.PrimaryVideoPath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var cameraId = path;
                var displayName = BuildCameraName(path, _cameras.Count + 1);
                _cameras.Add(new CameraProfile(cameraId, displayName, new List<string> { path }));

                if (!_cameraSettings.ContainsKey(cameraId))
                {
                    _cameraSettings[cameraId] = RuntimeProcessingSettings.Default;
                }

                added++;
            }

            if (_selectedCameraIndex < 0 && _cameras.Count > 0)
            {
                _selectedCameraIndex = 0;
            }
        }

        PersistCameraSettings();
        RefreshCameraUi();
        SetStatus($"Status: added {added} mock camera(s).");

        if (_runTask is not null)
        {
            StartBakeForAllCameras(_runCts?.Token ?? CancellationToken.None);
        }
    }

    private void RemoveCameraButtonOnClick(object? sender, RoutedEventArgs e)
    {
        CameraProfile? removed = null;

        lock (_cameraSync)
        {
            var index = PlaylistListBox.SelectedIndex;
            if (index < 0 || index >= _cameras.Count)
            {
                return;
            }

            removed = _cameras[index];
            _cameras.RemoveAt(index);
            _cameraSettings.Remove(removed.Value.Id);

            if (_cameras.Count == 0)
            {
                _selectedCameraIndex = -1;
            }
            else
            {
                _selectedCameraIndex = Math.Clamp(index, 0, _cameras.Count - 1);
            }
        }

        PersistCameraSettings();
        RefreshCameraUi();

        if (_runTask is not null && _selectedCameraIndex >= 0)
        {
            Interlocked.Exchange(ref _requestedCameraIndex, _selectedCameraIndex);
        }

        SetStatus($"Status: removed camera {removed?.DisplayName ?? "-"}.");
    }

    private void ClearCamerasButtonOnClick(object? sender, RoutedEventArgs e)
    {
        lock (_cameraSync)
        {
            _cameras.Clear();
            _cameraSettings.Clear();
            _selectedCameraIndex = -1;
        }

        PersistCameraSettings();
        RefreshCameraUi();
        SetStatus("Status: all cameras cleared.");
    }

    private void PreviousCameraButtonOnClick(object? sender, RoutedEventArgs e)
    {
        NavigateCameraRelative(-1);
    }

    private void NextCameraButtonOnClick(object? sender, RoutedEventArgs e)
    {
        NavigateCameraRelative(1);
    }

    private void CameraSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var index = PlaylistListBox.SelectedIndex;
        CameraProfile? camera = null;

        lock (_cameraSync)
        {
            if (index < 0 || index >= _cameras.Count)
            {
                return;
            }

            _selectedCameraIndex = index;
            camera = _cameras[index];
        }

        if (camera is not null)
        {
            ApplySettingsToUi(GetSettingsForCamera(camera.Value.Id));
        }

        if (_runTask is not null && index >= 0)
        {
            Interlocked.Exchange(ref _requestedCameraIndex, index);
            SetStatus($"Status: switching to camera {camera?.DisplayName}...");
        }
    }

    private async void StartStopButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (_runTask is not null)
        {
            await StopProcessingAsync();
            return;
        }

        if (GetCameraCount() == 0)
        {
            SetStatus("Status: add at least one mock camera.");
            return;
        }

        UpdateSelectedCameraSettingsFromUi(logChange: false);

        var startIndex = _selectedCameraIndex >= 0 ? _selectedCameraIndex : 0;
        var loopCameraVideos = LoopPlaylistCheckBox.IsChecked == true;

        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        SetRunState(isRunning: true);
        StartBakeForAllCameras(token);
        _runTask = Task.Run(() => RunCameraSelectionAsync(startIndex, loopCameraVideos, token), token);

        try
        {
            await _runTask;
        }
        catch (OperationCanceledException)
        {
            SetStatus("Status: processing stopped.");
        }
        catch (Exception ex)
        {
            SetStatus($"Status: error - {ex.Message}");
        }
        finally
        {
            _runTask = null;
            _runCts?.Dispose();
            _runCts = null;
            SetRunState(isRunning: false);
        }
    }

    private void OpenBakedMaskButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        UpdateSelectedCameraSettingsFromUi(logChange: false);
        var settings = GetSettingsForCamera(camera.Value.Id);
        var options = new BackgroundEstimationEngine.ProcessingOptions(
            settings.ProcessMaxWidth,
            settings.MotionArea,
            settings.ColorMinPixels,
            settings.MorphKernelSize);

        var bakedPath = _engine.GetExistingBakedBackgroundPath(camera.Value.PrimaryVideoPath, settings.SampleCount, options);
        if (string.IsNullOrWhiteSpace(bakedPath))
        {
            SetStatus("Status: no baked mask found yet for this camera/video. Run or pre-bake first.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = bakedPath,
                UseShellExecute = true
            });

            SetStatus($"Status: opened baked mask {Path.GetFileName(bakedPath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Status: failed to open baked mask - {ex.Message}");
        }
    }

    private async Task StopProcessingAsync()
    {
        var task = _runTask;
        if (task is null)
        {
            return;
        }

        _runCts?.Cancel();

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation while stopping.
        }
        catch
        {
            // Ignore stop-time exceptions.
        }
    }

    private async Task RunCameraSelectionAsync(int startCameraIndex, bool loopCameraVideos, CancellationToken cancellationToken)
    {
        var cameraIndex = startCameraIndex;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!TryGetCamera(cameraIndex, out var camera))
            {
                await Dispatcher.UIThread.InvokeAsync(() => SetStatus("Status: no cameras available."));
                break;
            }

            _selectedCameraIndex = cameraIndex;
            Interlocked.Exchange(ref _selectedCameraIndex, cameraIndex);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PlaylistListBox.SelectedIndex = cameraIndex;
                ApplySettingsToUi(GetSettingsForCamera(camera.Id));
                CurrentVideoText.Text = $"Current camera/video: {camera.DisplayName} / {Path.GetFileName(camera.PrimaryVideoPath)}";
            });

            await ProcessCameraVideosAsync(camera, loopCameraVideos, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (TryConsumeCameraSwitchRequest(out var requestedIndex))
            {
                cameraIndex = requestedIndex;
                continue;
            }

            // Stay on the currently selected camera loop unless explicitly switched.
            cameraIndex = Math.Clamp(_selectedCameraIndex, 0, Math.Max(0, GetCameraCount() - 1));
        }
    }

    private async Task ProcessCameraVideosAsync(CameraProfile camera, bool loopCameraVideos, CancellationToken cancellationToken)
    {
        var videoIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (TryConsumeCameraSwitchRequest(out var _))
            {
                return;
            }

            var settings = GetSettingsForCamera(camera.Id);
            var options = new BackgroundEstimationEngine.ProcessingOptions(
                settings.ProcessMaxWidth,
                settings.MotionArea,
                settings.ColorMinPixels,
                settings.MorphKernelSize);

            if (videoIndex >= camera.VideoPaths.Count)
            {
                if (!loopCameraVideos)
                {
                    break;
                }

                videoIndex = 0;
            }

            var videoPath = camera.VideoPaths[videoIndex];
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentVideoText.Text = $"Current camera/video: {camera.DisplayName} / {Path.GetFileName(videoPath)}";
            });

            var result = await _engine.ProcessVideoAsync(
                videoPath,
                settings.SampleCount,
                settings.Threshold,
                options,
                onFrame: frameSet =>
                {
                    QueuePreviewFrame(frameSet, cancellationToken);
                    return Task.CompletedTask;
                },
                onStatus: async message => await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {message}")),
                getLiveTuning: () => GetLiveTuningForCamera(camera.Id),
                shouldStopEarly: HasPendingCameraSwitchRequest,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {result.Message}"));
            }

            if (TryConsumeCameraSwitchRequest(out var _))
            {
                return;
            }

            videoIndex++;
        }
    }

    private void StartBakeForAllCameras(CancellationToken cancellationToken)
    {
        List<CameraProfile> snapshot;
        lock (_cameraSync)
        {
            snapshot = _cameras.ToList();
        }

        foreach (var camera in snapshot)
        {
            var settings = GetSettingsForCamera(camera.Id);
            var options = new BackgroundEstimationEngine.ProcessingOptions(
                settings.ProcessMaxWidth,
                settings.MotionArea,
                settings.ColorMinPixels,
                settings.MorphKernelSize);

            foreach (var videoPath in camera.VideoPaths)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _engine.PreBakeBackgroundAsync(videoPath, settings.SampleCount, options, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore cancellation during stop.
                    }
                    catch
                    {
                        // Ignore bake errors in background threads.
                    }
                }, cancellationToken);
            }
        }
    }

    private void QueuePreviewFrame(BackgroundEstimationEngine.PreviewFrameSet frameSet, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastPreviewRenderTick);
        if (now - last < PreviewIntervalMs)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _previewRenderBusy, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() => RenderFrameSet(frameSet));
                Interlocked.Exchange(ref _lastPreviewRenderTick, Environment.TickCount64);
            }
            catch
            {
                // Ignore preview-render errors.
            }
            finally
            {
                Interlocked.Exchange(ref _previewRenderBusy, 0);
            }
        }, cancellationToken);
    }

    private void RenderFrameSet(BackgroundEstimationEngine.PreviewFrameSet frameSet)
    {
        UpdatePreviewImage(PreviewBackgroundMaskImage, frameSet.BackgroundMaskJpeg);
        UpdatePreviewImage(PreviewMovingColorImage, frameSet.MovingColorJpeg);
        UpdatePreviewImage(PreviewColorDetectionImage, frameSet.ColorDetectionJpeg);
        UpdatePreviewImage(PreviewMotionImage, frameSet.MotionJpeg);
    }

    private static void UpdatePreviewImage(Image target, byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        var bitmap = new Bitmap(ms);

        var previous = target.Source as Bitmap;
        target.Source = bitmap;
        previous?.Dispose();
    }

    private void RefreshCameraUi()
    {
        List<CameraProfile> snapshot;
        lock (_cameraSync)
        {
            snapshot = _cameras.ToList();
        }

        PlaylistListBox.ItemsSource = snapshot.Select(camera => camera.DisplayName).ToList();

        PlaylistCountText.Text = snapshot.Count == 1
            ? "1 camera"
            : $"{snapshot.Count} cameras";

        var canNavigate = snapshot.Count > 1;
        PreviousVideoButton.IsEnabled = canNavigate;
        NextVideoButton.IsEnabled = canNavigate;

        if (snapshot.Count == 0)
        {
            PlaylistListBox.SelectedIndex = -1;
            return;
        }

        _selectedCameraIndex = Math.Clamp(_selectedCameraIndex, 0, snapshot.Count - 1);
        PlaylistListBox.SelectedIndex = _selectedCameraIndex;

        var selected = snapshot[_selectedCameraIndex];
        ApplySettingsToUi(GetSettingsForCamera(selected.Id));
    }

    private void SetRunState(bool isRunning)
    {
        StartStopButton.Content = isRunning ? "Stop" : "Start";
        AddVideosButton.IsEnabled = !isRunning;
        RemoveSelectedButton.IsEnabled = !isRunning;
        ClearPlaylistButton.IsEnabled = !isRunning;
        LoopPlaylistCheckBox.IsEnabled = !isRunning;
        RunStateBadge.Text = isRunning ? "Running" : "Idle";
        RunStateBadge.Foreground = isRunning
            ? Avalonia.Media.Brushes.LightGreen
            : Avalonia.Media.Brushes.LightBlue;

        if (!isRunning)
        {
            Interlocked.Exchange(ref _requestedCameraIndex, -1);
        }
    }

    private void RuntimeSettingControlOnLostFocus(object? sender, RoutedEventArgs e)
    {
        UpdateSelectedCameraSettingsFromUi(logChange: true);
    }

    private void UpdateSelectedCameraSettingsFromUi(bool logChange)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            return;
        }

        var sampleCount = ParseInt(SampleCountTextBox.Text, 20, 5, 200);
        var threshold = ParseInt(ThresholdTextBox.Text, 100, 1, 255);
        var motionArea = ParseInt(MotionAreaTextBox.Text, 220, 20, 100000);
        var colorMinPixels = ParseInt(ColorMinPixelsTextBox.Text, 40, 1, 100000);
        var morphKernelSize = ParseOddInt(MorphKernelSizeTextBox.Text, 3, 1, 31);
        var processMaxWidth = ParseInt(ProcessWidthTextBox.Text, 640, 160, 1920);

        SampleCountTextBox.Text = sampleCount.ToString();
        ThresholdTextBox.Text = threshold.ToString();
        MotionAreaTextBox.Text = motionArea.ToString();
        ColorMinPixelsTextBox.Text = colorMinPixels.ToString();
        MorphKernelSizeTextBox.Text = morphKernelSize.ToString();
        ProcessWidthTextBox.Text = processMaxWidth.ToString();

        lock (_settingsSync)
        {
            _cameraSettings[camera.Value.Id] = new RuntimeProcessingSettings(
                sampleCount,
                threshold,
                motionArea,
                colorMinPixels,
                morphKernelSize,
                processMaxWidth);
        }

        PersistCameraSettings();

        if (logChange)
        {
            SetStatus($"Status: camera {camera.Value.DisplayName} settings updated.");
        }
    }

    private void ApplySettingsToUi(RuntimeProcessingSettings settings)
    {
        SampleCountTextBox.Text = settings.SampleCount.ToString();
        ThresholdTextBox.Text = settings.Threshold.ToString();
        MotionAreaTextBox.Text = settings.MotionArea.ToString();
        ColorMinPixelsTextBox.Text = settings.ColorMinPixels.ToString();
        MorphKernelSizeTextBox.Text = settings.MorphKernelSize.ToString();
        ProcessWidthTextBox.Text = settings.ProcessMaxWidth.ToString();
    }

    private RuntimeProcessingSettings GetSettingsForCamera(string cameraId)
    {
        lock (_settingsSync)
        {
            if (_cameraSettings.TryGetValue(cameraId, out var settings))
            {
                return settings;
            }

            return RuntimeProcessingSettings.Default;
        }
    }

    private BackgroundEstimationEngine.LiveTuning GetLiveTuningForCamera(string cameraId)
    {
        var settings = GetSettingsForCamera(cameraId);
        return new BackgroundEstimationEngine.LiveTuning(
            settings.Threshold,
            settings.MotionArea,
            settings.ColorMinPixels,
            settings.MorphKernelSize);
    }

    private void PersistCameraSettings()
    {
        Dictionary<string, RuntimeProcessingSettings> snapshot;
        lock (_settingsSync)
        {
            snapshot = _cameraSettings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        _cameraSettingsStore.Save(snapshot);
    }

    private CameraProfile? GetSelectedCamera()
    {
        lock (_cameraSync)
        {
            if (_selectedCameraIndex < 0 || _selectedCameraIndex >= _cameras.Count)
            {
                return null;
            }

            return _cameras[_selectedCameraIndex];
        }
    }

    private int GetCameraCount()
    {
        lock (_cameraSync)
        {
            return _cameras.Count;
        }
    }

    private bool TryGetCamera(int index, out CameraProfile camera)
    {
        lock (_cameraSync)
        {
            if (index < 0 || index >= _cameras.Count)
            {
                camera = default;
                return false;
            }

            camera = _cameras[index];
            return true;
        }
    }

    private void NavigateCameraRelative(int delta)
    {
        var count = GetCameraCount();
        if (count == 0)
        {
            return;
        }

        var current = _selectedCameraIndex;
        if (current < 0 || current >= count)
        {
            current = Math.Clamp(PlaylistListBox.SelectedIndex, 0, count - 1);
        }

        var target = current + delta;
        if (target < 0)
        {
            target = count - 1;
        }
        else if (target >= count)
        {
            target = 0;
        }

        _selectedCameraIndex = target;
        PlaylistListBox.SelectedIndex = target;

        if (_runTask is not null)
        {
            Interlocked.Exchange(ref _requestedCameraIndex, target);
            SetStatus($"Status: switching to camera #{target + 1}...");
            return;
        }

        if (TryGetCamera(target, out var camera))
        {
            ApplySettingsToUi(GetSettingsForCamera(camera.Id));
            CurrentVideoText.Text = $"Current camera/video: {camera.DisplayName} / {Path.GetFileName(camera.PrimaryVideoPath)}";
            SetStatus($"Status: selected camera {camera.DisplayName}");
        }
    }

    private bool HasPendingCameraSwitchRequest()
    {
        return Interlocked.CompareExchange(ref _requestedCameraIndex, -1, -1) >= 0;
    }

    private bool TryConsumeCameraSwitchRequest(out int requestedIndex)
    {
        requestedIndex = Interlocked.Exchange(ref _requestedCameraIndex, -1);
        return requestedIndex >= 0;
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        AppendLog(text);
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{timestamp}] {message}";

        _logEntries.Add(line);
        while (_logEntries.Count > MaxLogEntries)
        {
            _logEntries.RemoveAt(0);
        }

        LogListBox.SelectedIndex = _logEntries.Count - 1;
        if (LogListBox.SelectedItem is not null)
        {
            LogListBox.ScrollIntoView(LogListBox.SelectedItem);
        }
    }

    private static int ParseInt(string? text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, out var parsed))
        {
            parsed = fallback;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static int ParseOddInt(string? text, int fallback, int min, int max)
    {
        var parsed = ParseInt(text, fallback, min, max);
        return parsed % 2 == 0 ? parsed + 1 : parsed;
    }

    private static string BuildCameraName(string path, int sequence)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"Camera {sequence}";
        }

        return name;
    }

    private readonly record struct CameraProfile(string Id, string DisplayName, List<string> VideoPaths)
    {
        public string PrimaryVideoPath => VideoPaths.Count > 0 ? VideoPaths[0] : string.Empty;
    }

    internal readonly record struct RuntimeProcessingSettings(
        int SampleCount,
        int Threshold,
        int MotionArea,
        int ColorMinPixels,
        int MorphKernelSize,
        int ProcessMaxWidth)
    {
        public static RuntimeProcessingSettings Default => new(20, 100, 220, 40, 3, 640);
    }
}
