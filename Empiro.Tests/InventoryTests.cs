using Empiro.Domain;
using Empiro.Infrastructure;
using Empiro.Inventory;
using Empiro.IPAM;
using Empiro.Monitoring;
using Empiro.Reporting;
using Empiro.Security;
using Empiro.Topology;
using Xunit;
namespace Empiro.Tests;
public sealed class InventoryTests
{
    [Fact] public void ConflictingMacDoesNotInheritIdentity()
    {
        var old = new Asset { Ip = "192.0.2.1", Mac = "AA", Kind = DeviceKind.Camera, Confidence = 95, Notes = "Private camera", Hostname = "Camera" };
        var current = new Asset { Ip = old.Ip, Mac = "BB" };
        var result = new CorrelationEngine().Merge(old, current);
        Assert.Equal(DeviceKind.Unknown, result.Kind); Assert.Null(result.Hostname); Assert.Empty(result.Notes);
        Assert.Contains(new ChangeDetection().Compare(old, current), e => e.Category == "IP conflict");
    }
    [Fact] public void OnlyRescannedPortsCanBeRemoved()
    {
        var previous = new Asset { Services = [new(80, "TCP", "HTTP", null, 95, "Response"), new(22, "TCP", "SSH", null, 95, "Response")] };
        var next = new Asset { ScannedTcpPorts = [80], Services = [] };
        var result = new CorrelationEngine().Merge(previous, next); Assert.Single(result.Services); Assert.Equal(22, result.Services[0].Port);
        Assert.Contains(new ChangeDetection().Compare(previous, next), e => e.Category == "Port no longer observed");
    }
    [Fact] public void TopologyNeverInfersPhysicalEdgesFromSameSubnet()
    {
        var assets = new[] { new Asset { Ip = "192.0.2.1" }, new Asset { Ip = "192.0.2.2" } };
        Assert.Empty(new TopologyEngine().Correlate(assets));
    }
    [Fact] public void TopologyPreservesEvidence()
    {
        var asset = new Asset { Ip = "192.0.2.1", Evidence = [new("Neighbor", "192.0.2.2", "LLDP", 95, DateTimeOffset.UtcNow)] };
        var edge = Assert.Single(new TopologyEngine().Correlate([asset])); Assert.Equal("LLDP", edge.Evidence); Assert.Equal(95, edge.Confidence);
    }
    [Fact] public void UnobservedAddressIsNotCalledFree()
    { var rows = new IpamEngine().Inspect("192.0.2.0/30", []).ToArray(); Assert.All(rows, r => Assert.Contains("unverified", r.State)); }
    [Fact] public async Task DatabasePersistsMetadataAndRejectsDemo()
    {
        var folder = Path.Combine(Path.GetTempPath(), "EmpiroTests", Guid.NewGuid().ToString()); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "test.db");
        try
        {
            var store = new SqliteInventory(path, new(), new()); await store.InitializeAsync();
            await store.SaveAsync(new Asset { Ip = "192.0.2.1", Mac = "AA" });
            await store.UpdateMetadataAsync("192.0.2.1", true, "Reception", "CCTV");
            await store.SaveAsync(new Asset { Ip = "192.0.2.1", Mac = "AA" });
            var saved = Assert.Single(await store.LoadAsync()); Assert.True(saved.Favorite); Assert.Equal("Reception", saved.Notes);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(new Asset { Ip = "192.0.2.9", Synthetic = true }));
            Assert.Single(await store.EventsAsync());
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }
    [Fact] public async Task ExportsEscapeHtmlAndSpreadsheetFormulas()
    {
        var folder = Path.Combine(Path.GetTempPath(), "EmpiroTests", Guid.NewGuid().ToString()); Directory.CreateDirectory(folder);
        try
        {
            var assets = new[] { new Asset { Ip = "192.0.2.1", Hostname = "=HYPERLINK(\"bad\")", Synthetic = true }, new Asset { Ip = "192.0.2.2", Hostname = "<script>bad</script>", Synthetic = true } };
            var exporter = new ReportingEngine(); await exporter.ExportAsync(Path.Combine(folder, "x.csv"), assets); await exporter.ExportAsync(Path.Combine(folder, "x.html"), assets);
            Assert.Contains("'=HYPERLINK", await File.ReadAllTextAsync(Path.Combine(folder, "x.csv")));
            var html = await File.ReadAllTextAsync(Path.Combine(folder, "x.html")); Assert.DoesNotContain("<script>", html); Assert.Contains("SYNTHETIC DEMO DATA", html);
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact] public void SecurityFindingIsEvidenceBased()
    { var asset = new Asset { Ip = "192.0.2.2", Services = [new(23, "TCP", "Telnet", null, 35, "Port convention")] }; Assert.Contains("Potential", Assert.Single(new SecurityAssessmentEngine().Assess(asset)).Title); }
}
