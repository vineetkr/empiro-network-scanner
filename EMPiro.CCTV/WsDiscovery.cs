using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Empiro.Core;
using Empiro.Domain;
namespace Empiro.CCTV;
/// <summary>Uncredentialed ONVIF WS-Discovery on each selected local IPv4 interface.</summary>
public sealed class WsDiscovery : ICctvDiscoveryProvider
{
    public static Asset? Parse(string xml, string sourceIp)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
        var doc = XDocument.Load(reader);
        var match = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ProbeMatch");
        if (match == null) return null;
        var types = match.Elements().FirstOrDefault(e => e.Name.LocalName == "Types")?.Value ?? "";
        var scopes = match.Elements().FirstOrDefault(e => e.Name.LocalName == "Scopes")?.Value ?? "";
        var isOnvif = types.Contains("NetworkVideoTransmitter", StringComparison.Ordinal) || scopes.Contains("onvif://", StringComparison.OrdinalIgnoreCase);
        if (!isOnvif) return null;
        var addresses = match.Elements().FirstOrDefault(e => e.Name.LocalName == "XAddrs")?.Value ?? "";
        var now = DateTimeOffset.UtcNow;
        var evidence = new List<Evidence> { new("ONVIF", "WS-Discovery advertisement (authentication not tested)", "WS-Discovery", 85, now), new("XAddrs", addresses, "WS-Discovery", 85, now) };
        string? Scope(string key) => scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(s => s.StartsWith($"onvif://www.onvif.org/{key}/", StringComparison.OrdinalIgnoreCase)) is { } s ? Uri.UnescapeDataString(s[(s.LastIndexOf('/') + 1)..]) : null;
        return new Asset { Ip = sourceIp, Hostname = Scope("name"), Model = Scope("hardware"), Kind = DeviceKind.Camera, Confidence = 85, Evidence = evidence };
    }
    public async IAsyncEnumerable<Asset> DiscoverAsync(IReadOnlyList<Adapter> adapters, TimeSpan duration, [EnumeratorCancellation] CancellationToken ct)
    {
        var found = new HashSet<string>();
        foreach (var adapter in adapters)
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Parse(adapter.Address), 0));
            udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.Parse(adapter.Address).GetAddressBytes());
            var id = Guid.NewGuid();
            var message = $"<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:a=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\"><s:Header><a:MessageID>uuid:{id}</a:MessageID><a:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To><a:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action></s:Header><s:Body><d:Probe xmlns:d=\"http://schemas.xmlsoap.org/ws/2005/04/discovery\"><d:Types xmlns:dn=\"http://www.onvif.org/ver10/network/wsdl\">dn:NetworkVideoTransmitter</d:Types></d:Probe></s:Body></s:Envelope>";
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(duration);
            try { await udp.SendAsync(Encoding.UTF8.GetBytes(message), new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702), ct); }
            catch (SocketException) { continue; }
            while (!deadline.IsCancellationRequested)
            {
                UdpReceiveResult response;
                try { response = await udp.ReceiveAsync(deadline.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { break; }
                catch (SocketException) { break; }
                Asset? asset = null;
                try { asset = Parse(Encoding.UTF8.GetString(response.Buffer), response.RemoteEndPoint.Address.ToString()); }
                catch (Exception ex) when (ex is XmlException or UriFormatException) { }
                if (asset != null && found.Add(asset.Ip)) yield return asset;
            }
            ct.ThrowIfCancellationRequested();
        }
    }
}
