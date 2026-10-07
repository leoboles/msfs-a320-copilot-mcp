using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Mcp;
using Xunit;

namespace A320Copilot.Tests;

public sealed class SimBridgeMcduTests
{
    private const string Update = "update:{\"left\":{\"title\":\"INIT\",\"lines\":[[\"FROM/TO\",\"\",\"\"]],\"scratchpad\":\"SBSP/SBRJ\"}}";

    [Fact]
    public async Task RequestsFreshScreenAndRetriesWhenAircraftJoins()
    {
        var connection = new FakeConnection("requestUpdate", "mcduConnected", Update);
        var reader = new SimBridgeMcduReader(new(), () => connection);
        var result = await reader.ReadAsync();
        Assert.Equal("simbridge", result.Source);
        Assert.False(result.IsMock);
        Assert.Equal("INIT", result.Left.GetProperty("title").GetString());
        Assert.Equal("SBSP/SBRJ", result.Left.GetProperty("scratchpad").GetString());
        Assert.Equal(new[] { "requestUpdate", "requestUpdate" }, connection.Sent);
        Assert.Equal("/interfaces/v1/mcdu", connection.Endpoint!.AbsolutePath);
        Assert.True(connection.Disposed);
    }

    [Theory]
    [InlineData("mcduDisconnected")]
    [InlineData("update:{}")]
    [InlineData("update:{\"left\":{\"title\":\"INIT\"}}")]
    public async Task RejectsDisconnectedOrIncompleteData(string message)
    {
        var reader = new SimBridgeMcduReader(new(), () => new FakeConnection(message));
        await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync());
    }

    [Fact]
    public async Task RejectsMalformedJson()
    {
        var reader = new SimBridgeMcduReader(new(), () => new FakeConnection("update:{"));
        await Assert.ThrowsAnyAsync<JsonException>(() => reader.ReadAsync());
    }

    [Fact]
    public async Task DeadlineFailsWithoutFabricatedScreen()
    {
        var reader = new SimBridgeMcduReader(new() { TimeoutSeconds = 1 }, () => new FakeConnection());
        await Assert.ThrowsAsync<TimeoutException>(() => reader.ReadAsync());
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = new SimBridgeMcduReader(new(), () => new FakeConnection());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(cancellation.Token));
    }

    [Fact]
    public async Task MockDoesNotOpenConnection()
    {
        var reader = new SimBridgeMcduReader(new(), () => throw new Exception("Must not connect"));
        var json = await new McduTools(new() { Mode = "Mock" }, reader).GetMcduState(default);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("IsMock").GetBoolean());
        Assert.Equal("", document.RootElement.GetProperty("Left").GetProperty("title").GetString());
    }

    [Fact]
    public async Task ToolReportsRealConnectionFailure()
    {
        var reader = new SimBridgeMcduReader(new(), () => new FakeConnection("mcduDisconnected"));
        var error = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(
            () => new McduTools(new() { Mode = "Real" }, reader).GetMcduState(default));
        Assert.Contains("No mock data was returned", error.Message);
    }

    [Theory]
    [InlineData("http://localhost:8380", 10)]
    [InlineData("ws://localhost:8380/interfaces/v1/mcdu", 0)]
    [InlineData("ws://localhost:8380/interfaces/v1/mcdu", 121)]
    public void RejectsInvalidSettings(string url, int timeout)
    {
        Assert.Throws<ArgumentException>(() => new SimBridgeSettings
            { McduWebSocketUrl = url, TimeoutSeconds = timeout }.Validate());
    }

    private sealed class FakeConnection(params string[] messages) : IMcduConnection
    {
        private readonly Queue<string> pending = new(messages);
        public List<string> Sent { get; } = [];
        public Uri? Endpoint { get; private set; }
        public bool Disposed { get; private set; }
        public Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Endpoint = endpoint;
            return Task.CompletedTask;
        }
        public Task SendAsync(string message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
        public async Task<string> ReceiveAsync(CancellationToken cancellationToken)
        {
            if (pending.TryDequeue(out var message)) return message;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException();
        }
        public void Dispose() => Disposed = true;
    }
}
