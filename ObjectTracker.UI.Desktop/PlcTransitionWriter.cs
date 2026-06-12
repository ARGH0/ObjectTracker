using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop;

internal sealed class PlcTransitionWriter
{
    private readonly IPlcClient _plcClient;
    private readonly Action<string> _log;

    public PlcTransitionWriter(IPlcClient plcClient, Action<string> log)
    {
        _plcClient = plcClient;
        _log = log;
    }

    public async Task WriteTransitionAsync(string regionName, string trainColor, bool isPlcConnected, CancellationToken ct = default)
    {
        var variableName = $"data.{regionName}";
        var variable = new PlcVariable(variableName, PlcVariableType.Int32);
        if (!TryGetTrainColorValue(trainColor, out var colorValue))
        {
            _log($"PLC write skipped: region={regionName} trainColor={trainColor} reason=unknown train color");
            return;
        }

        var value = PlcValue.Int32(colorValue);

        if (!isPlcConnected)
        {
            _log($"PLC write skipped: variable={variableName} value={colorValue} trainColor={trainColor} region={regionName} reason=not connected");
            return;
        }

        _log($"PLC write: variable={variableName} value={colorValue} trainColor={trainColor} region={regionName}");
        await _plcClient.WriteAsync(new[] { (variable, value) }, ct);
    }

    private static bool TryGetTrainColorValue(string trainColor, out int value)
    {
        switch (trainColor.Trim().ToUpperInvariant())
        {
            case "BLUE":
                value = 1;
                return true;
            case "RED":
                value = 2;
                return true;
            case "GREEN":
                value = 3;
                return true;
            case "WHITE":
                value = 4;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
