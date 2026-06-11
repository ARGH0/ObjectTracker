using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop.Plc.Siemens;

public static class SiemensPlcServiceCollectionExtensions
{
    public static IServiceCollection AddSiemensPlcServices(this IServiceCollection services, PlcClientConfig config)
    {
        services.AddSingleton(config);
        services.AddSingleton<SiemensPlcConnection>();
        services.AddSingleton<IPlcSessionManager, SiemensPlcSessionManager>();
        services.AddSingleton<IPlcClient, SiemensPlcClient>();

        return services;
    }
}
