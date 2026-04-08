using System;
using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.Core.Domain;
using ObjectTracker.Core.Ports;
using ObjectTracker.Vision;
using ObjectTracker.Vision.Source;

namespace ObjectTracker.UI.Desktop;

internal static class DesktopCompositionRoot
{
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    public static (IPipelineController Pipeline, AvaloniaOutputPort Output) CreateDependencies()
    {
        var provider = BuildServiceProvider();
        return (
            provider.GetRequiredService<IPipelineController>(),
            provider.GetRequiredService<AvaloniaOutputPort>());
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFrameSourceFactory, OpenCvFrameSourceFactory>();
        services.AddSingleton<IDetectionAlgorithm, OpenCvArucoDetector>();
        services.AddSingleton<IDetectionAlgorithm, OpenCvColorDetector>();
        services.AddSingleton<IDetectionAlgorithm, AlternativeNoOpDetector>();
        services.AddSingleton<IDetectorManager>(provider =>
            new DetectorManager(provider.GetServices<IDetectionAlgorithm>(), DetectorMode.Hybrid));
        services.AddSingleton<ITracker, SimpleTracker>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AvaloniaOutputPort>();
        services.AddSingleton<IOutputPort>(provider => provider.GetRequiredService<AvaloniaOutputPort>());
        services.AddSingleton<IPipelineController, PipelineController>();
        services.AddSingleton<MainWindow>();
    }
}
