using Empiro.Domain;
namespace Empiro.Core;

public interface INetworkScanner
{
    IAsyncEnumerable<Asset> ScanAsync(ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}
public interface IInventoryStore
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> LoadAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NetworkEvent>> SaveAsync(Asset asset, CancellationToken ct = default);
    Task SaveSessionAsync(ScanSession session, CancellationToken ct = default);
    Task<IReadOnlyList<ScanSession>> SessionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NetworkEvent>> EventsAsync(CancellationToken ct = default);
    Task UpdateMetadataAsync(string ip, bool favorite, string notes, string tags, CancellationToken ct = default);
}
public interface ICctvDiscoveryProvider
{
    IAsyncEnumerable<Asset> DiscoverAsync(IReadOnlyList<Adapter> adapters, TimeSpan duration, CancellationToken ct);
}
public interface IServiceDetector { Task<Service> DetectAsync(string ip, int port, int timeoutMs, CancellationToken ct); }
public interface IDeviceFingerprintProvider { Asset Identify(Asset asset); }
public interface ISecurityCheck { IEnumerable<Finding> Assess(Asset asset); }
public interface ITopologyProvider { IReadOnlyList<TopologyEdge> Correlate(IEnumerable<Asset> assets); }
