using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Empiro.Core;
using Empiro.Domain;
using Empiro.Network;
namespace Empiro.Discovery;

public sealed class NativeScanner(PortScanner ports, IServiceDetector services, IDeviceFingerprintProvider fingerprints, AdapterDiscovery adapters, NeighborCache neighbors) : INetworkScanner
{
    public async IAsyncEnumerable<Asset> ScanAsync(ScanOptions options, IProgress<ScanProgress>? progress, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        options.Validate();
        var network = new IpNetwork(options.Target);
        // Validate host count before scheduling any network traffic.
        if (network.Count > (ulong)options.MaximumHosts) throw new ArgumentException("Target exceeds the configured host limit.");
        var localNetworks = adapters.GetAdapters().Select(a => new IpNetwork($"{a.Address}/{a.Prefix}")).ToArray();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = linked.Token;
        var results = Channel.CreateBounded<Asset>(new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
        var work = ProduceAsync();
        try { await foreach (var asset in results.Reader.ReadAllAsync(ct)) yield return asset; }
        finally { linked.Cancel(); try { await work; } catch (OperationCanceledException) when (ct.IsCancellationRequested) { } }

        async Task ProduceAsync()
        {
            int completed = 0, found = 0;
            try
            {
                await Parallel.ForEachAsync(network.Hosts(options.MaximumHosts), new ParallelOptions { MaxDegreeOfParallelism = options.Concurrency, CancellationToken = ct }, async (ip, token) =>
                {
                    var address = ip.ToString();
                    bool alive = false; var open = new List<int>();
                    using (var ping = new Ping())
                    {
                        try { alive = (await ping.SendPingAsync(ip, TimeSpan.FromMilliseconds(options.TimeoutMs), cancellationToken: token)).Status == IPStatus.Success; }
                        catch (PingException) { }
                    }
                    // ICMP-blocked devices can still be found through a small TCP discovery set.
                    if (!alive)
                        foreach (var p in options.Ports.Take(8).Concat(new[] { 80, 443, 554, 445, 22 }).Distinct())
                            if (await ports.IsOpenAsync(address, p, options.TimeoutMs, token)) { alive = true; open.Add(p); break; }
                    if (alive)
                    {
                        foreach (var p in options.Ports.Distinct())
                            if (!open.Contains(p) && await ports.IsOpenAsync(address, p, options.TimeoutMs, token)) open.Add(p);
                        var detected = new List<Service>();
                        foreach (var p in open) detected.Add(await services.DetectAsync(address, p, options.TimeoutMs, token));
                        string? hostname = null;
                        try { hostname = (await Dns.GetHostEntryAsync(address).WaitAsync(TimeSpan.FromMilliseconds(options.TimeoutMs), token)).HostName; }
                        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or TimeoutException or ArgumentException) { }
                        // SendARP is synchronous; isolate it from the UI and never apply it to routed hosts.
                        var mac = localNetworks.Any(n => n.Contains(ip)) ? await Task.Run(() => neighbors.Resolve(ip), token) : null;
                        var evidence = new List<Evidence> { new("Reachability", "Online", "ICMP or successful TCP connection", 100, DateTimeOffset.UtcNow) };
                        if (hostname != null) evidence.Add(new("Hostname", hostname, "DNS PTR", 85, DateTimeOffset.UtcNow));
                        if (mac != null) evidence.Add(new("MAC", mac, "Windows ARP resolver", 95, DateTimeOffset.UtcNow));
                        await results.Writer.WriteAsync(fingerprints.Identify(new Asset { Ip = address, Mac = mac, Hostname = hostname, Services = detected, ScannedTcpPorts = options.Ports.Distinct().ToList(), Evidence = evidence }), token);
                        Interlocked.Increment(ref found);
                    }
                    progress?.Report(new(Interlocked.Increment(ref completed), (int)network.Count, Volatile.Read(ref found), "Host and service discovery"));
                });
                results.Writer.TryComplete();
            }
            catch (Exception ex) { results.Writer.TryComplete(ex); }
        }
    }
}
