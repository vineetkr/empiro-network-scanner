using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Empiro.Core;
using Empiro.CCTV;
using Empiro.Discovery;
using Empiro.Infrastructure;
using Empiro.Inventory;
using Empiro.IPAM;
using Empiro.Monitoring;
using Empiro.Network;
using Empiro.Reporting;
using Empiro.Security;
using Empiro.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
namespace Empiro.App;
public partial class App : Application
{
    private IHost? host;
    private FileStream? instanceLock;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var preview = e.Args.Contains("--preview");
        var outputIndex = Array.IndexOf(e.Args, "--render");
        if (outputIndex >= 0 && (outputIndex + 1 >= e.Args.Length || e.Args[outputIndex + 1].StartsWith("--")))
        {
            MessageBox.Show("Usage: Empiro --render <output-folder>", "Empiro preview");
            Shutdown(1); return;
        }
        var dataDir = outputIndex >= 0 ? Path.Combine(Path.GetFullPath(e.Args[outputIndex + 1]), "preview-data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), preview ? "Empiro-NetworkScanner-Demo" : "Empiro");
        Directory.CreateDirectory(dataDir);
        Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.File(Path.Combine(dataDir, "logs", "empiro-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7).CreateLogger();
        try
        {
            try { instanceLock = new FileStream(Path.Combine(dataDir, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33) { throw new InvalidOperationException("Empiro is already using this workspace. Close the other instance first.", ex); }
            host = Host.CreateDefaultBuilder().UseSerilog().ConfigureServices(services =>
            {
                services.AddSingleton<AdapterDiscovery>(); services.AddSingleton<PortScanner>(); services.AddSingleton<NeighborCache>();
                services.AddSingleton<IServiceDetector, ServiceDetectionEngine>(); services.AddSingleton<IDeviceFingerprintProvider, FingerprintEngine>();
                services.AddSingleton<INetworkScanner, NativeScanner>(); services.AddSingleton<ICctvDiscoveryProvider, WsDiscovery>();
                services.AddSingleton<CorrelationEngine>(); services.AddSingleton<ChangeDetection>();
                services.AddSingleton<IInventoryStore>(s => new SqliteInventory(Path.Combine(dataDir, "inventory.db"), s.GetRequiredService<CorrelationEngine>(), s.GetRequiredService<ChangeDetection>()));
                services.AddSingleton(new SettingsStore(dataDir)); services.AddSingleton<ReportingEngine>(); services.AddSingleton<IpamEngine>();
                services.AddSingleton<SecurityAssessmentEngine>(); services.AddSingleton<TopologyEngine>();
                services.AddSingleton<ShellViewModel>(); services.AddSingleton<MainWindow>();
            }).Build();
            await host.StartAsync();
            var vm = host.Services.GetRequiredService<ShellViewModel>(); await vm.InitializeAsync(preview || outputIndex >= 0);
            var window = host.Services.GetRequiredService<MainWindow>(); MainWindow = window;
            if (outputIndex >= 0)
            {
                // Render the real WPF view without opening a visible desktop window or scanning.
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                window.Width = 1440; window.Height = 960;
                var folder = Path.GetFullPath(e.Args[outputIndex + 1]); Directory.CreateDirectory(folder);
                vm.LightTheme = false;
                foreach (var page in new[] { "Dashboard", "CCTV Discovery", "Network Map", "IPAM", "Advanced Scan" })
                {
                    vm.Navigate(page);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    window.Measure(new Size(1440, 960)); window.Arrange(new Rect(0, 0, 1440, 960)); window.UpdateLayout();
                    var root = window.Content as FrameworkElement ?? window;
                    root.Measure(new Size(1440, 920)); root.Arrange(new Rect(0, 0, 1440, 920)); root.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(1440, 920, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(folder, page.ToLowerInvariant().Replace(' ', '-') + ".png")); encoder.Save(stream);
                }
                Shutdown(); return;
            }
            window.Show();
        }
        catch (Exception ex) { Log.Error(ex, "Application startup failed"); if (outputIndex < 0) MessageBox.Show(ex.Message, "Empiro could not start"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        host?.Dispose(); instanceLock?.Dispose(); Log.CloseAndFlush(); base.OnExit(e);
    }
}
