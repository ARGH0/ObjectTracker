using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Implementation;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop.Plc.Siemens;

internal sealed class SiemensPlcClient : IPlcClient
{
    private readonly SiemensPlcConnection connection;

    public SiemensPlcClient(SiemensPlcConnection connection)
    {
        this.connection = connection;
    }

    public async Task<PlcReadResult> ReadAsync(PlcVariable variable, CancellationToken ct = default)
    {
        try
        {
            var requestHandler = await connection.GetRequestHandlerAsync(ct);
            var value = variable.Type switch
            {
                PlcVariableType.Bool => PlcValue.Bool((await requestHandler.PlcProgramReadAsync<bool>(variable.Address, cancellationToken: ct)).Result),
                PlcVariableType.Int16 => PlcValue.Int16((await requestHandler.PlcProgramReadAsync<short>(variable.Address, cancellationToken: ct)).Result),
                PlcVariableType.Int32 => PlcValue.Int32((await requestHandler.PlcProgramReadAsync<int>(variable.Address, cancellationToken: ct)).Result),
                PlcVariableType.Real => PlcValue.Real((await requestHandler.PlcProgramReadAsync<float>(variable.Address, cancellationToken: ct)).Result),
                _ => throw new PlcException(0, $"Unknown variable type: {variable.Type}")
            };

            return new PlcReadResult(variable, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PlcException)
        {
            throw SiemensPlcExceptionMapper.Map(ex);
        }
    }

    public async Task<IReadOnlyList<PlcReadResult>> ReadBatchAsync(IReadOnlyList<PlcVariable> variables, CancellationToken ct = default)
    {
        var results = new List<PlcReadResult>(variables.Count);
        foreach (var variable in variables)
        {
            results.Add(await ReadAsync(variable, ct));
        }

        return results;
    }

    public Task WriteAsync(PlcVariable variable, CancellationToken ct = default)
    {
        return WriteAsync(new[] { (variable, PlcValueForVariable(variable)) }, ct);
    }

    public async Task WriteAsync(IEnumerable<(PlcVariable Variable, PlcValue Value)> writes, CancellationToken ct = default)
    {
        try
        {
            var requestHandler = await connection.GetRequestHandlerAsync(ct);
            foreach (var (variable, value) in writes)
            {
                var convertedvalue = ConvertValue(variable.Type, value);
                await requestHandler.PlcProgramWriteAsync(variable.Address, convertedvalue, cancellationToken: ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PlcException)
        {
            throw SiemensPlcExceptionMapper.Map(ex);
        }
    }

    private static PlcValue PlcValueForVariable(PlcVariable variable)
    {
        return variable.Type switch
        {
            PlcVariableType.Bool => PlcValue.Bool(false),
            PlcVariableType.Int16 => PlcValue.Int16(0),
            PlcVariableType.Int32 => PlcValue.Int32(0),
            PlcVariableType.Real => PlcValue.Real(0),
            _ => throw new PlcException(0, $"Unknown variable type: {variable.Type}")
        };
    }

    private static object ConvertValue(PlcVariableType expectedType, PlcValue value)
    {
        if (value.Type != expectedType)
        {
            throw new PlcException(0, $"Value type {value.Type} does not match variable type {expectedType}");
        }

        return expectedType switch
        {
            PlcVariableType.Bool => value.AsBool(),
            PlcVariableType.Int16 => value.AsInt16(),
            PlcVariableType.Int32 => value.AsInt32(),
            PlcVariableType.Real => value.AsReal(),
            _ => throw new PlcException(0, $"Unknown variable type: {expectedType}")
        };
    }
}
