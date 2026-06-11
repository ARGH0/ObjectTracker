using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop.Plc.Implementation;

internal sealed class PlcAuthHttpHandler : DelegatingHandler
{
    private readonly PlcClientConfig _config;
    private readonly IPlcSessionManager _sessionManager;

    public PlcAuthHttpHandler(PlcClientConfig config, IPlcSessionManager sessionManager)
    {
        _config = config;
        _sessionManager = sessionManager;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _sessionManager.EnsureAuthenticatedAsync(cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
