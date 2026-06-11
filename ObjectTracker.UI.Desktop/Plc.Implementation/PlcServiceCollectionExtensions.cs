using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Implementation;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop;

public static class PlcServiceCollectionExtensions
{
    public static IServiceCollection AddPlcServices(this IServiceCollection services, PlcClientConfig config)
    {
        services.AddSingleton(config);

        // Build dependencies in order to avoid circular resolution:
        // 1. SessionManager needs a plain HttpClient (no auth handler)
        var sessionHttpClient = new HttpClient(new HttpClientHandler(), disposeHandler: true);
        sessionHttpClient.Timeout = TimeSpan.FromSeconds(10);
        var sessionManager = new PlcSessionManager(sessionHttpClient, config);

        // 2. Auth handler uses the session manager
        var authHandler = new PlcAuthHttpHandler(config, sessionManager);
        var clientHttpClient = new HttpClient(authHandler, disposeHandler: true);
        clientHttpClient.Timeout = TimeSpan.FromSeconds(10);

        // 3. Client uses the auth-enabled HttpClient
        var plcClient = new PlcClient(clientHttpClient, config);

        services.AddSingleton<IPlcSessionManager>(sessionManager);
        services.AddSingleton<IPlcClient>(plcClient);

        return services;
    }
}
