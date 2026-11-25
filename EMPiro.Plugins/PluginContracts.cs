using Empiro.Domain;
namespace Empiro.Plugins;
/// <summary>Extension metadata. Plugins must be explicitly trusted before host registration.</summary>
public interface IEmpiroPlugin { string Name { get; } Version Version { get; } }
public interface ISnmpProvider : IEmpiroPlugin
{
    Task<IReadOnlyList<Evidence>> QueryAsync(string host, Guid credentialReference, CancellationToken ct);
}
public interface ICloudDiscoveryProvider : IEmpiroPlugin
{
    IAsyncEnumerable<Asset> DiscoverAsync(Guid credentialReference, CancellationToken ct);
}
