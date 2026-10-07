using System.Net.WebSockets;
using System.Text;

namespace A320Copilot.Bridge;

public sealed class McduWebSocketConnection : IMcduConnection
{
    private readonly ClientWebSocket socket = new();
    private const int MaximumMessageBytes = 256 * 1024;

    public Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
        => socket.ConnectAsync(endpoint, cancellationToken);

    public Task SendAsync(string message, CancellationToken cancellationToken)
        => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
            WebSocketMessageType.Text, true, cancellationToken);

    public async Task<string> ReceiveAsync(CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (part.MessageType == WebSocketMessageType.Close)
                throw new IOException("SimBridge closed the MCDU connection.");
            if (part.MessageType != WebSocketMessageType.Text)
                throw new IOException("Unexpected binary MCDU message.");
            if (message.Length + part.Count > MaximumMessageBytes)
                throw new IOException("SimBridge MCDU message exceeds 256 KiB.");
            message.Write(buffer, 0, part.Count);
        } while (!part.EndOfMessage);
        return new UTF8Encoding(false, true).GetString(message.ToArray());
    }

    public void Dispose() => socket.Dispose();
}
