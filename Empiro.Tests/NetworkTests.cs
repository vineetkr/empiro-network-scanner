using System.Net;
using System.Net.Sockets;
using System.Text;
using Empiro.CCTV;
using Empiro.Discovery;
using Empiro.Domain;
using Empiro.Inventory;
using Empiro.Network;
using Xunit;
namespace Empiro.Tests;
public sealed class NetworkTests
{
    [Theory]
    [InlineData("192.168.1.25/24", "192.168.1.0/24", 254)]
    [InlineData("10.0.0.1/31", "10.0.0.0/31", 2)]
    [InlineData("255.255.255.255/32", "255.255.255.255/32", 1)]
    public void CidrNormalizesAndCounts(string input, string expected, int count)
    { var network = new IpNetwork(input); Assert.Equal(expected, network.ToString()); Assert.Equal(count, network.Hosts(65536).Count()); }
    [Theory]
    [InlineData("10.0.0.1/33")][InlineData("::1/64")][InlineData("bad")][InlineData("10.0.0.1/-1")]
    public void InvalidTargetsRejected(string target) => Assert.Throws<ArgumentException>(() => new IpNetwork(target));
    [Fact] public void LargeTargetFailsBeforeEnumeration() => Assert.Throws<ArgumentException>(() => new IpNetwork("0.0.0.0/0").Hosts(65536).ToArray());
    [Fact] public void SubnetMembership() { var n = new IpNetwork("192.168.4.1/24"); Assert.True(n.Contains(IPAddress.Parse("192.168.4.255"))); Assert.False(n.Contains(IPAddress.Parse("192.168.5.1"))); Assert.False(n.Contains(IPAddress.IPv6Loopback)); }
    [Fact] public async Task TcpConnectFindsLocalListener()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        Assert.True(await new PortScanner().IsOpenAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, 1000, default));
    }
    [Fact] public async Task CancellationPropagates()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PortScanner().IsOpenAsync("127.0.0.1", 1, 1000, cts.Token));
    }
    [Fact] public async Task ServiceDetectionReadsProtocolResponse()
    {
        // Well-known port hints must not be misrepresented as verified protocols.
        var s = await new ServiceDetectionEngine().DetectAsync("127.0.0.1", 3389, 100, default);
        Assert.Equal(35, s.Confidence); Assert.Contains("unverified", s.Evidence);
    }
    [Fact] public void OnvifParsesSafeProbe()
    {
        var asset = WsDiscovery.Parse("<Envelope><ProbeMatches><ProbeMatch><Types>dn:NetworkVideoTransmitter</Types><Scopes>onvif://www.onvif.org/name/Front%20Door onvif://www.onvif.org/hardware/Model1</Scopes><XAddrs>http://192.0.2.4/onvif/device_service</XAddrs></ProbeMatch></ProbeMatches></Envelope>", "192.0.2.4");
        Assert.NotNull(asset); Assert.Equal("Front Door", asset.Hostname); Assert.Equal("Model1", asset.Model); Assert.Equal(85, asset.Confidence);
    }
    [Fact] public void OnvifRejectsExternalEntities() => Assert.Throws<System.Xml.XmlException>(() => WsDiscovery.Parse("<!DOCTYPE root [<!ENTITY x SYSTEM 'file:///C:/secret'>]><root>&x;</root>", "192.0.2.1"));
    [Fact] public void UnrelatedMulticastIsNotCamera() => Assert.Null(WsDiscovery.Parse("<Envelope><ProbeMatch><Types>Printer</Types></ProbeMatch></Envelope>", "192.0.2.1"));
    [Fact] public void RtspPortDoesNotProveCamera()
    { var result = new FingerprintEngine().Identify(new Asset { Ip = "192.0.2.1", Services = [new(554, "TCP", "RTSP", null, 35, "Port hint")] }); Assert.Equal(DeviceKind.Unknown, result.Kind); }
    [Fact] public async Task ScannerRejectsTooManyHostsBeforeSending()
    {
        var scanner = new NativeScanner(new PortScanner(), new ServiceDetectionEngine(), new FingerprintEngine(), new AdapterDiscovery(), new NeighborCache());
        await Assert.ThrowsAsync<ArgumentException>(async () => { await foreach (var _ in scanner.ScanAsync(new ScanOptions("10.0.0.0/8", [80], MaximumHosts: 16), null, default)) { } });
    }
}
