using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Empiro.Core;
using Empiro.Domain;
using Empiro.Infrastructure;
using Empiro.Inventory;
using Empiro.IPAM;
using Empiro.Network;
using Empiro.Reporting;
using Empiro.Security;
using Empiro.Topology;
using Microsoft.Win32;
using Serilog;
namespace Empiro.App;

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly INetworkScanner scanner;
    private readonly ICctvDiscoveryProvider cctv;
    private readonly IInventoryStore store;
    private readonly AdapterDiscovery adapters;
    private readonly CorrelationEngine correlation;
    private readonly SettingsStore settingsStore;
    private readonly ReportingEngine reporting;
    private readonly IpamEngine ipam;
    private readonly SecurityAssessmentEngine security;
    private readonly TopologyEngine topology;
    private UserSettings settings = new();
    public ObservableCollection<Asset> Assets { get; } = [];
    public ICollectionView Devices { get; }
    public ObservableCollection<Adapter> Adapters { get; } = [];
    public ObservableCollection<NetworkEvent> Events { get; } = [];
    public ObservableCollection<DisplayRow> Rows { get; } = [];
    public ObservableCollection<StatCard> Stats { get; } = [];
    public ObservableCollection<ScanSession> Sessions { get; } = [];
    public ObservableCollection<NavItem> Navigation { get; } = [];
    public ICollectionView NavigationGroups { get; }
    public ObservableCollection<TopologyEdge> Edges { get; } = [];
    private NavItem? selectedNavigation;
    public NavItem? SelectedNavigation { get => selectedNavigation; set { if (Set(ref selectedNavigation, value) && value != null) { Raise(nameof(Page)); Raise(nameof(PageSubtitle)); RefreshView(); } } }
    public string Page => SelectedNavigation?.Label ?? "Dashboard";
    public string PageSubtitle => Page switch
    {
        "Dashboard" => "Your network, in focus.", "CCTV Discovery" => "Find cameras and recorders. Understand every connection.",
        "Advanced Scan" => "A closer look, with you in control.", "Network Map" => "A clear view of your devices and observed relationships.",
        "IPAM" => "Observed addresses, with availability kept honest.", "Settings" => "Make Empiro work your way.",
        "Network Scan" => "Discover what's connected to your network.", _ => "Explore the evidence behind your network."
    };
    private Adapter? selectedAdapter;
    public Adapter? SelectedAdapter { get => selectedAdapter; set { if (Set(ref selectedAdapter, value) && value != null) { Target = new IpNetwork($"{value.Address}/{value.Prefix}").ToString(); Raise(nameof(ConnectionLabel)); } } }
    private Asset? selectedAsset;
    public Asset? SelectedAsset { get => selectedAsset; set { if (Set(ref selectedAsset, value)) { Notes = value?.Notes ?? ""; Tags = value?.Tags ?? ""; Raise(nameof(HasSelection)); Raise(nameof(SelectedEvidence)); } } }
    public bool HasSelection => SelectedAsset != null;
    public string SelectedEvidence => SelectedAsset == null ? "" : string.Join("\n\n", SelectedAsset.Evidence.Select(e => $"{e.Property}: {e.Value}\n{e.Source} · {e.Confidence}% · {e.ObservedAt.LocalDateTime:g}"));
    private string search = "";
    public string Search { get => search; set { if (Set(ref search, value)) { Devices.Refresh(); Raise(nameof(DeviceCountLabel)); } } }
    private string target = "";
    public string Target { get => target; set => Set(ref target, value); }
    private string portText = "22,80,443,445,554,3389,8000,8080";
    public string PortText { get => portText; set => Set(ref portText, value); }
    private int concurrency = 32;
    public int Concurrency { get => concurrency; set => Set(ref concurrency, value); }
    private int timeoutMs = 700;
    public int TimeoutMs { get => timeoutMs; set => Set(ref timeoutMs, value); }
    private int maximumHosts = 4096;
    public int MaximumHosts { get => maximumHosts; set => Set(ref maximumHosts, value); }
    private bool demo;
    public bool IsDemo { get => demo; private set { Set(ref demo, value); Raise(nameof(ModeLabel)); Raise(nameof(DemoButtonLabel)); Raise(nameof(ConnectionLabel)); } }
    public string ModeLabel => IsDemo ? "DEMO WORKSPACE · SYNTHETIC DATA" : "LIVE WORKSPACE";
    public string DemoButtonLabel => IsDemo ? "Exit demo" : "Explore demo";
    public string ConnectionLabel => IsDemo ? "Demo network · 192.0.2.0/24" : SelectedAdapter?.Label ?? "No active IPv4 adapter";
    private string status = "Ready when you are. Choose an adapter and start discovery.";
    public string Status { get => status; set => Set(ref status, value); }
    private string notes = "";
    public string Notes { get => notes; set => Set(ref notes, value); }
    private string tags = "";
    public string Tags { get => tags; set => Set(ref tags, value); }
    private bool light;
    public bool LightTheme { get => light; set { if (Set(ref light, value)) ThemeManager.Apply(value); } }
    public string Greeting => DateTime.Now.Hour < 12 ? "Good morning." : DateTime.Now.Hour < 18 ? "Good afternoon." : "Good evening.";
    public string OverviewTitle => Assets.Count == 0 ? "Let's discover your network." : "Everything connected. Clearly understood.";
    public string OverviewDetail => IsDemo ? "Explore a sample office network. Every device on this screen is synthetic." : Assets.Count == 0 ? "Choose your network, start a scan, and we'll take it from there." : "Your saved inventory at a glance. Scan again to refresh reachability and services.";
    public string OnlineSummary => $"{Assets.Count(a => a.Online)} of {Assets.Count} last observed online";
    public double OnlinePercent => Assets.Count == 0 ? 0 : 100.0 * Assets.Count(a => a.Online) / Assets.Count;
    public string CameraSummary => $"{Assets.Count(a => a.Kind is DeviceKind.Camera or DeviceKind.Nvr)} cameras & recorders";
    public string DeviceCountLabel => $"{Devices.Cast<object>().Count()} devices";
    public string MapCaption => Edges.Count == 0 ? "No physical links verified. Nodes show inventory only." : $"{Edges.Count} evidence-backed links · {(IsDemo ? "synthetic example" : "observed neighbors")}";
    public ICommand StartScanCommand { get; }
    public ICommand FindCamerasCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DemoCommand { get; }
    public ICommand MapCommand { get; }
    public ICommand AdvancedCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand SaveNotesCommand { get; }
    public ICommand FavoriteCommand { get; }
    public ICommand CopyIpCommand { get; }
    public ICommand WebCommand { get; }
    public ICommand CloseInspectorCommand { get; }
    public ICommand MonitoringCommand { get; }
    public ICommand RefreshAdaptersCommand { get; }

    public ShellViewModel(INetworkScanner scanner, ICctvDiscoveryProvider cctv, IInventoryStore store, AdapterDiscovery adapters,
        CorrelationEngine correlation, SettingsStore settingsStore, ReportingEngine reporting, IpamEngine ipam, SecurityAssessmentEngine security, TopologyEngine topology)
    {
        this.scanner = scanner; this.cctv = cctv; this.store = store; this.adapters = adapters; this.correlation = correlation;
        this.settingsStore = settingsStore; this.reporting = reporting; this.ipam = ipam; this.security = security; this.topology = topology;
        Devices = CollectionViewSource.GetDefaultView(Assets); Devices.Filter = Filter;
        foreach (var (group, items) in new[]
        {
            ("OVERVIEW", new[] { "Dashboard" }), ("DISCOVERY", new[] { "Network Scan", "CCTV Discovery", "Advanced Scan", "Network Map" }),
            ("INVENTORY", new[] { "Devices", "Cameras", "NVRs", "Network Devices", "Services" }),
            ("NETWORK", new[] { "IPAM", "Subnets", "VLANs", "Routing", "DNS / DHCP" }),
            ("MONITORING", new[] { "Live Monitoring", "Alerts", "Events", "Changes" }),
            ("ANALYSIS", new[] { "Security Assessment", "Dependencies" }), ("MANAGEMENT", new[] { "Scan History", "Favorites", "Reports", "Settings" })
        }) foreach (var item in items) Navigation.Add(new(item, item switch { "Dashboard" => "\uE80F", "Network Map" => "\uE774", "CCTV Discovery" or "Cameras" => "\uE714", "Settings" => "\uE713", "Alerts" => "\uE7F4", "Security Assessment" => "\uE72E", _ => "\uE8A5" }, group));
        NavigationGroups = CollectionViewSource.GetDefaultView(Navigation); NavigationGroups.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavItem.Group)));
        selectedNavigation = Navigation[0];
        StartScanCommand = new AsyncCommand(() => RunScanAsync(false), Error, () => !IsScanning && !IsMonitoring);
        FindCamerasCommand = new AsyncCommand(() => RunScanAsync(true), Error, () => !IsScanning && !IsMonitoring);
        CancelCommand = new RelayCommand(() => scanCancellation?.Cancel(), () => IsScanning);
        DemoCommand = new AsyncCommand(ToggleDemoAsync, Error, () => !IsScanning && !IsMonitoring);
        MapCommand = new RelayCommand(() => Navigate("Network Map")); AdvancedCommand = new RelayCommand(() => Navigate("Advanced Scan"));
        ExportCommand = new AsyncCommand(ExportAsync, Error, () => Assets.Count > 0);
        SaveSettingsCommand = new AsyncCommand(SaveSettingsAsync, Error);
        SaveNotesCommand = new AsyncCommand(() => SaveMetadataAsync(false), Error, () => HasSelection && !IsScanning && !IsMonitoring);
        FavoriteCommand = new AsyncCommand(() => SaveMetadataAsync(true), Error, () => HasSelection && !IsScanning && !IsMonitoring);
        CopyIpCommand = new RelayCommand(() => Clipboard.SetText(SelectedAsset!.Ip), () => HasSelection);
        WebCommand = new RelayCommand(OpenWeb, () => SelectedAsset is { Synthetic: false } a && a.Services.Any(s => s.Port is 80 or 443));
        CloseInspectorCommand = new RelayCommand(() => SelectedAsset = null);
        MonitoringCommand = new AsyncCommand(ToggleMonitoringAsync, Error, () => !IsScanning && !IsDemo && Assets.Count > 0);
        RefreshAdaptersCommand = new RelayCommand(RefreshAdapters, () => !IsScanning && !IsMonitoring && !IsDemo);
    }
    public async Task InitializeAsync(bool preview)
    {
        settings = settingsStore.Load(); Concurrency = settings.Concurrency; TimeoutMs = settings.TimeoutMs; MaximumHosts = settings.MaximumHosts; LightTheme = settings.LightTheme;
        await Task.Run(() => store.InitializeAsync()); RefreshAdapters();
        IsDemo = preview; await ReloadAsync();
    }
    public void Navigate(string label) => SelectedNavigation = Navigation.First(n => n.Label == label);
    private void RefreshAdapters() { Adapters.Clear(); foreach (var a in adapters.GetAdapters()) Adapters.Add(a); SelectedAdapter = Adapters.FirstOrDefault(); }
    private async Task ToggleDemoAsync() { IsDemo = !IsDemo; SelectedAsset = null; await ReloadAsync(); Status = IsDemo ? "Demo mode. All example data is synthetic; scanning is disabled." : "Live workspace restored. Ready to scan."; }
    private async Task ReloadAsync()
    {
        if (IsDemo)
        {
            Adapters.Clear(); Adapters.Add(new Adapter("demo", "Demo Ethernet", "192.0.2.100", 24, "192.0.2.1", "02:00:00:00:01:00")); SelectedAdapter = Adapters[0];
            Status = "Demo workspace · synthetic data · no network traffic is generated.";
        }
        else RefreshAdapters();
        Assets.Clear(); Events.Clear(); Sessions.Clear();
        foreach (var a in IsDemo ? DemoData.Create() : await Task.Run(() => store.LoadAsync())) Assets.Add(a);
        if (IsDemo)
        {
            Events.Add(new(DateTimeOffset.Now.AddMinutes(-2), "192.0.2.4", "Camera discovered", "Reception camera identified · synthetic example"));
            Events.Add(new(DateTimeOffset.Now.AddMinutes(-8), "192.0.2.14", "New device", "An unidentified device joined · synthetic example"));
            Events.Add(new(DateTimeOffset.Now.AddMinutes(-48), "192.0.2.7", "Availability", "Warehouse camera offline · synthetic example"));
        }
        else
        {
            foreach (var e in await Task.Run(() => store.EventsAsync())) Events.Add(e);
            foreach (var s in (await Task.Run(() => store.SessionsAsync())).OrderByDescending(s => s.Started)) Sessions.Add(s);
        }
        RefreshView();
    }
    private bool Filter(object item)
    {
        if (item is not Asset a) return false;
        var matchesPage = Page switch { "CCTV Discovery" or "Cameras" => a.Kind is DeviceKind.Camera or DeviceKind.Nvr || a.Services.Any(s => s.Name == "RTSP"), "NVRs" => a.Kind == DeviceKind.Nvr, "Network Devices" => a.Kind is DeviceKind.Router or DeviceKind.Switch or DeviceKind.AccessPoint, "Favorites" => a.Favorite, _ => true };
        return matchesPage && (string.IsNullOrWhiteSpace(Search) || $"{a.Ip} {a.Mac} {a.Hostname} {a.Vendor} {a.Model} {a.Kind} {a.Tags} {a.Ports} {string.Join(' ', a.Services.Select(s => s.Name))}".Contains(Search, StringComparison.OrdinalIgnoreCase));
    }
    public void RefreshView()
    {
        Devices.Refresh(); Stats.Clear();
        Stats.Add(new("TOTAL DEVICES", Assets.Count.ToString("N0"), "In your inventory", "\uE8A5"));
        Stats.Add(new("CAMERAS & NVRS", Assets.Count(a => a.Kind is DeviceKind.Camera or DeviceKind.Nvr).ToString(), "Video infrastructure", "\uE714"));
        Stats.Add(new("LAST SEEN ONLINE", Assets.Count(a => a.Online).ToString(), $"{Assets.Count(a => !a.Online)} last seen offline", "\uE930"));
        Stats.Add(new("OPEN SERVICES", Assets.Sum(a => a.Services.Count).ToString(), "Observed TCP services", "\uE968"));
        Edges.Clear(); foreach (var edge in topology.Correlate(Assets)) Edges.Add(edge);
        RefreshRows();
        foreach (var name in new[] { nameof(OverviewTitle), nameof(OverviewDetail), nameof(OnlineSummary), nameof(OnlinePercent), nameof(CameraSummary), nameof(DeviceCountLabel), nameof(MapCaption) }) Raise(name);
        CommandManager.InvalidateRequerySuggested();
    }
    private void RefreshRows()
    {
        Rows.Clear();
        if (Page == "Services") foreach (var a in Assets) foreach (var s in a.Services) Rows.Add(new(a.Ip, $"{s.Port}/{s.Protocol} · {s.Name}", s.Version ?? s.Evidence, $"{s.Confidence}% confidence"));
        else if (Page == "Security Assessment" || Page == "Alerts") foreach (var f in Assets.SelectMany(security.Assess)) Rows.Add(new(f.Ip, f.Title, f.Recommendation, f.Severity));
        else if (Page is "Events" or "Changes" or "Live Monitoring") foreach (var e in Events) Rows.Add(new(e.At.LocalDateTime.ToString("g"), e.Category, $"{e.Ip} · {e.Description}", "Observed"));
        else if (Page == "Scan History") foreach (var s in Sessions) Rows.Add(new(s.Started.LocalDateTime.ToString("g"), s.Target, $"{s.Profile} · {s.Found} found · {s.Seconds:F1}s", s.Outcome));
        else if (Page == "Subnets") foreach (var a in Adapters) Rows.Add(new(new IpNetwork($"{a.Address}/{a.Prefix}").ToString(), a.Name, $"Gateway {a.Gateway ?? "not configured"}", "Local interface"));
        else if (Page == "DNS / DHCP") foreach (var a in Assets.Where(a => a.Hostname != null)) Rows.Add(new(a.Ip, a.Hostname!, "Name from discovery evidence; DHCP leases not collected", "Observed"));
        else if (Page == "Dependencies") foreach (var e in Edges) Rows.Add(new(e.Source, e.Destination, e.Evidence, $"{e.Confidence}%"));
        else if (Page == "IPAM")
        {
            try { foreach (var a in ipam.Inspect(IsDemo ? "192.0.2.0/24" : Target, Assets, MaximumHosts)) Rows.Add(new(a.Ip, a.State, a.Device, "")); }
            catch (ArgumentException ex) { Status = ex.Message; }
        }
        else if (Page is "VLANs" or "Routing") Rows.Add(new("Collection not available", "Requires a protocol provider", "This build does not yet collect switch VLAN tables or Windows route tables.", "Not implemented"));
    }
    private async Task ExportAsync()
    {
        var dialog = new SaveFileDialog { FileName = IsDemo ? "Empiro-demo-inventory" : "Empiro-inventory", Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json|HTML report (*.html)|*.html|Text (*.txt)|*.txt" };
        if (dialog.ShowDialog() != true) return;
        await reporting.ExportAsync(dialog.FileName, Devices.Cast<Asset>().ToArray()); Status = $"Export saved: {dialog.FileName}";
    }
    private async Task SaveSettingsAsync()
    {
        new ScanOptions(Target, [], Concurrency, TimeoutMs, MaximumHosts).Validate();
        settings = new(LightTheme, Concurrency, TimeoutMs, MaximumHosts); await settingsStore.SaveAsync(settings); Status = "Preferences saved.";
    }
    private async Task SaveMetadataAsync(bool toggleFavorite)
    {
        var asset = SelectedAsset! with { Notes = Notes, Tags = Tags, Favorite = toggleFavorite ? !SelectedAsset!.Favorite : SelectedAsset!.Favorite };
        if (!IsDemo) await Task.Run(() => store.UpdateMetadataAsync(asset.Ip, asset.Favorite, asset.Notes, asset.Tags));
        Assets[Assets.IndexOf(SelectedAsset!)] = asset; SelectedAsset = asset; RefreshView(); Status = IsDemo ? "Updated in this demo session." : "Device details saved.";
    }
    private void OpenWeb()
    {
        if (SelectedAsset is not { Synthetic: false } a || !IPAddress.TryParse(a.Ip, out _)) return;
        try { Process.Start(new ProcessStartInfo($"{(a.Services.Any(s => s.Port == 443) ? "https" : "http")}://{a.Ip}/") { UseShellExecute = true }); }
        catch (Exception ex) { Error(ex); }
    }
    public void Error(Exception ex) { Log.Warning("Operation failed: {ErrorType}", ex.GetType().Name); Status = ex.Message; }
    public void Dispose() { scanCancellation?.Cancel(); monitoringCancellation?.Cancel(); }
}
