using Empiro.Domain;
using Empiro.Network;
namespace Empiro.IPAM;
public sealed record IpAllocation(string Ip, string State, string Device);
public sealed class IpamEngine
{
    public IEnumerable<IpAllocation> Inspect(string target, IEnumerable<Asset> assets, int maximum = 4096)
    {
        var known = assets.ToDictionary(a => a.Ip);
        foreach (var ip in new IpNetwork(target).Hosts(maximum))
            yield return known.TryGetValue(ip.ToString(), out var asset) ? new(ip.ToString(), "Observed", asset.DisplayName) : new(ip.ToString(), "Unobserved — availability unverified", "");
    }
}
