using System.Net.Sockets;
using System.Text;
using Empiro.Core;
using Empiro.Domain;
namespace Empiro.Inventory;

public sealed class ServiceDetectionEngine : IServiceDetector
{
    public async Task<Service> DetectAsync(string ip, int port, int timeoutMs, CancellationToken ct)
    {
        var hint = port switch { 22 => "SSH", 80 or 8080 or 8000 => "HTTP", 443 or 8443 => "HTTPS", 554 => "RTSP", 445 => "SMB", 3389 => "RDP", 21 => "FTP", 23 => "Telnet", 25 => "SMTP", 53 => "DNS", _ => "Unknown" };
        if (port is not (22 or 21 or 25 or 80 or 8080 or 8000 or 554)) return new(port, "TCP", hint, null, 35, "Port convention; protocol unverified");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(timeoutMs);
        try
        {
            using var client = new TcpClient(); await client.ConnectAsync(ip, port, timeout.Token);
            using var stream = client.GetStream();
            if (port is 80 or 8080 or 8000) await stream.WriteAsync(Encoding.ASCII.GetBytes($"HEAD / HTTP/1.0\r\nHost: {ip}\r\nConnection: close\r\n\r\n"), timeout.Token);
            if (port == 554) await stream.WriteAsync(Encoding.ASCII.GetBytes($"OPTIONS rtsp://{ip}/ RTSP/1.0\r\nCSeq: 1\r\n\r\n"), timeout.Token);
            var buffer = new byte[4096]; var count = await stream.ReadAsync(buffer, timeout.Token);
            var banner = Encoding.ASCII.GetString(buffer, 0, count);
            var verified = banner.StartsWith("HTTP/", StringComparison.Ordinal) ? "HTTP" : banner.StartsWith("RTSP/", StringComparison.Ordinal) ? "RTSP" : banner.StartsWith("SSH-", StringComparison.Ordinal) ? "SSH" : null;
            if (verified != null)
            {
                var version = verified == "SSH" ? banner.Split('\n')[0].Trim() : banner.Split('\n').FirstOrDefault(l => l.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))?.Trim();
                return new(port, "TCP", verified, version?[..Math.Min(version.Length, 160)], 95, "Protocol response");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is SocketException or IOException) { }
        return new(port, "TCP", hint, null, 35, "Port convention; protocol unverified");
    }
}
public sealed class FingerprintEngine : IDeviceFingerprintProvider
{
    public Asset Identify(Asset asset)
    {
        if (asset.Evidence.Any(e => e.Source == "WS-Discovery" && e.Property == "ONVIF"))
            return asset with { Kind = DeviceKind.Camera, Confidence = 85 };
        // RTSP alone also occurs on recorders and media servers; retain Unknown until stronger evidence exists.
        return asset with { Kind = DeviceKind.Unknown, Confidence = 0 };
    }
}
public sealed class CorrelationEngine
{
    public Asset Merge(Asset previous, Asset observed)
    {
        // A reused address must not inherit the former device's identity or notes.
        if (previous.Mac != null && observed.Mac != null && !string.Equals(previous.Mac, observed.Mac, StringComparison.OrdinalIgnoreCase)) return observed;
        return observed with
    {
        Mac = observed.Mac ?? previous.Mac, Hostname = observed.Hostname ?? previous.Hostname,
        Vendor = observed.Vendor ?? previous.Vendor, Model = observed.Model ?? previous.Model,
        FirstSeen = previous.FirstSeen, Favorite = previous.Favorite, Notes = previous.Notes, Tags = previous.Tags,
        Kind = observed.Confidence >= previous.Confidence ? observed.Kind : previous.Kind,
        Confidence = Math.Max(previous.Confidence, observed.Confidence),
        Services = previous.Services.Where(s => s.Protocol != "TCP" || !observed.ScannedTcpPorts.Contains(s.Port)).Concat(observed.Services).GroupBy(s => (s.Port, s.Protocol)).Select(g => g.Last()).ToList(),
        Evidence = previous.Evidence.Concat(observed.Evidence).GroupBy(e => (e.Property, e.Value, e.Source)).Select(g => g.Last()).ToList()
        };
    }
}
