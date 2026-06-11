using System.Text.Json.Serialization;

namespace ObjectTracker.UI.Desktop.Plc.Implementation;

internal sealed class JsonRpcRequest
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("jsonrpc")]
    public string Version { get; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonRpcParams? Params { get; set; }
}

internal sealed class JsonRpcParams
{
    [JsonPropertyName("var")]
    public string? Variable { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

internal sealed class JsonRpcResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("jsonrpc")]
    public string Version { get; set; } = "2.0";

    [JsonPropertyName("result")]
    public JsonRpcResult? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

internal sealed class JsonRpcResult
{
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

internal sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
