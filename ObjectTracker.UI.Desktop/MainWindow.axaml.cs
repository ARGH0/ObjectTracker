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
using FluentAvalonia.UI.Windowing;
using ObjectTracker.Vision;
using ObjectTracker.Vision.Source;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : AppWindow
{
    private const int MaxLogEntries = 300;
    private const int PreviewIntervalMs = 33;
    private const int MaxUsbCameraProbeIndex = 5;

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
    private volatile bool _isYoloViewVisible;

    public MainWindow()
    {
        InitializeComponent();

        if (OperatingSystem.IsWindows())
        {
            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.Height = 40;
        }

        LogListBox.ItemsSource = _logEntries;

        foreach (var (cameraId, settings) in _cameraSettingsStore.Load())
        {
            _cameraSettings[cameraId] = settings;
        }

        var yoloModelPath = DetectorRegistry.FindYoloModel();
        if (_engine.ConfigureYoloModel(yoloModelPath, out var yoloStatus))
        {
            AppendLog($"YOLO initialized: {Path.GetFileName(yoloModelPath)}");
            YoloModelStatusText.Text = $"Model: {Path.GetFileName(yoloModelPath)}";
        }
        else
        {
            AppendLog($"YOLO unavailable: {yoloStatus}");
            YoloModelStatusText.Text = $"Model unavailable: {yoloStatus}";
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
        BakeSourceComboBox.SelectionChanged += BakeSourceComboBoxOnSelectionChanged;
        SelectBakeImageButton.Click += SelectBakeImageButtonOnClick;
        ClearBakeImageButton.Click += ClearBakeImageButtonOnClick;

        SampleCountTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ThresholdTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MotionAreaTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ColorMinPixelsTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        MorphKernelSizeTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        ProcessWidthTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        DetectionConfidenceTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        NmsIouThresholdTextBox.LostFocus += RuntimeSettingControlOnLostFocus;
        DetectionImgSizeComboBox.SelectionChanged += RuntimeSettingControlOnLostFocus;
        DetectorModeComboBox.SelectionChanged += RuntimeSettingControlOnLostFocus;
        DetectorModeComboBox.SelectionChanged += (_, _) => SyncDetectorView();
    }

    private async void AddCamerasButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var selection = await new CameraSourceChoiceDialog().ShowDialog<CameraAddChoice?>(this);
        if (selection is null)
        {
            return;
        }

        switch (selection.Value)
        {
            case CameraAddChoice.VideoFiles:
                await AddVideoCamerasAsync();
                break;
            case CameraAddChoice.UsbCamera:
                await AddUsbCameraAsync();
                break;
        }
    }

    private async Task AddVideoCamerasAsync()
    {
        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select camera video files",
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
                if (string.IsNullOrWhiteSpace(path) || _cameras.Any(c => string.Equals(c.Id, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var cameraId = path;
                var displayName = BuildCameraName(path, _cameras.Count + 1);
                _cameras.Add(CameraProfile.CreateVideo(cameraId, displayName, new List<string> { path }));

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
        SetStatus(added == 0
            ? "Status: no new video cameras added."
            : $"Status: added {added} video camera(s).");

        if (_runTask is not null)
        {
            StartBakeForAllCameras(_runCts?.Token ?? CancellationToken.None);
        }
    }

    private async Task AddUsbCameraAsync()
    {
        SetStatus("Status: scanning USB cameras...");
        var usbOptions = await Task.Run(() => OpenCvUsbCameraDiscovery.DiscoverOptions(MaxUsbCameraProbeIndex));
        if (usbOptions.Count == 0)
        {
            SetStatus("Status: no USB cameras detected.");
            return;
        }

        var selectedOption = await new UsbCameraSelectionDialog(usbOptions).ShowDialog<UsbCameraOption?>(this);
        if (selectedOption is null)
        {
            SetStatus("Status: USB camera selection cancelled.");
            return;
        }

        var option = selectedOption.Value;
        var added = false;

        lock (_cameraSync)
        {
            if (!_cameras.Any(camera => string.Equals(camera.Id, option.Id, StringComparison.OrdinalIgnoreCase)))
            {
                _cameras.Add(CameraProfile.CreateUsb(option.Id, option.DisplayName, option.CameraIndex, option.ApiId));

                if (!_cameraSettings.ContainsKey(option.Id))
                {
                    _cameraSettings[option.Id] = RuntimeProcessingSettings.Default;
                }

                if (_selectedCameraIndex < 0)
                {
                    _selectedCameraIndex = 0;
                }

                added = true;
            }
        }

        if (!added)
        {
            SetStatus($"Status: {option.DisplayName} is already added.");
            return;
        }

        PersistCameraSettings();
        RefreshCameraUi();
        SetStatus($"Status: added {option.DisplayName}.");
    }

    private void BakeSourceComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyBakeSourceUiState();
        UpdateSelectedCameraSettingsFromUi(logChange: false);
    }

    private async void SelectBakeImageButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        if (StorageProvider is null)
        {
            SetStatus("Status: file picker is not available in this runtime.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select bake image",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tif", "*.tiff", "*.webp" } }
            }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        BakeSourceComboBox.SelectedIndex = (int)BakeSourceMode.ImageFile;
        BakeImagePathTextBox.Text = path;
        ApplyBakeSourceUiState();
        UpdateSelectedCameraSettingsFromUi(logChange: false);
        SetStatus($"Status: bake image selected for {camera.Value.DisplayName}.");
    }

    private void ClearBakeImageButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        BakeImagePathTextBox.Text = string.Empty;
        ApplyBakeSourceUiState();
        UpdateSelectedCameraSettingsFromUi(logChange: false);
        SetStatus($"Status: cleared bake image for {camera.Value.DisplayName}.");
    }

    private void RemoveCameraButtonOnClick(object? sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            ReportException("Status: remove camera failed", ex);
        }
    }

    private void ClearCamerasButtonOnClick(object? sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            ReportException("Status: clear cameras failed", ex);
        }
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
            CurrentVideoText.Text = BuildCurrentSourceText(camera.Value);
            OpenBakedMaskButton.IsEnabled = camera.Value.CanOpenBakedMask;
        }

        if (_runTask is not null && index >= 0)
        {
            Interlocked.Exchange(ref _requestedCameraIndex, index);
            SetStatus($"Status: switching to camera {camera?.DisplayName}...");
        }
    }

    private async void StartStopButtonOnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_runTask is not null)
            {
                await StopProcessingAsync();
                return;
            }

            if (GetCameraCount() == 0)
            {
                SetStatus("Status: add at least one camera.");
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
                ReportException("Status: processing failed", ex);
            }
        }
        catch (Exception ex)
        {
            ReportException("Status: start/stop failed", ex);
        }
        finally
        {
            _runTask = null;
            _runCts?.Dispose();
            _runCts = null;
            SetRunState(isRunning: false);
        }
    }

    private async void OpenBakedMaskButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            SetStatus("Status: select a camera first.");
            return;
        }

        if (camera.Value.IsUsbCamera)
        {
            SetStatus("Status: baked masks are only available for video cameras.");
            return;
        }

        UpdateSelectedCameraSettingsFromUi(logChange: false);
        var settings = GetSettingsForCamera(camera.Value.Id);
        var options = new BackgroundEstimationEngine.ProcessingOptions(
            settings.ProcessMaxWidth,
            settings.MotionArea,
            settings.ColorMinPixels,
            settings.MorphKernelSize);

        string? bakedPath;
        try
        {
            bakedPath = await _engine.EnsureBakedBackgroundAsync(
                camera.Value.PrimaryVideoPath,
                settings.SampleCount,
                options,
                GetBakeImagePath(settings),
                CancellationToken.None,
                async message => await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {message}")));
        }
        catch (Exception ex)
        {
            ReportException("Status: failed to prepare baked mask", ex);
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
            ReportException("Status: failed to open baked mask", ex);
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
                CurrentVideoText.Text = BuildCurrentSourceText(camera);
                OpenBakedMaskButton.IsEnabled = camera.CanOpenBakedMask;
            });

            await ProcessCameraAsync(camera, loopCameraVideos, cancellationToken);

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

    private async Task ProcessCameraAsync(CameraProfile camera, bool loopCameraVideos, CancellationToken cancellationToken)
    {
        if (camera.IsUsbCamera && camera.UsbCamera is { } usbCamera)
        {
            var settings = GetSettingsForCamera(camera.Id);
            var options = new BackgroundEstimationEngine.ProcessingOptions(
                settings.ProcessMaxWidth,
                settings.MotionArea,
                settings.ColorMinPixels,
                settings.MorphKernelSize);
            var detectionOptions = new BackgroundEstimationEngine.DetectionOptions(
                settings.DetectionMethod == DetectionMethod.Yolo,
                settings.DetectionConfidence,
                settings.NmsIouThreshold,
                settings.DetectionImgSize);

            var result = await _engine.ProcessUsbCameraAsync(
                usbCamera.CameraIndex,
                usbCamera.ApiId,
                camera.DisplayName,
                settings.SampleCount,
                settings.Threshold,
                options,
                detectionOptions,
                GetBakeImagePath(settings),
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

            return;
        }

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
            var detectionOptions = new BackgroundEstimationEngine.DetectionOptions(
                settings.DetectionMethod == DetectionMethod.Yolo,
                settings.DetectionConfidence,
                settings.NmsIouThreshold,
                settings.DetectionImgSize);

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
                CurrentVideoText.Text = BuildCurrentSourceText(camera, Path.GetFileName(videoPath));
            });

            var result = await _engine.ProcessVideoAsync(
                videoPath,
                settings.SampleCount,
                settings.Threshold,
                options,
                detectionOptions,
                GetBakeImagePath(settings),
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
            if (camera.IsUsbCamera)
            {
                continue;
            }

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
                        await _engine.EnsureBakedBackgroundAsync(
                            videoPath,
                            settings.SampleCount,
                            options,
                            GetBakeImagePath(settings),
                            cancellationToken);
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

                // Performance optimization: Pre-decode bitmaps on thread pool before dispatching to UI thread.
                // This avoids expensive Bitmap creation blocking the UI thread.
                var bitmaps = new Dictionary<Image, Bitmap>();
                
                bitmaps[PreviewBackgroundMaskImage] = CreateBitmapFromJpeg(frameSet.BackgroundMaskJpeg);
                bitmaps[PreviewMovingColorImage] = CreateBitmapFromJpeg(frameSet.MovingColorJpeg);
                bitmaps[PreviewColorDetectionImage] = CreateBitmapFromJpeg(frameSet.ColorDetectionJpeg);
                bitmaps[PreviewMotionImage] = CreateBitmapFromJpeg(frameSet.MotionJpeg);

                if (_isYoloViewVisible)
                {
                    bitmaps[PreviewYoloDetectionImage] = CreateBitmapFromJpeg(frameSet.ColorDetectionJpeg);
                    bitmaps[PreviewYoloMaskImage] = CreateBitmapFromJpeg(frameSet.BackgroundMaskJpeg);
                }

                // Dispatch only the UI update work to the UI thread
                await Dispatcher.UIThread.InvokeAsync(() => ApplyBitmapsToImages(bitmaps));
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

        if (YoloViewPanel.IsVisible)
        {
            UpdatePreviewImage(PreviewYoloDetectionImage, frameSet.ColorDetectionJpeg);
            UpdatePreviewImage(PreviewYoloMaskImage, frameSet.BackgroundMaskJpeg);
        }
    }

    /// <summary>
    /// Creates a Bitmap from JPEG bytes. Called on thread pool thread for performance.
    /// NOTE: MemoryStream is kept alive to prevent Avalonia's lazy bitmap loading from failing.
    /// </summary>
    private static Bitmap CreateBitmapFromJpeg(byte[] jpegBytes)
    {
        // Create a MemoryStream that owns the buffer and won't be disposed
        // Avalonia's Bitmap may lazy-load the image, so we must keep the stream alive
        var ms = new MemoryStream(jpegBytes, writable: false);
        try
        {
            return new Bitmap(ms);
        }
        catch
        {
            ms?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Applies pre-created bitmaps to Image controls. Called on UI thread.
    /// </summary>
    private void ApplyBitmapsToImages(Dictionary<Image, Bitmap> bitmaps)
    {
        try
        {
            foreach (var (target, bitmap) in bitmaps)
            {
                // Ensure the control is still in the visual tree before updating
                if (target is null || !target.IsVisible)
                {
                    bitmap?.Dispose();
                    continue;
                }

                try
                {
                    var previous = target.Source as Bitmap;
                    target.Source = bitmap;
                    previous?.Dispose();
                }
                catch
                {
                    // If updating this specific image fails, dispose the new bitmap and continue
                    bitmap?.Dispose();
                }
            }
        }
        catch
        {
            // If the entire batch fails, dispose all bitmaps
            foreach (var bitmap in bitmaps.Values)
            {
                bitmap?.Dispose();
            }
        }
    }

    private void SyncDetectorView()
    {
        var isYolo = DetectorModeComboBox.SelectedIndex == 1;
        MotionViewPanel.IsVisible = !isYolo;
        YoloViewPanel.IsVisible = isYolo;
        _isYoloViewVisible = isYolo;

        if (isYolo)
        {
            UpdateYoloSettingsSummary();
        }
    }

    private void UpdateYoloSettingsSummary()
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            YoloSettingsSummaryText.Text = "No camera selected.";
            return;
        }

        var s = GetSettingsForCamera(camera.Value.Id);
        YoloSettingsSummaryText.Text =
            $"Confidence: {s.DetectionConfidence}%  \u00b7  NMS IoU: {s.NmsIouThreshold}%  \u00b7  Input size: {s.DetectionImgSize}px";
    }

    private static void UpdatePreviewImage(Image target, byte[] imageBytes)
    {
        try
        {
            // Create MemoryStream without disposing it - Avalonia may lazy-load the bitmap
            var ms = new MemoryStream(imageBytes, writable: false);
            var bitmap = new Bitmap(ms);

            var previous = target.Source as Bitmap;
            target.Source = bitmap;
            previous?.Dispose();
        }
        catch
        {
            // Ignore individual preview update errors to prevent UI blocking
        }
    }

    private void RefreshCameraUi()
    {
        RunOnUiThread(() =>
        {
            List<CameraProfile> snapshot;
            lock (_cameraSync)
            {
                snapshot = _cameras.ToList();
            }

            PlaylistListBox.ItemsSource = snapshot.Select(camera => camera.DisplayName).ToList();

            var canNavigate = snapshot.Count > 1;
            PreviousVideoButton.IsEnabled = canNavigate;
            NextVideoButton.IsEnabled = canNavigate;

            if (snapshot.Count == 0)
            {
                PlaylistListBox.SelectedIndex = -1;
                CurrentVideoText.Text = "Current camera/source: -";
                OpenBakedMaskButton.IsEnabled = false;
                return;
            }

            _selectedCameraIndex = Math.Clamp(_selectedCameraIndex, 0, snapshot.Count - 1);
            PlaylistListBox.SelectedIndex = _selectedCameraIndex;

            var selected = snapshot[_selectedCameraIndex];
            ApplySettingsToUi(GetSettingsForCamera(selected.Id));
            CurrentVideoText.Text = BuildCurrentSourceText(selected);
            OpenBakedMaskButton.IsEnabled = selected.CanOpenBakedMask;
        });
    }

    private void SetRunState(bool isRunning)
    {
        RunOnUiThread(() =>
        {
            StartStopButton.Content = isRunning ? "Stop" : "Start";
            AddVideosButton.IsEnabled = !isRunning;
            RemoveSelectedButton.IsEnabled = !isRunning;
            ClearPlaylistButton.IsEnabled = !isRunning;
            LoopPlaylistCheckBox.IsEnabled = !isRunning;

            if (!isRunning)
            {
                Interlocked.Exchange(ref _requestedCameraIndex, -1);
            }
        });
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
        var detectionConfidence = ParseInt(DetectionConfidenceTextBox.Text, 50, 1, 99);
        var nmsIouThreshold = ParseInt(NmsIouThresholdTextBox.Text, 45, 1, 99);
        var detectionImgSize = ParseDetectionImgSize();
        var bakeSourceMode = ParseBakeSourceMode();
        var bakeImagePath = (BakeImagePathTextBox.Text ?? string.Empty).Trim();
        var detectionMethod = (DetectionMethod)ParseInt(DetectorModeComboBox.SelectedIndex.ToString(), 0, 0, 1);

        SampleCountTextBox.Text = sampleCount.ToString();
        ThresholdTextBox.Text = threshold.ToString();
        MotionAreaTextBox.Text = motionArea.ToString();
        ColorMinPixelsTextBox.Text = colorMinPixels.ToString();
        MorphKernelSizeTextBox.Text = morphKernelSize.ToString();
        ProcessWidthTextBox.Text = processMaxWidth.ToString();
        DetectionConfidenceTextBox.Text = detectionConfidence.ToString();
        NmsIouThresholdTextBox.Text = nmsIouThreshold.ToString();
        BakeImagePathTextBox.Text = bakeImagePath;

        lock (_settingsSync)
        {
            _cameraSettings[camera.Value.Id] = new RuntimeProcessingSettings(
                sampleCount,
                threshold,
                motionArea,
                colorMinPixels,
                morphKernelSize,
                processMaxWidth,
                bakeSourceMode,
                bakeImagePath,
                detectionConfidence,
                nmsIouThreshold,
                detectionImgSize,
                detectionMethod);
        }

        PersistCameraSettings();
        UpdateOpenBakedMaskButtonState(camera.Value, _cameraSettings[camera.Value.Id]);

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
        DetectionConfidenceTextBox.Text = settings.DetectionConfidence.ToString();
        NmsIouThresholdTextBox.Text = settings.NmsIouThreshold.ToString();
        DetectionImgSizeComboBox.SelectedIndex = settings.DetectionImgSize switch
        {
            320 => 0,
            1280 => 2,
            _ => 1 // 640 default
        };
        DetectorModeComboBox.SelectedIndex = (int)settings.DetectionMethod;
        BakeSourceComboBox.SelectedIndex = (int)settings.BakeSourceMode;
        BakeImagePathTextBox.Text = settings.BakeImagePath;
        ApplyBakeSourceUiState();
        SyncDetectorView();
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
            CurrentVideoText.Text = BuildCurrentSourceText(camera);
            OpenBakedMaskButton.IsEnabled = camera.CanOpenBakedMask;
            SetStatus($"Status: selected camera {camera.DisplayName}");
        }
    }

    private static string BuildCurrentSourceText(CameraProfile camera, string? activeSourceLabel = null)
    {
        var sourceLabel = string.IsNullOrWhiteSpace(activeSourceLabel)
            ? camera.CurrentSourceLabel
            : activeSourceLabel;

        return $"Current camera/source: {camera.DisplayName} / {sourceLabel}";
    }

    private void ApplyBakeSourceUiState()
    {
        var bakeSourceMode = ParseBakeSourceMode();
        var usesImage = bakeSourceMode == BakeSourceMode.ImageFile;

        SampleCountTextBox.IsEnabled = !usesImage;
        SelectBakeImageButton.IsEnabled = usesImage;
        BakeImagePathTextBox.IsEnabled = usesImage;
        ClearBakeImageButton.IsEnabled = usesImage && !string.IsNullOrWhiteSpace(BakeImagePathTextBox.Text);
    }

    private BakeSourceMode ParseBakeSourceMode()
    {
        return BakeSourceComboBox.SelectedIndex == (int)BakeSourceMode.ImageFile
            ? BakeSourceMode.ImageFile
            : BakeSourceMode.Samples;
    }

    private static string? GetBakeImagePath(RuntimeProcessingSettings settings)
    {
        if (settings.BakeSourceMode != BakeSourceMode.ImageFile || string.IsNullOrWhiteSpace(settings.BakeImagePath))
        {
            return null;
        }

        return settings.BakeImagePath;
    }

    private void UpdateOpenBakedMaskButtonState(CameraProfile camera, RuntimeProcessingSettings settings)
    {
        OpenBakedMaskButton.IsEnabled = !camera.IsUsbCamera
            && (settings.BakeSourceMode == BakeSourceMode.Samples || !string.IsNullOrWhiteSpace(settings.BakeImagePath));
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
        RunOnUiThread(() =>
        {
            StatusText.Text = text;
            AppendLog(text);
        });
    }

    private void ReportException(string context, Exception ex)
    {
        var baseException = ex.GetBaseException();
        var statusText = $"{context} - {baseException.GetType().Name}: {baseException.Message}";
        var details = $"{context}\n{ex}";

        SetStatus(statusText);
        AppendLog(details);
        Debug.WriteLine(details);
    }

    private void AppendLog(string message)
    {
        RunOnUiThread(() =>
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
        });
    }

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
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

    private int ParseDetectionImgSize()
    {
        return DetectionImgSizeComboBox.SelectedIndex switch
        {
            0 => 320,
            2 => 1280,
            _ => 640
        };
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

    private enum CameraSourceKind
    {
        VideoFiles,
        UsbCamera
    }

    internal enum BakeSourceMode
    {
        Samples = 0,
        ImageFile = 1
    }

    private readonly record struct UsbCameraSource(int CameraIndex, string ApiId);

    private readonly record struct CameraProfile(
        string Id,
        string DisplayName,
        CameraSourceKind SourceKind,
        List<string> VideoPaths,
        UsbCameraSource? UsbCamera)
    {
        public string PrimaryVideoPath => VideoPaths.Count > 0 ? VideoPaths[0] : string.Empty;

        public bool IsUsbCamera => SourceKind == CameraSourceKind.UsbCamera && UsbCamera is not null;

        public bool CanOpenBakedMask => SourceKind == CameraSourceKind.VideoFiles && !string.IsNullOrWhiteSpace(PrimaryVideoPath);

        public string CurrentSourceLabel => IsUsbCamera && UsbCamera is { } usbCamera
            ? $"USB camera {usbCamera.CameraIndex}"
            : (string.IsNullOrWhiteSpace(PrimaryVideoPath) ? DisplayName : Path.GetFileName(PrimaryVideoPath));

        public static CameraProfile CreateVideo(string id, string displayName, List<string> videoPaths)
            => new(id, displayName, CameraSourceKind.VideoFiles, videoPaths, null);

        public static CameraProfile CreateUsb(string id, string displayName, int cameraIndex, string apiId)
            => new(id, displayName, CameraSourceKind.UsbCamera, new List<string>(), new UsbCameraSource(cameraIndex, apiId));
    }

    internal enum DetectionMethod
    {
        MotionColor = 0,
        Yolo = 1
    }

    internal readonly record struct RuntimeProcessingSettings(
        int SampleCount,
        int Threshold,
        int MotionArea,
        int ColorMinPixels,
        int MorphKernelSize,
        int ProcessMaxWidth,
        BakeSourceMode BakeSourceMode,
        string BakeImagePath,
        int DetectionConfidence,
        int NmsIouThreshold,
        int DetectionImgSize,
        DetectionMethod DetectionMethod)
    {
        public static RuntimeProcessingSettings Default => new(20, 100, 220, 40, 3, 640, BakeSourceMode.Samples, string.Empty, 50, 45, 640, DetectionMethod.MotionColor);
    }
}
