using System.Net;
using System.Text;
using System.Text.Json;
using Empiro.Domain;
namespace Empiro.Reporting;
public sealed class ReportingEngine
{
    public async Task ExportAsync(string path, IEnumerable<Asset> source, CancellationToken ct = default)
    {
        var assets = source.ToArray();
        static string Csv(string? v) { v ??= ""; if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v; return "\"" + v.Replace("\"", "\"\"") + "\""; }
        static string Html(string? v) => WebUtility.HtmlEncode(v ?? "");
        string content = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => JsonSerializer.Serialize(assets, new JsonSerializerOptions { WriteIndented = true }),
            ".html" => "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Empiro inventory</title><style>body{font:14px Segoe UI;margin:40px;color:#17243b}table{border-collapse:collapse;width:100%}td,th{padding:12px;text-align:left;border-bottom:1px solid #ccd4df}</style><h1>Empiro network inventory</h1><p>" + (assets.Any(a => a.Synthetic) ? "SYNTHETIC DEMO DATA" : "Observed network assets") + " · " + DateTimeOffset.Now.ToString("u") + "</p><table><tr><th>IP</th><th>Device</th><th>Type</th><th>Ports</th><th>Confidence</th></tr>" + string.Join("", assets.Select(a => $"<tr><td>{Html(a.Ip)}</td><td>{Html(a.DisplayName)}</td><td>{a.Kind}</td><td>{Html(a.Ports)}</td><td>{a.Confidence}%</td></tr>")) + "</table></html>",
            ".txt" => string.Join(Environment.NewLine, assets.Select(a => $"{a.Ip}\t{a.DisplayName}\t{a.Kind}\t{a.Status}\t{a.Ports}\tDemo={a.Synthetic}")),
            _ => "IP,Hostname,Kind,MAC,Status,Ports,Confidence,Synthetic\r\n" + string.Join("\r\n", assets.Select(a => string.Join(",", new[] { Csv(a.Ip), Csv(a.Hostname), Csv(a.Kind.ToString()), Csv(a.Mac), Csv(a.Status), Csv(a.Ports), Csv(a.Confidence.ToString()), Csv(a.Synthetic.ToString()) })))
        };
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
    }
}
