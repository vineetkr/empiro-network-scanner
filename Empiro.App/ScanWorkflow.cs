using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows.Input;
using Empiro.Domain;
using Empiro.Network;
using Serilog;
namespace Empiro.App;
public sealed partial class ShellViewModel
{
    private CancellationTokenSource? scanCancellation;
    private CancellationTokenSource? monitoringCancellation;
    private Task? monitoringTask;
    private TaskCompletionSource? scanFinished;
    private bool scanning;
    public bool IsScanning { get => scanning; private set { Set(ref scanning, value); CommandManager.InvalidateRequerySuggested(); } }
    private bool monitoring;
    public bool IsMonitoring { get => monitoring; private set { Set(ref monitoring, value); Raise(nameof(MonitoringLabel)); CommandManager.InvalidateRequerySuggested(); } }
    public string MonitoringLabel => IsMonitoring ? "Stop monitoring" : "Start monitoring";
    private double progress;
    public double Progress { get => progress; private set => Set(ref progress, value); }
    private string progressText = "Ready to discover";
    public string ProgressText { get => progressText; private set => Set(ref progressText, value); }

    private async Task RunScanAsync(bool cameras)
    {
        Navigate(cameras ? "CCTV Discovery" : Page == "Advanced Scan" ? "Advanced Scan" : "Network Scan");
        if (IsDemo) { Status = "Demo workspace. Exit demo to discover real devices on your network."; return; }
        if (SelectedAdapter == null) throw new InvalidOperationException("Choose an active network adapter first.");
        var ports = PortText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => int.TryParse(p, out var port) ? port : throw new ArgumentException("Enter comma-separated port numbers.")).Distinct().ToArray();
        var options = new ScanOptions(Target, cameras ? ports.Union(new[] { 80, 443, 554, 8000, 8080 }).ToArray() : ports, Concurrency, TimeoutMs, MaximumHosts, cameras);
        options.Validate(); var network = new IpNetwork(Target);
        if (network.Count > (ulong)MaximumHosts) throw new ArgumentException($"Target contains {network.Count:N0} hosts. The configured limit is {MaximumHosts:N0}.");
        using var cancellation = new CancellationTokenSource(); scanCancellation = cancellation;
        scanFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IsScanning = true; Progress = 0; var timer = Stopwatch.StartNew(); var started = DateTimeOffset.UtcNow;
        string outcome = "Completed"; var discovered = new HashSet<string>();
        var sessionId = Guid.NewGuid();
        var pending = new List<Asset>(); var flushTimer = Stopwatch.StartNew();
        var adapterSnapshot = new[] { SelectedAdapter };
        var progressReporter = new ThrottledProgress(p => { Progress = p.Percent; ProgressText = $"{p.Completed:N0} / {p.Total:N0} hosts · {p.Found} found · {timer.Elapsed:mm\\:ss}"; });
        Log.Information("Scan started for {Target} with {Concurrency} workers", Target, Concurrency);
        try
        {
            await Task.Run(() => store.SaveSessionAsync(new ScanSession(sessionId, started, options.Target, cameras ? "CCTV" : "Standard", 0, "Running", 0)));
            Status = cameras ? "Searching ONVIF advertisements on the selected adapter…" : "Scanning selected target…";
            if (cameras)
            {
                try
                {
                    await foreach (var asset in cctv.DiscoverAsync(adapterSnapshot, TimeSpan.FromSeconds(3), cancellation.Token))
                    { pending.Add(asset); discovered.Add(asset.Ip); }
                }
                catch (System.Net.Sockets.SocketException ex) { Log.Warning("WS-Discovery unavailable: {ErrorCode}", ex.SocketErrorCode); Status = "Multicast discovery unavailable; continuing TCP discovery."; }
                await FlushAsync();
            }
            await foreach (var asset in scanner.ScanAsync(options, progressReporter, cancellation.Token))
            {
                pending.Add(asset); discovered.Add(asset.Ip);
                if (pending.Count >= 24 || flushTimer.ElapsedMilliseconds >= 180) await FlushAsync();
            }
            await FlushAsync(); Progress = 100;
            Status = $"Scan complete · {discovered.Count} devices responded · {timer.Elapsed.TotalSeconds:F1}s. Unanswered addresses may be filtered or unreachable.";
        }
        catch (OperationCanceledException) { outcome = "Cancelled"; Status = "Scan cancelled. Completed observations are retained."; await FlushAsync(); }
        catch { outcome = "Failed"; throw; }
        finally
        {
            try
            {
                var session = new ScanSession(sessionId, started, options.Target, cameras ? "CCTV" : "Standard", discovered.Count, outcome, timer.Elapsed.TotalSeconds);
                await Task.Run(() => store.SaveSessionAsync(session)); Sessions.Insert(0, session);
                Log.Information("Scan {Outcome}; found {Count}; elapsed {Seconds}", outcome, discovered.Count, timer.Elapsed.TotalSeconds);
                RefreshView();
            }
            finally { scanCancellation = null; IsScanning = false; scanFinished.TrySetResult(); }
        }
        async Task FlushAsync()
        {
            if (pending.Count == 0) return;
            var batch = pending.ToArray(); pending.Clear();
            var events = await Task.Run(async () => { var list = new List<NetworkEvent>(); foreach (var asset in batch) list.AddRange(await store.SaveAsync(asset)); return list; });
            foreach (var asset in batch)
            {
                var old = Assets.FirstOrDefault(a => a.Ip == asset.Ip);
                if (old == null) Assets.Add(asset); else Assets[Assets.IndexOf(old)] = correlation.Merge(old, asset);
            }
            foreach (var entry in events) Events.Insert(0, entry);
            while (Events.Count > 2000) Events.RemoveAt(Events.Count - 1);
            RefreshView(); flushTimer.Restart();
        }
    }
    public async Task StopAsync()
    {
        scanCancellation?.Cancel(); monitoringCancellation?.Cancel();
        if (scanFinished != null) await scanFinished.Task;
        if (monitoringTask != null) await monitoringTask;
    }
    private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring)
        {
            monitoringCancellation?.Cancel();
            if (monitoringTask != null) await monitoringTask;
            return;
        }
        monitoringCancellation = new CancellationTokenSource(); IsMonitoring = true;
        monitoringTask = MonitorAsync(monitoringCancellation.Token);
        Status = "Monitoring saved devices every 30 seconds. Three missed rounds are required before marking offline.";
    }
    private async Task MonitorAsync(CancellationToken ct)
    {
        var failures = new Dictionary<string, int>(); var portScanner = new PortScanner();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var asset in Assets.ToArray())
                {
                    ct.ThrowIfCancellationRequested(); bool online = false;
                    using var ping = new Ping();
                    try { online = (await ping.SendPingAsync(System.Net.IPAddress.Parse(asset.Ip), TimeSpan.FromMilliseconds(TimeoutMs), cancellationToken: ct)).Status == IPStatus.Success; }
                    catch (PingException) { }
                    if (!online && asset.Services.FirstOrDefault() is { } service) online = await portScanner.IsOpenAsync(asset.Ip, service.Port, TimeoutMs, ct);
                    failures[asset.Ip] = online ? 0 : failures.GetValueOrDefault(asset.Ip) + 1;
                    if (!online && failures[asset.Ip] < 3) continue;
                    var observed = asset with { Online = online, LastSeen = online ? DateTimeOffset.UtcNow : asset.LastSeen, ScannedTcpPorts = [] };
                    var events = await Task.Run(() => store.SaveAsync(observed, ct), ct);
                    Assets[Assets.IndexOf(asset)] = observed;
                    foreach (var entry in events) Events.Insert(0, entry);
                    while (Events.Count > 2000) Events.RemoveAt(Events.Count - 1);
                }
                RefreshView(); await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { Status = "Monitoring stopped."; }
        catch (Exception ex) { Error(ex); }
        finally { IsMonitoring = false; monitoringCancellation?.Dispose(); monitoringCancellation = null; }
    }
}

internal sealed class ThrottledProgress(Action<ScanProgress> callback) : IProgress<ScanProgress>
{
    private readonly System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;
    private long lastTicks;
    public void Report(ScanProgress value)
    {
        var now = Environment.TickCount64;
        var old = Interlocked.Read(ref lastTicks);
        if (value.Completed < value.Total && now - old < 120) return;
        if (Interlocked.CompareExchange(ref lastTicks, now, old) != old && value.Completed < value.Total) return;
        dispatcher.BeginInvoke(() => callback(value));
    }
}
