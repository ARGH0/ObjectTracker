using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop.Plc.Implementation;

internal sealed class PlcClient : IPlcClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
    private int _idCounter;

    public PlcClient(HttpClient httpClient, PlcClientConfig config)
    {
        _httpClient = httpClient;
        Endpoint = config.JsonRpcEndpoint;
    }

    private string Endpoint { get; }

    public async Task<PlcReadResult> ReadAsync(PlcVariable variable, CancellationToken ct = default)
    {
        var response = await SendRequestAsync(new JsonRpcRequest
        {
            Id = NextId(),
            Method = "PlcProgram.Read",
            Params = new JsonRpcParams { Variable = variable.Address }
        }, ct);

        if (response.Error is not null)
        {
            throw new PlcException(response.Error.Code, response.Error.Message);
        }

        var value = ParseValue(variable.Type, response.Result?.Value);
        return new PlcReadResult(variable, value);
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

    public async Task WriteAsync(PlcVariable variable, CancellationToken ct = default)
    {
        var response = await SendRequestAsync(new JsonRpcRequest
        {
            Id = NextId(),
            Method = "PlcProgram.Write",
            Params = new JsonRpcParams { Variable = variable.Address }
        }, ct);

        if (response.Error is not null)
        {
            throw new PlcException(response.Error.Code, response.Error.Message);
        }
    }

    public async Task WriteAsync(IEnumerable<(PlcVariable Variable, PlcValue Value)> writes, CancellationToken ct = default)
    {
        foreach (var (variable, _) in writes)
        {
            await WriteAsync(variable, ct);
        }
    }

    private async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(Endpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var rpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson, _jsonOptions)
            ?? throw new PlcException(0, "Empty response from PLC");

        if (rpcResponse.Error is not null)
        {
            throw new PlcException(rpcResponse.Error.Code, rpcResponse.Error.Message);
        }

        return rpcResponse;
    }

    private static PlcValue ParseValue(PlcVariableType type, object? raw)
    {
        return type switch
        {
            PlcVariableType.Bool => PlcValue.Bool(raw is true),
            PlcVariableType.Int16 => PlcValue.Int16(Convert.ToInt16(raw)),
            PlcVariableType.Int32 => PlcValue.Int32(Convert.ToInt32(raw)),
            PlcVariableType.Real => PlcValue.Real(Convert.ToSingle(raw)),
            _ => throw new PlcException(0, $"Unknown variable type: {type}")
        };
    }

    private int NextId() => Interlocked.Increment(ref _idCounter);

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public sealed class PlcException : Exception
{
    public PlcException(int code, string message) : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}
