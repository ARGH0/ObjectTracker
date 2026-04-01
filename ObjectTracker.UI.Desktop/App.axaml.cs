using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using ObjectTracker.Vision;
using ObjectTracker.Vision.Source;

namespace ObjectTracker.UI.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var output = new AvaloniaOutputPort();
            var frameSourceFactory = new OpenCvFrameSourceFactory();

            var algorithms = new List<IDetectionAlgorithm>
            {
                new OpenCvArucoDetector(),
                new OpenCvColorDetector(),
                new AlternativeNoOpDetector()
            };

            var detectorManager = new DetectorManager(algorithms, DetectorMode.Hybrid);
            var tracker = new SimpleTracker();
            var clock = new SystemClock();
            var pipeline = new PipelineController(
                frameSourceFactory,
                detectorManager,
                tracker,
                [output],
                clock);

            desktop.MainWindow = new MainWindow(pipeline, output);
        }

        base.OnFrameworkInitializationCompleted();
    }
}