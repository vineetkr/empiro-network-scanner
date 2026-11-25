using System.Net.NetworkInformation;
using System.Net.Sockets;
using Empiro.Domain;
namespace Empiro.Network;
public sealed class AdapterDiscovery
{
    public IReadOnlyList<Adapter> GetAdapters() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => new Adapter(n.Id, n.Name, a.Address.ToString(), a.PrefixLength,
                n.GetIPProperties().GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString(),
                n.GetPhysicalAddress().ToString()))).ToArray();
}
