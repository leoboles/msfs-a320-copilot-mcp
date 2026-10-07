using System.Text.Json;
using A320Copilot.Domain;

namespace A320Copilot.Bridge;

public sealed class SimBridgeMcduReader(SimBridgeSettings settings, Func<IMcduConnection> connectionFactory)
{
    public async Task<McduState> ReadAsync(CancellationToken cancellationToken = default)
    {
        settings.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            using var connection = connectionFactory();
            await connection.ConnectAsync(new Uri(settings.McduWebSocketUrl), deadline.Token);
            await connection.SendAsync("requestUpdate", deadline.Token);
            while (true)
            {
                var message = await connection.ReceiveAsync(deadline.Token);
                if (message == "mcduDisconnected")
                    throw new IOException("The aircraft MCDU disconnected from SimBridge.");
                if (message == "mcduConnected")
                {
                    await connection.SendAsync("requestUpdate", deadline.Token);
                    continue;
                }
                if (!message.StartsWith("update:", StringComparison.Ordinal)) continue;
                using var document = JsonDocument.Parse(message["update:".Length..]);
                if (!document.RootElement.TryGetProperty("left", out var left) ||
                    left.ValueKind != JsonValueKind.Object ||
                    !left.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array ||
                    !left.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String)
                    throw new IOException("Invalid SimBridge MCDU update: expected left screen with title and lines.");
                return new McduState("simbridge", false, DateTimeOffset.UtcNow, "left_mcdu_screen_only", left.Clone());
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for a fresh MCDU update. Check SimBridge and the loaded FlyByWire aircraft.");
        }
    }
}
