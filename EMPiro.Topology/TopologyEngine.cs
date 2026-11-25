using Empiro.Core;
using Empiro.Domain;
namespace Empiro.Topology;
/// <summary>Only explicit neighbor observations create physical relationships.</summary>
public sealed class TopologyEngine : ITopologyProvider
{
    public IReadOnlyList<TopologyEdge> Correlate(IEnumerable<Asset> assets) => assets.SelectMany(a => a.Evidence
        .Where(e => e.Property == "Neighbor" && e.Source is "LLDP" or "CDP" or "SNMP FDB")
        .Select(e => new TopologyEdge(a.Ip, e.Value, e.Source, e.Confidence, e.ObservedAt))).Distinct().ToArray();
}
