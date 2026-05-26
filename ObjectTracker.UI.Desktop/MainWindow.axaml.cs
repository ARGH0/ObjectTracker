using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Windowing;
using ObjectTracker.Core.Domain;
using VideoCapture = OpenCvSharp.VideoCapture;
using VideoCaptureAPIs = OpenCvSharp.VideoCaptureAPIs;
using VideoCaptureProperties = OpenCvSharp.VideoCaptureProperties;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : AppWindow
{
    private const int MaxLogEntries = 300;
    private const int PreviewIntervalMs = 33;
    private const int MaxUsbCameraProbeIndex = 5;

    private readonly Lock cameraSync = new ();
    private readonly Lock settingsSync = new ();
    private readonly List<CameraProfile> cameras = new ();
    private readonly Dictionary<string, RuntimeProcessingSettings> cameraSettings = new (StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<string> logEntries = new ();

    private readonly BackgroundEstimationEngine engine = new ();
    private readonly CameraSettingsStore cameraSettingsStore = new ();

    private CancellationTokenSource? runCts;
    private Task? runTask;
    private long lastPreviewRenderTick;
    private int previewRenderBusy;
    private int selectedCameraIndex = -1;
    private int requestedCameraIndex = -1;
    private string selectedCalibrationColor = "red";

    public MainWindow()
    {
        InitializeComponent();

        if (OperatingSystem.IsWindows())
        {
            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.Height = 40;
        }

        LogListBox.ItemsSource = logEntries;

        foreach (var (cameraId, settings) in cameraSettingsStore.Load())
        {
            cameraSettings[cameraId] = settings;
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

        CalibrationColorComboBox.SelectionChanged += CalibrationColorComboBoxOnSelectionChanged;
        HueLowerTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        HueUpperTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        SaturationLowerTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        SaturationUpperTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        ValueLowerTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
        ValueUpperTextBox.LostFocus += ColorCalibrationControlOnLostFocus;
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
                new ("Video files") { Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.m4v" } }
            }
        });

        var added = 0;
        lock (cameraSync)
        {
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path) || cameras.Any(c => string.Equals(c.Id, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var cameraId = path;
                var displayName = BuildCameraName(path, cameras.Count + 1);
                cameras.Add(CameraProfile.CreateVideo(cameraId, displayName, new List<string> { path }));

                if (!cameraSettings.ContainsKey(cameraId))
                {
                    cameraSettings[cameraId] = RuntimeProcessingSettings.Default;
                }

                added++;
            }

            if (selectedCameraIndex < 0 && cameras.Count > 0)
            {
                selectedCameraIndex = 0;
            }
        }

        PersistCameraSettings();
        RefreshCameraUi();
        SetStatus(added == 0
            ? "Status: no new video cameras added."
            : $"Status: added {added} video camera(s).");

        if (runTask is not null)
        {
            StartBakeForAllCameras(runCts?.Token ?? CancellationToken.None);
        }
    }

    private async Task AddUsbCameraAsync()
    {
        SetStatus("Status: scanning USB cameras...");
        var usbOptions = await Task.Run(DiscoverUsbCameraOptions);
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

        lock (cameraSync)
        {
            if (!cameras.Any(camera => string.Equals(camera.Id, option.Id, StringComparison.OrdinalIgnoreCase)))
            {
                cameras.Add(CameraProfile.CreateUsb(option.Id, option.DisplayName, option.CameraIndex, option.Api));

                if (!cameraSettings.ContainsKey(option.Id))
                {
                    cameraSettings[option.Id] = RuntimeProcessingSettings.Default;
                }

                if (selectedCameraIndex < 0)
                {
                    selectedCameraIndex = 0;
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
                new ("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tif", "*.tiff", "*.webp" } }
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
        CameraProfile? removed = null;

        lock (cameraSync)
        {
            var index = PlaylistListBox.SelectedIndex;
            if (index < 0 || index >= cameras.Count)
            {
                return;
            }

            removed = cameras[index];
            cameras.RemoveAt(index);
            cameraSettings.Remove(removed.Value.Id);

            if (cameras.Count == 0)
            {
                selectedCameraIndex = -1;
            }
            else
            {
                selectedCameraIndex = Math.Clamp(index, 0, cameras.Count - 1);
            }
        }

        PersistCameraSettings();
        RefreshCameraUi();

        if (runTask is not null && selectedCameraIndex >= 0)
        {
            Interlocked.Exchange(ref requestedCameraIndex, selectedCameraIndex);
        }

        SetStatus($"Status: removed camera {removed?.DisplayName ?? "-"}.");
    }

    private void ClearCamerasButtonOnClick(object? sender, RoutedEventArgs e)
    {
        lock (cameraSync)
        {
            cameras.Clear();
            cameraSettings.Clear();
            selectedCameraIndex = -1;
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

        lock (cameraSync)
        {
            if (index < 0 || index >= cameras.Count)
            {
                return;
            }

            selectedCameraIndex = index;
            camera = cameras[index];
        }

        if (camera is not null)
        {
            ApplySettingsToUi(GetSettingsForCamera(camera.Value.Id));
            CurrentVideoText.Text = BuildCurrentSourceText(camera.Value);
            OpenBakedMaskButton.IsEnabled = camera.Value.CanOpenBakedMask;
        }

        if (runTask is not null && index >= 0)
        {
            Interlocked.Exchange(ref requestedCameraIndex, index);
            SetStatus($"Status: switching to camera {camera?.DisplayName}...");
        }
    }

    private async void StartStopButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (runTask is not null)
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

        var startIndex = selectedCameraIndex >= 0 ? selectedCameraIndex : 0;
        var loopCameraVideos = LoopPlaylistCheckBox.IsChecked == true;

        runCts = new CancellationTokenSource();
        var token = runCts.Token;

        SetRunState(isRunning: true);
        StartBakeForAllCameras(token);
        runTask = Task.Run(() => RunCameraSelectionAsync(startIndex, loopCameraVideos, token), token);

        try
        {
            await runTask;
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
            runTask = null;
            runCts?.Dispose();
            runCts = null;
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
            settings.MorphKernelSize,
            settings.ColorCalibrations);

        string? bakedPath;
        try
        {
            bakedPath = await engine.EnsureBakedBackgroundAsync(
                camera.Value.PrimaryVideoPath,
                settings.SampleCount,
                options,
                GetBakeImagePath(settings),
                CancellationToken.None,
                async message => await Dispatcher.UIThread.InvokeAsync(() => SetStatus($"Status: {message}")));
        }
        catch (Exception ex)
        {
            SetStatus($"Status: failed to prepare baked mask - {ex.Message}");
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
        var task = runTask;
        if (task is null)
        {
            return;
        }

        runCts?.Cancel();

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

            selectedCameraIndex = cameraIndex;
            Interlocked.Exchange(ref selectedCameraIndex, cameraIndex);

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
            cameraIndex = Math.Clamp(selectedCameraIndex, 0, Math.Max(0, GetCameraCount() - 1));
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
                settings.MorphKernelSize,
                settings.ColorCalibrations);

            var result = await engine.ProcessUsbCameraAsync(
                usbCamera.CameraIndex,
                usbCamera.Api,
                camera.DisplayName,
                settings.SampleCount,
                settings.Threshold,
                options,
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

        for (var videoIndex = 0; !cancellationToken.IsCancellationRequested; videoIndex++)
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
                settings.MorphKernelSize,
                settings.ColorCalibrations);

            if (videoIndex >= camera.VideoPaths.Count)
            {
                if (!loopCameraVideos)
                {
                    break;
                }

                videoIndex = 0;
            }

            var videoPath = camera.VideoPaths[videoIndex];
            await Dispatcher.UIThread.InvokeAsync(() => CurrentVideoText.Text = BuildCurrentSourceText(camera, Path.GetFileName(videoPath)));

            var result = await engine.ProcessVideoAsync(
                videoPath,
                settings.SampleCount,
                settings.Threshold,
                options,
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
        }
    }

    private void StartBakeForAllCameras(CancellationToken cancellationToken)
    {
        List<CameraProfile> snapshot;
        lock (cameraSync)
        {
            snapshot = cameras.ToList();
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
                settings.MorphKernelSize,
                settings.ColorCalibrations);

            foreach (var videoPath in camera.VideoPaths)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await engine.EnsureBakedBackgroundAsync(
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
        var last = Interlocked.Read(ref lastPreviewRenderTick);
        if (now - last < PreviewIntervalMs)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref previewRenderBusy, 1, 0) != 0)
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
                Interlocked.Exchange(ref lastPreviewRenderTick, Environment.TickCount64);
            }
            catch
            {
                // Ignore preview-render errors.
            }
            finally
            {
                Interlocked.Exchange(ref previewRenderBusy, 0);
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
        lock (cameraSync)
        {
            snapshot = cameras.ToList();
        }

        PlaylistListBox.ItemsSource = snapshot.ConvertAll(camera => camera.DisplayName);

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

        selectedCameraIndex = Math.Clamp(selectedCameraIndex, 0, snapshot.Count - 1);
        PlaylistListBox.SelectedIndex = selectedCameraIndex;

        var selected = snapshot[selectedCameraIndex];
        ApplySettingsToUi(GetSettingsForCamera(selected.Id));
        CurrentVideoText.Text = BuildCurrentSourceText(selected);
        OpenBakedMaskButton.IsEnabled = selected.CanOpenBakedMask;
    }

    private void SetRunState(bool isRunning)
    {
        StartStopButton.Content = isRunning ? "Stop" : "Start";
        AddVideosButton.IsEnabled = !isRunning;
        RemoveSelectedButton.IsEnabled = !isRunning;
        ClearPlaylistButton.IsEnabled = !isRunning;
        LoopPlaylistCheckBox.IsEnabled = !isRunning;

        if (!isRunning)
        {
            Interlocked.Exchange(ref requestedCameraIndex, -1);
        }
    }

    private void RuntimeSettingControlOnLostFocus(object? sender, RoutedEventArgs e)
    {
        UpdateSelectedCameraSettingsFromUi(logChange: true);
    }

    private void CalibrationColorComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedName = GetCalibrationSelectionName();
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            return;
        }

        selectedCalibrationColor = selectedName;

        var camera = GetSelectedCamera();
        var settings = camera is null
            ? RuntimeProcessingSettings.Default
            : GetSettingsForCamera(camera.Value.Id);

        LoadCalibrationEditor(settings.ColorCalibrations, selectedName);
    }

    private void ColorCalibrationControlOnLostFocus(object? sender, RoutedEventArgs e)
    {
        UpdateSelectedColorCalibrationFromUi(logChange: true);
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
        var bakeSourceMode = ParseBakeSourceMode();
        var bakeImagePath = (BakeImagePathTextBox.Text ?? string.Empty).Trim();

        SampleCountTextBox.Text = sampleCount.ToString();
        ThresholdTextBox.Text = threshold.ToString();
        MotionAreaTextBox.Text = motionArea.ToString();
        ColorMinPixelsTextBox.Text = colorMinPixels.ToString();
        MorphKernelSizeTextBox.Text = morphKernelSize.ToString();
        ProcessWidthTextBox.Text = processMaxWidth.ToString();
        BakeImagePathTextBox.Text = bakeImagePath;

        var existing = GetSettingsForCamera(camera.Value.Id);
        var colorCalibrations = BuildCalibrationsFromEditor(existing.ColorCalibrations);

        lock (settingsSync)
        {
            cameraSettings[camera.Value.Id] = new RuntimeProcessingSettings(
                sampleCount,
                threshold,
                motionArea,
                colorMinPixels,
                morphKernelSize,
                processMaxWidth,
                bakeSourceMode,
                bakeImagePath,
                colorCalibrations);
        }

        PersistCameraSettings();
        UpdateOpenBakedMaskButtonState(camera.Value, cameraSettings[camera.Value.Id]);

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
        BakeSourceComboBox.SelectedIndex = (int)settings.BakeSourceMode;
        BakeImagePathTextBox.Text = settings.BakeImagePath;
        ApplyBakeSourceUiState();

        var selectedColor = GetCalibrationSelectionName() ?? selectedCalibrationColor;
        selectedCalibrationColor = selectedColor;
        LoadCalibrationEditor(settings.ColorCalibrations, selectedColor);
    }

    private RuntimeProcessingSettings GetSettingsForCamera(string cameraId)
    {
        lock (settingsSync)
        {
            if (cameraSettings.TryGetValue(cameraId, out var settings))
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
            settings.MorphKernelSize,
            settings.ColorCalibrations);
    }

    private void PersistCameraSettings()
    {
        Dictionary<string, RuntimeProcessingSettings> snapshot;
        lock (settingsSync)
        {
            snapshot = cameraSettings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        cameraSettingsStore.Save(snapshot);
    }

    private CameraProfile? GetSelectedCamera()
    {
        lock (cameraSync)
        {
            if (selectedCameraIndex < 0 || selectedCameraIndex >= cameras.Count)
            {
                return null;
            }

            return cameras[selectedCameraIndex];
        }
    }

    private int GetCameraCount()
    {
        lock (cameraSync)
        {
            return cameras.Count;
        }
    }

    private bool TryGetCamera(int index, out CameraProfile camera)
    {
        lock (cameraSync)
        {
            if (index < 0 || index >= cameras.Count)
            {
                camera = default;
                return false;
            }

            camera = cameras[index];
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

        var current = selectedCameraIndex;
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

        selectedCameraIndex = target;
        PlaylistListBox.SelectedIndex = target;

        if (runTask is not null)
        {
            Interlocked.Exchange(ref requestedCameraIndex, target);
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

    private static IReadOnlyList<UsbCameraOption> DiscoverUsbCameraOptions()
    {
        var api = GetDefaultUsbCaptureApi();
        var options = new List<UsbCameraOption>();

        for (var cameraIndex = 0; cameraIndex <= MaxUsbCameraProbeIndex; cameraIndex++)
        {
            using var capture = new VideoCapture(cameraIndex, api);
            capture.Set(VideoCaptureProperties.BufferSize, 1);
            if (!capture.IsOpened())
            {
                continue;
            }

            var width = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameWidth));
            var height = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameHeight));
            var sizeSuffix = width > 0 && height > 0
                ? $" ({width}x{height})"
                : string.Empty;

            options.Add(new UsbCameraOption(
                $"usb:{cameraIndex}:{api.ToString().ToLowerInvariant()}",
                $"USB camera {cameraIndex}{sizeSuffix}",
                cameraIndex,
                api));
        }

        return options;
    }

    private static VideoCaptureAPIs GetDefaultUsbCaptureApi()
    {
        return OperatingSystem.IsWindows()
            ? VideoCaptureAPIs.DSHOW
            : VideoCaptureAPIs.ANY;
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
        return Interlocked.CompareExchange(ref requestedCameraIndex, -1, -1) >= 0;
    }

    private bool TryConsumeCameraSwitchRequest(out int requestedIndex)
    {
        requestedIndex = Interlocked.Exchange(ref requestedCameraIndex, -1);
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

        logEntries.Add(line);
        while (logEntries.Count > MaxLogEntries)
        {
            logEntries.RemoveAt(0);
        }

        LogListBox.SelectedIndex = logEntries.Count - 1;
        if (LogListBox.SelectedItem is not null)
        {
            LogListBox.ScrollIntoView(LogListBox.SelectedItem);
        }
    }

    private void UpdateSelectedColorCalibrationFromUi(bool logChange)
    {
        var camera = GetSelectedCamera();
        if (camera is null)
        {
            return;
        }

        var settings = GetSettingsForCamera(camera.Value.Id);
        var updatedCalibrations = BuildCalibrationsFromEditor(settings.ColorCalibrations);

        lock (settingsSync)
        {
            cameraSettings[camera.Value.Id] = settings with { ColorCalibrations = updatedCalibrations };
        }

        PersistCameraSettings();

        if (logChange)
        {
            SetStatus($"Status: {camera.Value.DisplayName} color calibration updated for {selectedCalibrationColor}.");
        }
    }

    private IReadOnlyList<ColorCalibrationProfile> BuildCalibrationsFromEditor(IReadOnlyList<ColorCalibrationProfile> source)
    {
        var selectedColor = GetCalibrationSelectionName() ?? selectedCalibrationColor;
        selectedCalibrationColor = selectedColor;

        var fallback = source.FirstOrDefault(profile => profile.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(fallback.Name))
        {
            fallback = CreateDefaultColorCalibrations().First(profile => profile.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        }

        var updated = NormalizeColorCalibration(new ColorCalibrationProfile(
            selectedColor,
            ParseInt(HueLowerTextBox.Text, fallback.HueLower, 0, 180),
            ParseInt(HueUpperTextBox.Text, fallback.HueUpper, 0, 180),
            ParseInt(SaturationLowerTextBox.Text, fallback.SaturationLower, 0, 255),
            ParseInt(SaturationUpperTextBox.Text, fallback.SaturationUpper, 0, 255),
            ParseInt(ValueLowerTextBox.Text, fallback.ValueLower, 0, 255),
            ParseInt(ValueUpperTextBox.Text, fallback.ValueUpper, 0, 255)));

        HueLowerTextBox.Text = updated.HueLower.ToString();
        HueUpperTextBox.Text = updated.HueUpper.ToString();
        SaturationLowerTextBox.Text = updated.SaturationLower.ToString();
        SaturationUpperTextBox.Text = updated.SaturationUpper.ToString();
        ValueLowerTextBox.Text = updated.ValueLower.ToString();
        ValueUpperTextBox.Text = updated.ValueUpper.ToString();

        var result = source
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(NormalizeColorCalibration)
            .Where(profile => !profile.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(profile => profile.Name, profile => profile, StringComparer.OrdinalIgnoreCase);

        result[selectedColor] = updated;

        foreach (var defaults in CreateDefaultColorCalibrations())
        {
            if (!result.ContainsKey(defaults.Name))
            {
                result[defaults.Name] = defaults;
            }
        }

        return result.Values
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadCalibrationEditor(IReadOnlyList<ColorCalibrationProfile> calibrations, string selectedColor)
    {
        selectedCalibrationColor = selectedColor;
        var profile = calibrations.FirstOrDefault(item => item.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile = CreateDefaultColorCalibrations().First(item => item.Name.Equals(selectedColor, StringComparison.OrdinalIgnoreCase));
        }

        HueLowerTextBox.Text = profile.HueLower.ToString();
        HueUpperTextBox.Text = profile.HueUpper.ToString();
        SaturationLowerTextBox.Text = profile.SaturationLower.ToString();
        SaturationUpperTextBox.Text = profile.SaturationUpper.ToString();
        ValueLowerTextBox.Text = profile.ValueLower.ToString();
        ValueUpperTextBox.Text = profile.ValueUpper.ToString();
    }

    private string? GetCalibrationSelectionName()
    {
        if (CalibrationColorComboBox.SelectedItem is ComboBoxItem item
            && item.Content is string selected
            && !string.IsNullOrWhiteSpace(selected))
        {
            return selected.Trim().ToLowerInvariant();
        }

        return null;
    }

    private static ColorCalibrationProfile NormalizeColorCalibration(ColorCalibrationProfile profile)
    {
        return new ColorCalibrationProfile(
            profile.Name.Trim().ToLowerInvariant(),
            Math.Clamp(profile.HueLower, 0, 180),
            Math.Clamp(profile.HueUpper, 0, 180),
            Math.Clamp(profile.SaturationLower, 0, 255),
            Math.Clamp(profile.SaturationUpper, 0, 255),
            Math.Clamp(profile.ValueLower, 0, 255),
            Math.Clamp(profile.ValueUpper, 0, 255));
    }

    internal static IReadOnlyList<ColorCalibrationProfile> CreateDefaultColorCalibrations()
    {
        return new List<ColorCalibrationProfile>
        {
            new ("red", 170, 10, 120, 255, 70, 255),
            new ("green", 35, 85, 80, 255, 60, 255),
            new ("blue", 90, 130, 100, 255, 60, 255),
            new ("yellow", 20, 35, 110, 255, 80, 255),
            new ("white", 0, 180, 0, 50, 190, 255),
            new ("black", 0, 180, 0, 255, 0, 45)
        };
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

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct UsbCameraSource(int CameraIndex, VideoCaptureAPIs Api);

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
            => new (id, displayName, CameraSourceKind.VideoFiles, videoPaths, null);

        public static CameraProfile CreateUsb(string id, string displayName, int cameraIndex, VideoCaptureAPIs api)
            => new (id, displayName, CameraSourceKind.UsbCamera, new List<string>(), new UsbCameraSource(cameraIndex, api));
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
        IReadOnlyList<ColorCalibrationProfile> ColorCalibrations)
    {
        public static RuntimeProcessingSettings Default => new (20, 100, 220, 40, 3, 640, BakeSourceMode.Samples, string.Empty, CreateDefaultColorCalibrations());
    }
}
