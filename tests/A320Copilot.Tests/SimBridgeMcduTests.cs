using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading.Channels;
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
        await using var reader = new SimBridgeMcduReader(new(), () => connection);
        var result = await reader.ReadAsync();
        Assert.Equal("simbridge", result.Source);
        Assert.False(result.IsMock);
        Assert.Equal("INIT", result.Left.GetProperty("title").GetString());
        Assert.Equal("SBSP/SBRJ", result.Left.GetProperty("scratchpad").GetString());
        Assert.Equal(new[] { "requestUpdate", "requestUpdate" }, connection.Sent);
        Assert.Equal("/interfaces/v1/mcdu", connection.Endpoint!.AbsolutePath);
        Assert.False(connection.Disposed);
        await reader.DisposeAsync();
        Assert.True(connection.Disposed);
    }

    [Theory]
    [InlineData("mcduDisconnected")]
    [InlineData("update:{}")]
    [InlineData("update:{\"left\":{\"title\":\"INIT\"}}")]
    public async Task RejectsDisconnectedOrIncompleteData(string message)
    {
        await using var reader = new SimBridgeMcduReader(new(), () => new FakeConnection(message));
        await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync());
    }

    [Fact]
    public async Task RejectsMalformedJson()
    {
        await using var reader = new SimBridgeMcduReader(new(), () => new FakeConnection("update:{"));
        await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync());
    }

    [Fact]
    public async Task DeadlineFailsWithoutFabricatedScreen()
    {
        await using var reader = new SimBridgeMcduReader(new() { TimeoutSeconds = 1 }, () => new FakeConnection());
        var error = await Record.ExceptionAsync(() => reader.ReadAsync());
        Assert.True(error is TimeoutException || error is IOException && error.Message.Contains("Timed out"));
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var reader = new SimBridgeMcduReader(new(), () => new FakeConnection());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(cancellation.Token));
    }

    [Fact]
    public async Task MockDoesNotOpenConnection()
    {
        await using var reader = new SimBridgeMcduReader(new(), () => throw new Exception("Must not connect"));
        var tools = new McduTools(new() { Mode = "Mock" }, reader);
        var json = await tools.GetMcduState(default);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("IsMock").GetBoolean());
        Assert.Equal("", document.RootElement.GetProperty("Left").GetProperty("title").GetString());
        using var status = JsonDocument.Parse(tools.GetMcduStatus());
        Assert.False(status.RootElement.GetProperty("Running").GetBoolean());
        using var events = JsonDocument.Parse(tools.GetRecentMcduEvents());
        Assert.Empty(events.RootElement.GetProperty("Events").EnumerateArray());
    }

    [Fact]
    public async Task ToolReportsRealConnectionFailure()
    {
        await using var reader = new SimBridgeMcduReader(new(), () => new FakeConnection("mcduDisconnected"));
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

    [Fact]
    public async Task ConcurrentReadsReuseOneConnectionAndCachedScreen()
    {
        var opens = 0;
        var connection = new FakeConnection(Update);
        await using var reader = new SimBridgeMcduReader(new(), () => { Interlocked.Increment(ref opens); return connection; });
        var screens = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => reader.ReadAsync()));
        Assert.Equal(1, opens);
        Assert.Single(connection.Sent);
        Assert.All(screens, screen => Assert.Equal(screens[0].ReceivedAtUtc, screen.ReceivedAtUtc));
        Assert.All(screens, screen => Assert.Equal("fresh", screen.Freshness!.Validity));
        Assert.False(connection.Disposed);
        Assert.Equal(1, connection.MaximumConcurrentReceives);
    }

    [Fact]
    public async Task CancelingOneClientDoesNotCancelSharedReceive()
    {
        var connection = new FakeConnection();
        await using var reader = new SimBridgeMcduReader(new(), () => connection);
        using var canceled = new CancellationTokenSource();
        var pending = reader.ReadAsync(canceled.Token);
        await WaitUntil(() => !connection.Sent.IsEmpty);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(connection.Disposed);
        connection.Push(Update);
        Assert.Equal("INIT", (await reader.ReadAsync()).Left.GetProperty("title").GetString());
        Assert.Equal(1, reader.GetStatus().SessionId);
    }

    [Fact]
    public async Task BackgroundRefreshConfirmsUnchangedScreensWithoutCreatingChanges()
    {
        var connection = new FakeConnection { ReplyToRequests = Update };
        await using var reader = new SimBridgeMcduReader(new() { RefreshIntervalMilliseconds = 100 }, () => connection);
        await reader.ReadAsync();
        await WaitUntil(() => reader.GetStatus().ScreensReceived >= 3);
        Assert.True(reader.GetStatus().UpdateRequestsSent >= 3);
        Assert.Equal(1, reader.GetStatus().ConnectionAttempts);
        Assert.Equal(3, reader.GetEvents().Events.Count);
        Assert.All(connection.Sent, command => Assert.Equal("requestUpdate", command));
    }

    [Fact]
    public async Task StaleScreenIsRejectedByMcpAndFreshUpdateRecoversSameSocket()
    {
        var connection = new FakeConnection(Update);
        await using var reader = new SimBridgeMcduReader(new() { MaximumScreenAgeMilliseconds = 100 }, () => connection);
        var tools = new McduTools(new() { Mode = "Real" }, reader);
        await tools.GetMcduState(default);
        await WaitUntil(() => reader.GetStatus().Validity == "stale");
        var error = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => tools.GetMcduState(default));
        Assert.Contains("stale", error.Message);
        Assert.Contains("No mock data was returned", error.Message);
        connection.Push(Update);
        await WaitUntil(() => reader.GetStatus().Validity == "fresh");
        Assert.Equal(1, reader.GetStatus().SessionId);
        Assert.Contains(reader.GetEvents().Events, e => e.Type == "data_fresh");
    }

    [Fact]
    public async Task DisconnectReconnectsAndResetsScreenBaseline()
    {
        var first = new FakeConnection(Update);
        var second = new FakeConnection(Update.Replace("INIT", "FUEL PRED"));
        var opens = 0;
        await using var reader = new SimBridgeMcduReader(new() { ReconnectDelayMilliseconds = 100 },
            () => Interlocked.Increment(ref opens) == 1 ? first : second);
        await reader.ReadAsync();
        first.Push("mcduDisconnected");
        await WaitUntil(() => reader.GetStatus().ConnectionState == "disconnected");
        await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync());
        await WaitUntil(() => reader.GetStatus().SessionId == 2 && reader.GetStatus().Validity == "fresh");
        Assert.True(first.Disposed);
        Assert.Equal("FUEL PRED", (await reader.ReadAsync()).Left.GetProperty("title").GetString());
        Assert.Equal(2, reader.GetEvents().Events.Count(e => e.Type == "baseline_created"));
        Assert.DoesNotContain(reader.GetEvents().Events, e => e.Type == "page_changed");
    }

    [Fact]
    public async Task EchoesCannotKeepSilentAircraftScreenAlive()
    {
        var first = new FakeConnection(Update) { ReplyToRequests = "requestUpdate" };
        var second = new FakeConnection(Update);
        var opens = 0;
        await using var reader = new SimBridgeMcduReader(new()
            { TimeoutSeconds = 1, MaximumScreenAgeMilliseconds = 100, RefreshIntervalMilliseconds = 100, ReconnectDelayMilliseconds = 50 },
            () => Interlocked.Increment(ref opens) == 1 ? first : second);
        await reader.ReadAsync();
        await WaitUntil(() => reader.GetStatus().SessionId >= 2 && reader.GetStatus().Validity == "fresh");
        Assert.True(first.Disposed);
        Assert.Contains(reader.GetEvents().Events, e => e.Type == "data_stale");
        Assert.Contains(reader.GetEvents().Events, e => e.Type == "disconnected" && e.Description.Contains("Timed out"));
    }

    private static async Task WaitUntil(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (!predicate()) await Task.Delay(5, deadline.Token);
    }

    private sealed class FakeConnection(params string[] messages) : IMcduConnection
    {
        private readonly Channel<string> pending = CreateChannel(messages);
        private int activeReceives, maximumReceives;
        public ConcurrentQueue<string> Sent { get; } = new();
        public string? ReplyToRequests { get; init; }
        public int MaximumConcurrentReceives => maximumReceives;
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
            Sent.Enqueue(message);
            if (ReplyToRequests is not null) Push(ReplyToRequests);
            return Task.CompletedTask;
        }
        public async Task<string> ReceiveAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref activeReceives);
            maximumReceives = Math.Max(maximumReceives, active);
            try { return await pending.Reader.ReadAsync(cancellationToken); }
            finally { Interlocked.Decrement(ref activeReceives); }
        }
        public void Push(string message) => pending.Writer.TryWrite(message);
        private static Channel<string> CreateChannel(string[] initial)
        {
            var channel = Channel.CreateUnbounded<string>();
            foreach (var message in initial) channel.Writer.TryWrite(message);
            return channel;
        }
        public void Dispose() => Disposed = true;
    }
}
