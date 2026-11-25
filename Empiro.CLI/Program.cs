using Empiro.Discovery;
using Empiro.Domain;
using Empiro.Inventory;
using Empiro.Network;
using Empiro.Reporting;
if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Empiro Network Scanner\nUsage: Empiro.CLI <IPv4 or CIDR> [output.json]\nScans networks you own or are authorized to administer. Default limit: 4096 hosts."); return 0;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var engine = new NativeScanner(new PortScanner(), new ServiceDetectionEngine(), new FingerprintEngine(), new AdapterDiscovery(), new NeighborCache());
    var assets = new List<Asset>();
    await foreach (var asset in engine.ScanAsync(new ScanOptions(args[0], [22,80,443,445,554]), null, cancellation.Token))
    { assets.Add(asset); Console.WriteLine($"{asset.Ip,-16} {asset.DisplayName,-32} {asset.Ports}"); }
    if (args.Length > 1) await new ReportingEngine().ExportAsync(args[1], assets, cancellation.Token);
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Scan cancelled."); return 2; }
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
