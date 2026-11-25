using System.Net.Sockets;
namespace Empiro.Network;
public sealed class PortScanner
{
    public async Task<bool> IsOpenAsync(string ip, int port, int timeoutMs, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(timeoutMs);
        try { await client.ConnectAsync(ip, port, timeout.Token); return true; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (SocketException) { return false; }
    }
}
