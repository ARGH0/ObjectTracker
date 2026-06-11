using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Model;

namespace ObjectTracker.UI.Desktop.Plc.Implementation;

internal sealed class PlcSessionManager : IPlcSessionManager
{
    private readonly HttpClient _httpClient;
    private readonly PlcClientConfig _config;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
    private string? _token;

    public PlcSessionManager(HttpClient httpClient, PlcClientConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        return !string.IsNullOrEmpty(_token);
    }

    public async Task EnsureAuthenticatedAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            return;
        }

        await LoginAsync(ct);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        _token = null;
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        var request = new
        {
            id = 0,
            jsonrpc = "2.0",
            method = "Api.Login",
            @params = new
            {
                user = _config.User,
                password = _config.Password
            }
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_config.JsonRpcEndpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var rpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson, _jsonOptions);

        if (rpcResponse?.Error is not null)
        {
            throw new PlcException(rpcResponse.Error.Code, rpcResponse.Error.Message);
        }

        _token = ExtractToken(response);
    }

    private static string? ExtractToken(HttpResponseMessage response)
    {
        var values = response.Headers.GetValues("X-Auth-Token").ToArray();
        return values.Length > 0 ? values[0] : null;
    }
}
