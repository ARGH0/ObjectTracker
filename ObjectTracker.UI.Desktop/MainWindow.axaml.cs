using System;
using System.Linq;
using System.Threading;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;

namespace ObjectTracker.UI.Desktop;

public partial class MainWindow : Window
{
    private readonly IPipelineController _pipeline;
    private readonly AvaloniaOutputPort _output;
    private readonly OverlaySettingsStore _overlaySettingsStore;
    private bool _isBinding;

    public MainWindow()
        : this(ResolveDependencies())
    {
    }

    private MainWindow((IPipelineController Pipeline, AvaloniaOutputPort Output) dependencies)
        : this(dependencies.Pipeline, dependencies.Output)
    {
    }

    public MainWindow(IPipelineController pipeline, AvaloniaOutputPort output)
    {
        _pipeline = pipeline;
        _output = output;
        _overlaySettingsStore = new OverlaySettingsStore();

        try
        {
            _overlaySettingsStore.LoadIntoPipeline(_pipeline);
        }
        catch
        {
            // Ignore persisted-settings load errors and continue with defaults.
        }

        InitializeComponent();
        BindData();
        HookEvents();
    }

    private static (IPipelineController Pipeline, AvaloniaOutputPort Output) ResolveDependencies()
    {
        if (App.Services is not null)
        {
            return (
                App.Services.GetRequiredService<IPipelineController>(),
                App.Services.GetRequiredService<AvaloniaOutputPort>());
        }

        return DesktopCompositionRoot.CreateDependencies();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        try
        {
            _overlaySettingsStore.SaveFromPipeline(_pipeline);
        }
        catch
        {
            // Ignore persisted-settings save errors during shutdown.
        }

        try
        {
            await _pipeline.StopAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Ignore pipeline stop errors during shutdown to avoid crashing the app.
            // Optionally surface a generic status message; avoid throwing.
            if (StatusText is not null)
            {
                StatusText.Text = "Error while stopping pipeline during shutdown.";
            }
        }
        base.OnClosing(e);
    }

    private void BindData()
    {
        _isBinding = true;

        SourceComboBox.ItemsSource = _pipeline.AvailableSources.ToList();
        SourceComboBox.SelectedItem = _pipeline.AvailableSources.FirstOrDefault();

        DetectorComboBox.ItemsSource = _pipeline.AvailableDetectors.ToList();
        DetectorComboBox.SelectedItem = _pipeline.ActiveDetector;

        ColorFilterPanel.Children.Clear();
        var enabled = _pipeline.EnabledColorFilters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var color in _pipeline.AvailableColorFilters)
        {
            var checkBox = new CheckBox
            {
                Content = color,
                IsChecked = enabled.Contains(color)
            };

            checkBox.IsCheckedChanged += ColorFilterCheckBoxOnChanged;
            ColorFilterPanel.Children.Add(checkBox);
        }

        MetricsText.Text = "FPS: 0 | Proc: 0ms";
        StatusText.Text = "Status: idle";
        TracksListBox.ItemsSource = Array.Empty<string>();

        _isBinding = false;
    }

    private void HookEvents()
    {
        StartButton.Click += StartButtonOnClick;
        StopButton.Click += StopButtonOnClick;
        DetectorComboBox.SelectionChanged += DetectorComboBoxOnSelectionChanged;
        SourceComboBox.SelectionChanged += SourceComboBoxOnSelectionChanged;
        OpenOverlaySettingsButton.Click += OpenOverlaySettingsButtonOnClick;

        _output.SnapshotPublished += snapshot => Dispatcher.UIThread.Post(() => RenderSnapshot(snapshot));
        _output.StatusPublished += status => Dispatcher.UIThread.Post(() => StatusText.Text = status);
    }

    private async void StartButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (SourceComboBox.SelectedItem is FrameSourceInfo selectedSource)
        {
            await _pipeline.StartAsync(selectedSource.Id, CancellationToken.None);
        }
    }

    private async void StopButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await _pipeline.StopAsync(CancellationToken.None);
    }

    private void DetectorComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isBinding)
        {
            return;
        }

        if (DetectorComboBox.SelectedItem is DetectorMode mode)
        {
            _pipeline.SwitchDetector(mode);
        }
    }

    private async void SourceComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isBinding || !_pipeline.IsRunning)
        {
            return;
        }

        if (SourceComboBox.SelectedItem is FrameSourceInfo source)
        {
            await _pipeline.SwitchSourceAsync(source.Id, CancellationToken.None);
        }
    }

    private void ColorFilterCheckBoxOnChanged(object? sender, RoutedEventArgs e)
    {
        ApplyColorFiltersFromUi();
    }

    private async void OpenOverlaySettingsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new OverlaySettingsWindow(_pipeline, _overlaySettingsStore);
        await settingsWindow.ShowDialog(this);
        StatusText.Text = "Status: overlay instellingen bijgewerkt";
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

        _pipeline.SetEnabledColorFilters(selectedColors);
    }

    private void RenderSnapshot(PipelineSnapshot snapshot)
    {
        RenderFrame(snapshot.Frame);
        MetricsText.Text = $"FPS: {snapshot.FramesPerSecond} | Proc: {snapshot.ProcessingMs:F1}ms | {snapshot.ActiveDetector}";
        TracksListBox.ItemsSource = snapshot.Tracks
            .Select(track => $"{track.TrackId}: ({track.X:F0}, {track.Y:F0}) {track.SpeedPixelsPerSecond:F1}px/s")
            .ToList();
    }

    private void RenderFrame(FramePacket frame)
    {
        using var ms = new MemoryStream(frame.EncodedJpeg);
        var bitmap = new Bitmap(ms);

        var previous = PreviewImage.Source as Bitmap;
        PreviewImage.Source = bitmap;
        previous?.Dispose();
    }
}