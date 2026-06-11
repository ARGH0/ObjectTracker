using System;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Model;
using Siemens.Simatic.S7.Webserver.API.Services;
using Siemens.Simatic.S7.Webserver.API.Services.RequestHandling;

namespace ObjectTracker.UI.Desktop.Plc.Siemens;

internal sealed class SiemensPlcConnection
{
    private readonly PlcClientConfig config;
    private readonly ApiStandardServiceFactory serviceFactory = new();
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private IApiRequestHandler? requestHandler;

    public SiemensPlcConnection(PlcClientConfig config)
    {
        this.config = config;
        ServerCertificateCallback.CertificateCallback = (_, _, _, _) => true;
    }

    public bool IsConnected => requestHandler is not null;

    public async Task<IApiRequestHandler> GetRequestHandlerAsync(CancellationToken ct)
    {
        if (requestHandler is not null)
        {
            return requestHandler;
        }

        await connectionLock.WaitAsync(ct);
        try
        {
            requestHandler ??= await serviceFactory.GetApiHttpClientRequestHandlerAsync(
                NormalizeBaseAddress(config.BaseUrl),
                config.User,
                config.Password,
                ct);

            return requestHandler;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw SiemensPlcExceptionMapper.Map(ex);
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public void Reset()
    {
        if (requestHandler is IDisposable disposable)
        {
            disposable.Dispose();
        }

        requestHandler = null;
    }

    private static string NormalizeBaseAddress(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        return baseUrl
            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
    }
}
