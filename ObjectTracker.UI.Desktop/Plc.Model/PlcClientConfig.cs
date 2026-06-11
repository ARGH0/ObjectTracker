namespace ObjectTracker.UI.Desktop.Plc.Model;

public sealed record PlcClientConfig(
    string BaseUrl,
    string User,
    string Password)
{
    public string JsonRpcEndpoint => BaseUrl.TrimEnd('/') + "/api/jsonrpc";
}
