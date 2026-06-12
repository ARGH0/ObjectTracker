using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ObjectTracker.UI.Desktop.Plc.Contracts;
using ObjectTracker.UI.Desktop.Plc.Model;
using Xunit;

namespace ObjectTracker.UI.Desktop.Tests;

public sealed class PlcTransitionWriterTests
{
    [Fact]
    public async Task WriteTransitionAsync_RedTrain_WritesRegionVariableInDataTableWithRedValue()
    {
        var plcClient = new RecordingPlcClient();
        var log = new List<string>();
        var writer = new PlcTransitionWriter(plcClient, log.Add);

        await writer.WriteTransitionAsync("randomnr", "Red", isPlcConnected: true);

        var write = Assert.Single(plcClient.Writes);
        Assert.Equal("data.randomnr", write.Variable.Address);
        Assert.Equal(PlcVariableType.Int32, write.Variable.Type);
        Assert.Equal(2, write.Value.AsInt32());
        Assert.Contains(log, entry => entry.Contains("variable=data.randomnr", System.StringComparison.Ordinal) &&
                                      entry.Contains("value=2", System.StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Blue", 1)]
    [InlineData("Red", 2)]
    [InlineData("Green", 3)]
    [InlineData("White", 4)]
    public async Task WriteTransitionAsync_KnownTrainColor_WritesConfiguredColorValue(string trainColor, int expectedValue)
    {
        var plcClient = new RecordingPlcClient();
        var writer = new PlcTransitionWriter(plcClient, _ => { });

        await writer.WriteTransitionAsync("signal", trainColor, isPlcConnected: true);

        var write = Assert.Single(plcClient.Writes);
        Assert.Equal(expectedValue, write.Value.AsInt32());
    }

    [Fact]
    public async Task WriteTransitionAsync_WhenPlcDisconnected_LogsVariableAndValueWithoutWriting()
    {
        var plcClient = new RecordingPlcClient();
        var log = new List<string>();
        var writer = new PlcTransitionWriter(plcClient, log.Add);

        await writer.WriteTransitionAsync("randomnr", "Blue", isPlcConnected: false);

        Assert.Empty(plcClient.Writes);
        Assert.Contains(log, entry => entry.Contains("variable=data.randomnr", System.StringComparison.Ordinal) &&
                                      entry.Contains("value=1", System.StringComparison.Ordinal) &&
                                      entry.Contains("reason=not connected", System.StringComparison.Ordinal));
    }

    private sealed class RecordingPlcClient : IPlcClient
    {
        public List<(PlcVariable Variable, PlcValue Value)> Writes { get; } = new();

        public Task<PlcReadResult> ReadAsync(PlcVariable variable, CancellationToken ct = default)
            => throw new System.NotImplementedException();

        public Task<IReadOnlyList<PlcReadResult>> ReadBatchAsync(IReadOnlyList<PlcVariable> variables, CancellationToken ct = default)
            => throw new System.NotImplementedException();

        public Task WriteAsync(PlcVariable variable, CancellationToken ct = default)
            => throw new System.NotImplementedException();

        public Task WriteAsync(IEnumerable<(PlcVariable Variable, PlcValue Value)> writes, CancellationToken ct = default)
        {
            Writes.AddRange(writes);
            return Task.CompletedTask;
        }
    }
}
