using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Contracts;

namespace ObjectTracker.UI.Desktop.Plc.Siemens;

internal sealed class SiemensPlcSessionManager : IPlcSessionManager
{
    private readonly SiemensPlcConnection connection;

    public SiemensPlcSessionManager(SiemensPlcConnection connection)
    {
        this.connection = connection;
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        return Task.FromResult(connection.IsConnected);
    }

    public async Task EnsureAuthenticatedAsync(CancellationToken ct = default)
    {
        await connection.GetRequestHandlerAsync(ct);
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        connection.Reset();
        return Task.CompletedTask;
    }
}
