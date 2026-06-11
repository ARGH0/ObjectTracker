using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop.Plc.Contracts;

public interface IPlcClient
{
    Task<PlcReadResult> ReadAsync(PlcVariable variable, CancellationToken ct = default);
    Task<IReadOnlyList<PlcReadResult>> ReadBatchAsync(IReadOnlyList<PlcVariable> variables, CancellationToken ct = default);
    Task WriteAsync(PlcVariable variable, CancellationToken ct = default);
    Task WriteAsync(IEnumerable<(PlcVariable Variable, PlcValue Value)> writes, CancellationToken ct = default);
}
