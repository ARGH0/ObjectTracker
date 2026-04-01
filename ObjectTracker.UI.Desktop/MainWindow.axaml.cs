using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _snapshotTimer;
    private readonly Random _random = new();
    private readonly List<FrameSourceInfo> _sources =
    [
        new("cam0", "Camera 0"),
        new("cam1", "Camera 1"),
        new("video", "Video file")
    ];

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

    public MainWindow()
    {
        InitializeComponent();

        _snapshotTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        BindData();
        HookEvents();
        RenderIdleState();
    }

    private void BindData()
    {
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
        _snapshotTimer.Tick += SnapshotTimerOnTick;
    }

    private void StartButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _snapshotTimer.Start();

        var source = SourceComboBox.SelectedItem as FrameSourceInfo;
        StatusText.Text = source is null
            ? "Status: running"
            : $"Status: running ({source.DisplayName})";
    }

    private void StopButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _snapshotTimer.Stop();
        StatusText.Text = "Status: stopped";
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

    private void SourceComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SourceComboBox.SelectedItem is not FrameSourceInfo source)
        {
            return;
        }

        if (!_isRunning)
        {
            StatusText.Text = $"Status: idle | source ready: {source.DisplayName}";
            return;
        }

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

    private void SnapshotTimerOnTick(object? sender, EventArgs e)
    {
        _frameCounter++;

        var fps = 28 + _random.NextDouble() * 6;
        var processingMs = 8 + _random.NextDouble() * 10;
        var activeDetector = DetectorComboBox.SelectedItem is DetectorMode mode
            ? mode.ToString()
            : DetectorMode.Color.ToString();

        MetricsText.Text = $"FPS: {fps:F1} | Proc: {processingMs:F1}ms | {activeDetector}";

        var trackCount = _random.Next(1, 5);
        var tracks = Enumerable.Range(1, trackCount)
            .Select(index =>
            {
                var x = _random.NextDouble() * 1440;
                var y = _random.NextDouble() * 810;
                var speed = _random.NextDouble() * 240;
                return $"T{index:00}: ({x:F0}, {y:F0}) {speed:F1}px/s";
            })
            .ToList();

        TracksListBox.ItemsSource = tracks;
        ToolTip.SetTip(PreviewImage, $"Frame {_frameCounter}");
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

    private sealed record FrameSourceInfo(string Id, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}