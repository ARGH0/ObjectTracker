using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ObjectTracker.Core.Domain;
using ObjectTracker.Vision.Source;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : Window
{
    private readonly Random _random = new();
    private readonly List<IFrameSource> _frameSources = [new MockFrameSource()];
    private readonly List<FrameSourceOption> _sources = [];

    private readonly List<string> _availableColorFilters =
    [
        "Red",
        "Orange",
        "Pink",
        "Purple",
        "Green",
        "Blue",
        "Cyan",
        "Yellow",
        "White",
        "Black",
        "ArUco"
    ];

    private bool _isRunning;
    private int _frameCounter;
    private DateTime _fpsWindowStartedUtc;
    private int _framesInCurrentFpsWindow;
    private double _lastMeasuredFps;
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;
    private IFrameSource? _activeSource;

    public MainWindow()
    {
        InitializeComponent();

        BindData();
        HookEvents();
        RenderIdleState();
    }

    private void BindData()
    {
        _sources.Clear();
        foreach (var source in _frameSources)
        {
            _sources.Add(new FrameSourceOption(source.Id, source.DisplayName, source));
        }

        SourceComboBox.ItemsSource = _sources;
        SourceComboBox.SelectedItem = _sources.FirstOrDefault();

        DetectorComboBox.ItemsSource = Enum.GetValues<DetectorMode>();
        DetectorComboBox.SelectedItem = DetectorMode.Color;

        ColorFilterPanel.Children.Clear();
        foreach (var color in _availableColorFilters)
        {
            var checkBox = new CheckBox
            {
                Content = color,
                IsChecked = color is "Red" or "Orange" or "Green" or "Blue"
            };

            checkBox.IsCheckedChanged += ColorFilterCheckBoxOnChanged;
            ColorFilterPanel.Children.Add(checkBox);
        }

        TracksListBox.ItemsSource = Array.Empty<string>();
    }

    private void HookEvents()
    {
        StartButton.Click += StartButtonOnClick;
        StopButton.Click += StopButtonOnClick;
        SourceComboBox.SelectionChanged += SourceComboBoxOnSelectionChanged;
        DetectorComboBox.SelectionChanged += DetectorComboBoxOnSelectionChanged;
        Closing += OnWindowClosing;
    }

    private async void StartButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        if (SourceComboBox.SelectedItem is not FrameSourceOption selected)
        {
            StatusText.Text = "Status: idle | no source selected";
            return;
        }

        await StartStreamingAsync(selected);
    }

    private async void StopButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await StopStreamingAsync("Status: stopped");
    }

    private void DetectorComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DetectorComboBox.SelectedItem is not DetectorMode detectorMode)
        {
            return;
        }

        var statusPrefix = _isRunning ? "Status: running" : "Status: idle";
        StatusText.Text = $"{statusPrefix} | detector: {detectorMode}";
    }

    private async void SourceComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SourceComboBox.SelectedItem is not FrameSourceOption source)
        {
            return;
        }

        if (!_isRunning)
        {
            StatusText.Text = $"Status: idle | source ready: {source.DisplayName}";
            return;
        }

        await StartStreamingAsync(source);
        StatusText.Text = $"Status: running | source switched to {source.DisplayName}";
    }

    private void ColorFilterCheckBoxOnChanged(object? sender, RoutedEventArgs e)
    {
        ApplyColorFiltersFromUi();
    }

    private void ApplyColorFiltersFromUi()
    {
        var selectedColors = ColorFilterPanel.Children
            .OfType<CheckBox>()
            .Where(checkBox => checkBox.IsChecked == true)
            .Select(checkBox => checkBox.Content?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

        var selectedText = selectedColors.Count == 0
            ? "none"
            : string.Join(", ", selectedColors);

        var statusPrefix = _isRunning ? "Status: running" : "Status: idle";
        StatusText.Text = $"{statusPrefix} | filters: {selectedText}";
    }

    private async Task StartStreamingAsync(FrameSourceOption source)
    {
        await StopStreamingAsync("Status: idle");

        _activeSource = source.Source;
        _frameCounter = 0;
        _framesInCurrentFpsWindow = 0;
        _lastMeasuredFps = 0;
        _fpsWindowStartedUtc = DateTime.UtcNow;
        _streamCts = new CancellationTokenSource();

        await _activeSource.StartAsync(_streamCts.Token);

        _isRunning = true;
        StatusText.Text = $"Status: running ({source.DisplayName})";

        _streamTask = RunStreamLoopAsync(_activeSource, _streamCts.Token);
    }

    private async Task StopStreamingAsync(string status)
    {
        var sourceToStop = _activeSource;
        if (_streamCts is not null)
        {
            await _streamCts.CancelAsync();
            _streamCts.Dispose();
            _streamCts = null;
        }

        if (_streamTask is not null)
        {
            try
            {
                await _streamTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping the stream.
            }

            _streamTask = null;
        }

        if (sourceToStop is not null)
        {
            await sourceToStop.StopAsync(CancellationToken.None);
        }

        _activeSource = null;
        _isRunning = false;
        StatusText.Text = status;
    }

    private async Task RunStreamLoopAsync(IFrameSource source, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            FramePacket? frame;
            try
            {
                frame = await source.ReadFrameAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (frame is null)
            {
                continue;
            }

            _frameCounter++;
            _framesInCurrentFpsWindow++;
            var elapsed = DateTime.UtcNow - _fpsWindowStartedUtc;
            if (elapsed.TotalSeconds >= 1)
            {
                _lastMeasuredFps = _framesInCurrentFpsWindow / elapsed.TotalSeconds;
                _framesInCurrentFpsWindow = 0;
                _fpsWindowStartedUtc = DateTime.UtcNow;
            }

            var processingMs = 12 + _random.NextDouble() * 4;
            var detectorText = DetectorComboBox.SelectedItem is DetectorMode mode
                ? mode.ToString()
                : DetectorMode.Color.ToString();

            var tracks = BuildTrackRows(frame.Width, frame.Height);

            Dispatcher.UIThread.Post(() =>
            {
                RenderFrame(frame);
                MetricsText.Text = $"FPS: {_lastMeasuredFps:F1} | Proc: {processingMs:F1}ms | {detectorText}";
                TracksListBox.ItemsSource = tracks;
            });

            var diagnostic = source.ConsumeDiagnosticEvent();
            if (!string.IsNullOrWhiteSpace(diagnostic))
            {
                Dispatcher.UIThread.Post(() => StatusText.Text = $"Status: running | {diagnostic}");
            }
        }
    }

    private List<string> BuildTrackRows(int width, int height)
    {
        var trackCount = _random.Next(1, 4);
        var tracks = new List<string>(trackCount);
        for (var i = 1; i <= trackCount; i++)
        {
            var x = _random.Next(20, Math.Max(21, width - 20));
            var y = _random.Next(20, Math.Max(21, height - 20));
            var speed = _random.NextDouble() * 220;
            tracks.Add($"T{i:00}: ({x}, {y}) {speed:F1}px/s");
        }

        return tracks;
    }

    private void RenderFrame(FramePacket frame)
    {
        using var ms = new MemoryStream(frame.EncodedJpeg);
        var bitmap = new Bitmap(ms);

        var previous = PreviewImage.Source as Bitmap;
        PreviewImage.Source = bitmap;
        previous?.Dispose();

        ToolTip.SetTip(PreviewImage, $"Frame {_frameCounter}");
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        await StopStreamingAsync("Status: stopped");

        foreach (var frameSource in _frameSources)
        {
            await frameSource.DisposeAsync();
        }
    }

    private void RenderIdleState()
    {
        StatusText.Text = "Status: idle";
        MetricsText.Text = "FPS: 0 | Proc: 0ms";
    }

    private enum DetectorMode
    {
        Color,
        ArUco,
        Hybrid
    }

    private sealed record FrameSourceOption(string Id, string DisplayName, IFrameSource Source)
    {
        public override string ToString() => DisplayName;
    }
}