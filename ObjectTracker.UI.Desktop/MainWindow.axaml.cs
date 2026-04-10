using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CvCapture = OpenCvSharp.VideoCapture;
using CvCaptureApi = OpenCvSharp.VideoCaptureAPIs;
using CvMat = OpenCvSharp.Mat;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : Window
{
    private enum ActiveSourceKind
    {
        None,
        Image,
        Camera,
        VideoFile
    }

    private static readonly SampleModeOption[] SampleModes = Enum
        .GetValues<OpenCvSampleMode>()
        .Select(mode => new SampleModeOption(mode, mode.ToDisplayName()))
        .ToArray();

    private Bitmap? _currentBitmap;
    private string? _currentImagePath;
    private string? _currentVideoPath;
    private ActiveSourceKind _activeSourceKind;
    private bool _ignoreVideoSliderChange;
    private bool _isScrubbingVideo;
    private bool _isVideoPaused;
    private long? _pendingSeekFrame;
    private double _currentVideoFrame;
    private double _videoFrameCount;
    private CancellationTokenSource? _playbackCancellation;
    private Task? _playbackTask;
    private OpenCvSampleMode _selectedMode = OpenCvSampleMode.Contours;
    private CvCapture? _videoCapture;

    public MainWindow()
    {
        InitializeComponent();

        ProcessingModeComboBox.ItemsSource = SampleModes;
        ProcessingModeComboBox.SelectedIndex = 0;
        ProcessingModeComboBox.SelectionChanged += ProcessingModeComboBoxOnSelectionChanged;
        SampleDescriptionText.Text = _selectedMode.GetDescription();

        OpenImageButton.Click += OpenImageButtonOnClick;
        OpenVideoButton.Click += OpenVideoButtonOnClick;
        StartCameraButton.Click += StartCameraButtonOnClick;
        StopCameraButton.Click += StopCameraButtonOnClick;
        PauseVideoButton.Click += PauseVideoButtonOnClick;
        ResumeVideoButton.Click += ResumeVideoButtonOnClick;
        StepBackButton.Click += StepBackButtonOnClick;
        StepForwardButton.Click += StepForwardButtonOnClick;
        VideoPositionSlider.PointerPressed += VideoPositionSliderOnPointerPressed;
        VideoPositionSlider.PointerReleased += VideoPositionSliderOnPointerReleased;
        VideoPositionSlider.PropertyChanged += VideoPositionSliderOnPropertyChanged;

        UpdateVideoTransportControls();
    }

    private async void ProcessingModeComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProcessingModeComboBox.SelectedItem is SampleModeOption option)
        {
            _selectedMode = option.Mode;
            OpenCvObjectRecognizer.Reset(option.Mode);
            SampleDescriptionText.Text = option.Mode.GetDescription();
            await RefreshCurrentSourceAsync(option.Label);

            if (_activeSourceKind == ActiveSourceKind.None)
            {
                StatusText.Text = $"Selected sample mode: {option.Label}. Open an image or start the camera.";
            }
        }
    }

    private async void StartCameraButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await StartCameraAsync();
    }

    private async void OpenVideoButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var file = await PickVideoAsync();
            if (file is null)
            {
                return;
            }

            await StartVideoFileAsync(file.Path.LocalPath, false);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unable to play video: {ex.Message}";
            ResultsListBox.ItemsSource = Array.Empty<string>();
        }
    }

    private async void StopCameraButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await StopPlaybackAsync("Playback stopped.");
    }

    private void PauseVideoButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_activeSourceKind != ActiveSourceKind.VideoFile)
        {
            return;
        }

        _isVideoPaused = true;
        UpdateVideoTransportControls();
        StatusText.Text = "Video playback paused.";
    }

    private void ResumeVideoButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_activeSourceKind != ActiveSourceKind.VideoFile)
        {
            return;
        }

        _isVideoPaused = false;
        UpdateVideoTransportControls();
        StatusText.Text = "Video playback resumed.";
    }

    private async void StepBackButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await StepVideoAsync(-1);
    }

    private async void StepForwardButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await StepVideoAsync(1);
    }

    private void VideoPositionSliderOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_activeSourceKind == ActiveSourceKind.VideoFile)
        {
            _isScrubbingVideo = true;
        }
    }

    private async void VideoPositionSliderOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isScrubbingVideo)
        {
            return;
        }

        _isScrubbingVideo = false;
        await SeekVideoAsync((long)Math.Round(VideoPositionSlider.Value), keepPaused: _isVideoPaused);
    }

    private void VideoPositionSliderOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty || _ignoreVideoSliderChange || !_isScrubbingVideo)
        {
            return;
        }

        PlaybackPositionText.Text = FormatPlaybackPosition(VideoPositionSlider.Value, _videoFrameCount);
    }

    private async Task StartCameraAsync()
    {
        await StopPlaybackAsync(null);
        OpenCvObjectRecognizer.Reset(_selectedMode);

        var capture = new CvCapture();
        capture.Open(0, CvCaptureApi.ANY);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            StatusText.Text = "Unable to open the default camera. Check device permissions and whether another app is using it.";
            ResultsListBox.ItemsSource = new[] { "Camera open failed." };
            return;
        }

        _videoCapture = capture;
        _playbackCancellation = new CancellationTokenSource();
        _playbackTask = RunPlaybackLoopAsync(capture, "camera", ActiveSourceKind.Camera, _playbackCancellation.Token);
        _activeSourceKind = ActiveSourceKind.Camera;
        _currentImagePath = null;
        _currentVideoPath = null;
        _isVideoPaused = false;
        _pendingSeekFrame = null;
        _currentVideoFrame = 0;
        _videoFrameCount = 0;

        UpdateCameraButtons(isRunning: true);
        UpdateVideoTransportControls();
        EmptyStateText.IsVisible = false;
        StatusText.Text = $"Camera preview running with the {_selectedMode.ToDisplayName()} sample.";
        ResultsListBox.ItemsSource = new[] { "Waiting for frames..." };
    }

    private async Task StartVideoFileAsync(string videoPath, bool restartedForModeChange)
    {
        await StopPlaybackAsync(null);
        OpenCvObjectRecognizer.Reset(_selectedMode);

        var capture = new CvCapture();
        capture.Open(videoPath, CvCaptureApi.ANY);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            StatusText.Text = "Unable to open the selected video file.";
            ResultsListBox.ItemsSource = new[] { "Video open failed." };
            return;
        }

        _videoCapture = capture;
        _videoFrameCount = Math.Max(0, capture.FrameCount);
        _currentVideoFrame = 0;
        _isVideoPaused = false;
        _pendingSeekFrame = null;
        _playbackCancellation = new CancellationTokenSource();
        _playbackTask = RunPlaybackLoopAsync(capture, Path.GetFileName(videoPath), ActiveSourceKind.VideoFile, _playbackCancellation.Token);
        _activeSourceKind = ActiveSourceKind.VideoFile;
        _currentImagePath = null;
        _currentVideoPath = videoPath;

        UpdateCameraButtons(isRunning: true);
        UpdateVideoTransportControls();
        UpdatePlaybackPosition(0, _videoFrameCount);
        EmptyStateText.IsVisible = false;
        StatusText.Text = restartedForModeChange
            ? $"Video restarted with the {_selectedMode.ToDisplayName()} sample."
            : $"Video {Path.GetFileName(videoPath)} playing with the {_selectedMode.ToDisplayName()} sample.";
        ResultsListBox.ItemsSource = new[] { "Starting video playback..." };
    }

    private async Task RunPlaybackLoopAsync(CvCapture capture, string sourceName, ActiveSourceKind sourceKind, CancellationToken cancellationToken)
    {
        try
        {
            using var frame = new CvMat();
            var delay = GetFrameDelay(capture);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (sourceKind == ActiveSourceKind.VideoFile)
                {
                    if (_pendingSeekFrame is long seekFrame)
                    {
                        capture.PosFrames = (int)ClampFrameIndex(seekFrame, _videoFrameCount);
                        _pendingSeekFrame = null;
                    }

                    if (_isVideoPaused)
                    {
                        await Task.Delay(30, cancellationToken);
                        continue;
                    }
                }

                if (!capture.Read(frame) || frame.Empty())
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusText.Text = sourceKind == ActiveSourceKind.VideoFile
                            ? $"Video {sourceName} finished."
                            : "Camera preview ended because no frames were returned.";
                        ResultsListBox.ItemsSource = new[]
                        {
                            sourceKind == ActiveSourceKind.VideoFile
                                ? "End of video reached."
                                : "No camera frame available."
                        };

                        if (sourceKind == ActiveSourceKind.VideoFile)
                        {
                            _isVideoPaused = true;
                            UpdateVideoTransportControls();
                        }
                    });
                    break;
                }

                var recognition = OpenCvObjectRecognizer.RecognizeFrame(frame, _selectedMode, sourceName);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowRecognition(recognition);

                    if (sourceKind == ActiveSourceKind.VideoFile)
                    {
                        var currentFrame = Math.Max(0, capture.PosFrames - 1);
                        UpdatePlaybackPosition(currentFrame, _videoFrameCount);
                        UpdateVideoTransportControls();
                    }
                });

                if (sourceKind == ActiveSourceKind.VideoFile && _isVideoPaused)
                {
                    continue;
                }

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText.Text = $"Playback failed: {ex.Message}";
                ResultsListBox.ItemsSource = new[] { "Playback failed." };
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCameraButtons(isRunning: false);
                UpdateVideoTransportControls();
            });
        }
    }

    private async void OpenImageButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await StopPlaybackAsync(null);
            OpenCvObjectRecognizer.Reset(_selectedMode);

            var file = await PickImageAsync();
            if (file is null)
            {
                return;
            }

            var recognition = await ProcessImageAsync(file.Path.LocalPath);
            _currentImagePath = file.Path.LocalPath;
            _currentVideoPath = null;
            _activeSourceKind = ActiveSourceKind.Image;
            _isVideoPaused = false;
            _pendingSeekFrame = null;
            _currentVideoFrame = 0;
            _videoFrameCount = 0;
            UpdateVideoTransportControls();
            ShowRecognition(recognition);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unable to process image: {ex.Message}";
            ResultsListBox.ItemsSource = Array.Empty<string>();
        }
    }

    private async Task<IStorageFile?> PickImageAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"]
                }
            ]
        });

        return files.FirstOrDefault();
    }

    private async Task<IStorageFile?> PickVideoAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open video",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video files")
                {
                    Patterns = ["*.mp4", "*.avi", "*.mov", "*.mkv", "*.webm"]
                }
            ]
        });

        return files.FirstOrDefault();
    }

    private Task<RecognitionResult> ProcessImageAsync(string imagePath)
    {
        return Task.Run(() => OpenCvObjectRecognizer.RecognizeImage(imagePath, _selectedMode));
    }

    private void ShowRecognition(RecognitionResult recognition)
    {
        EmptyStateText.IsVisible = false;
        StatusText.Text = recognition.Status;
        ResultsListBox.ItemsSource = recognition.Details.Count == 0
            ? ["No details returned for this sample."]
            : recognition.Details;

        _currentBitmap?.Dispose();
        _currentBitmap = CreateBitmap(recognition.AnnotatedImageBytes);
        PreviewImage.Source = _currentBitmap;
    }

    private async Task RefreshCurrentSourceAsync(string selectedLabel)
    {
        switch (_activeSourceKind)
        {
            case ActiveSourceKind.Image when !string.IsNullOrWhiteSpace(_currentImagePath):
            {
                var recognition = await ProcessImageAsync(_currentImagePath);
                ShowRecognition(recognition);
                break;
            }
            case ActiveSourceKind.VideoFile when !string.IsNullOrWhiteSpace(_currentVideoPath):
                await StartVideoFileAsync(_currentVideoPath, true);
                break;
            case ActiveSourceKind.Camera when _playbackTask is not null && !_playbackTask.IsCompleted:
                StatusText.Text = $"Camera preview switched to {selectedLabel}.";
                break;
        }
    }

    private async Task SeekVideoAsync(long frameIndex, bool keepPaused)
    {
        if (_activeSourceKind != ActiveSourceKind.VideoFile || _videoCapture is null)
        {
            return;
        }

        OpenCvObjectRecognizer.Reset(_selectedMode);
        _isVideoPaused = keepPaused;
        _pendingSeekFrame = ClampFrameIndex(frameIndex, _videoFrameCount);
        UpdatePlaybackPosition(_pendingSeekFrame.Value, _videoFrameCount);
        UpdateVideoTransportControls();

        if (_playbackTask is null || _playbackTask.IsCompleted)
        {
            return;
        }

        await Task.Yield();
    }

    private async Task StepVideoAsync(int direction)
    {
        if (_activeSourceKind != ActiveSourceKind.VideoFile || _videoCapture is null)
        {
            return;
        }

        OpenCvObjectRecognizer.Reset(_selectedMode);
        _isVideoPaused = true;
        var targetFrame = ClampFrameIndex(_currentVideoFrame + direction, _videoFrameCount);
        _pendingSeekFrame = targetFrame;
        UpdateVideoTransportControls();
        StatusText.Text = $"Video frame step to {targetFrame + 1:0}.";

        if (_playbackTask is null || _playbackTask.IsCompleted)
        {
            return;
        }

        await Task.Yield();
    }

    private async Task StopPlaybackAsync(string? statusMessage)
    {
        var cancellation = _playbackCancellation;
        var runningTask = _playbackTask;

        _playbackCancellation = null;
        _playbackTask = null;

        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        if (runningTask is not null)
        {
            try
            {
                await runningTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _videoCapture?.Release();
        _videoCapture?.Dispose();
        _videoCapture = null;
        _isVideoPaused = false;
        _pendingSeekFrame = null;
        _currentVideoFrame = 0;
        _videoFrameCount = 0;

        UpdateCameraButtons(isRunning: false);
        UpdateVideoTransportControls();

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            StatusText.Text = statusMessage;
        }
    }

    private void UpdateCameraButtons(bool isRunning)
    {
        StartCameraButton.IsEnabled = !isRunning;
        StopCameraButton.IsEnabled = isRunning;
    }

    private void UpdateVideoTransportControls()
    {
        var hasVideo = _activeSourceKind == ActiveSourceKind.VideoFile && !string.IsNullOrWhiteSpace(_currentVideoPath);
        var canControl = hasVideo && _videoCapture is not null;

        PauseVideoButton.IsEnabled = canControl && !_isVideoPaused && _playbackTask is not null && !_playbackTask.IsCompleted;
        ResumeVideoButton.IsEnabled = canControl && _isVideoPaused && _playbackTask is not null && !_playbackTask.IsCompleted;
        StepBackButton.IsEnabled = canControl && _isVideoPaused;
        StepForwardButton.IsEnabled = canControl && _isVideoPaused;
        VideoPositionSlider.IsEnabled = canControl && _videoFrameCount > 1;

        if (!hasVideo)
        {
            _ignoreVideoSliderChange = true;
            VideoPositionSlider.Maximum = 1;
            VideoPositionSlider.Value = 0;
            _ignoreVideoSliderChange = false;
            PlaybackPositionText.Text = "Geen video geladen.";
            return;
        }

        PlaybackPositionText.Text = FormatPlaybackPosition(_currentVideoFrame, _videoFrameCount);
    }

    private void UpdatePlaybackPosition(double currentFrame, double frameCount)
    {
        _currentVideoFrame = ClampFrameIndex(currentFrame, frameCount);
        _videoFrameCount = Math.Max(0, frameCount);

        _ignoreVideoSliderChange = true;
        VideoPositionSlider.Maximum = Math.Max(1, _videoFrameCount - 1);
        VideoPositionSlider.Value = Math.Min(VideoPositionSlider.Maximum, _currentVideoFrame);
        _ignoreVideoSliderChange = false;

        PlaybackPositionText.Text = FormatPlaybackPosition(_currentVideoFrame, _videoFrameCount);
    }

    private static long ClampFrameIndex(double frameIndex, double frameCount)
    {
        var maximum = Math.Max(0, frameCount - 1);
        return (long)Math.Clamp(Math.Round(frameIndex), 0, maximum);
    }

    private static string FormatPlaybackPosition(double currentFrame, double frameCount)
    {
        if (frameCount <= 0)
        {
            return "Geen video geladen.";
        }

        return $"Frame {currentFrame + 1:0} / {frameCount:0}";
    }

    private static TimeSpan GetFrameDelay(CvCapture capture)
    {
        var fps = capture.Fps;
        if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0)
        {
            return TimeSpan.FromMilliseconds(33);
        }

        return TimeSpan.FromMilliseconds(Math.Clamp(1000d / fps, 15d, 100d));
    }

    private static Bitmap CreateBitmap(byte[] encodedImage)
    {
        using var stream = new MemoryStream(encodedImage);
        return new Bitmap(stream);
    }

    protected override void OnClosed(EventArgs e)
    {
        _playbackCancellation?.Cancel();
        _videoCapture?.Release();
        _videoCapture?.Dispose();
        _currentBitmap?.Dispose();
        base.OnClosed(e);
    }

    private sealed record SampleModeOption(OpenCvSampleMode Mode, string Label)
    {
        public override string ToString() => Label;
    }
}