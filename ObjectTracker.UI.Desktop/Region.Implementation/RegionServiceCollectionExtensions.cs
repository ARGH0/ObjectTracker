using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.UI.Desktop.Region.Contracts;
using ObjectTracker.UI.Desktop.Region.Implementation;

namespace ObjectTracker.UI.Desktop;

public static class RegionServiceCollectionExtensions
{
    public static IServiceCollection AddRegionServices(this IServiceCollection services, string? filePath = null)
    {
        var defaultPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "ObjectTracker",
            "regions.json");

        services.AddSingleton<ICoordinateMapper, CoordinateMapper>();
        services.AddSingleton<IRegionPriorityResolver, RegionPriorityResolver>();
        services.AddSingleton<IRegionPersistence>(sp => new RegionPersistence(filePath ?? defaultPath));
        services.AddSingleton<IRegionRegistry, RegionRegistry>();
        services.AddSingleton<IHandoffResolver, HandoffResolver>();
        services.AddSingleton<IRegionEvaluator, RegionEvaluator>();
        services.AddSingleton<IRegionProcessorService, RegionProcessorService>();
        services.AddSingleton<IRegionManagerService, RegionManagerService>();
        services.AddTransient<GridEditorDialog>();
        return services;
    }
}
