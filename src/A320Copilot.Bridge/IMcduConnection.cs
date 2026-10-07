namespace A320Copilot.Bridge;

public interface IMcduConnection : IDisposable
{
    Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken);
    Task SendAsync(string message, CancellationToken cancellationToken);
    Task<string> ReceiveAsync(CancellationToken cancellationToken);
}
