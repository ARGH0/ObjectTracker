using System;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectTracker.UI.Desktop.Plc.Contracts;

public interface IPlcSessionManager
{
    Task<bool> IsAuthenticatedAsync(CancellationToken ct = default);
    Task EnsureAuthenticatedAsync(CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
}
