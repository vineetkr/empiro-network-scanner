using Empiro.Domain;
namespace Empiro.Monitoring;
public sealed class ChangeDetection
{
    public IReadOnlyList<NetworkEvent> Compare(Asset? previous, Asset current)
    {
        var events = new List<NetworkEvent>();
        void Add(string category, string text) => events.Add(new(DateTimeOffset.UtcNow, current.Ip, category, text));
        if (previous == null) { Add("New device", $"Discovered {current.DisplayName}"); return events; }
        if (previous.Online != current.Online) Add("Availability", current.Status);
        if (previous.Mac != null && current.Mac != null && previous.Mac != current.Mac) Add("IP conflict", $"MAC changed from {previous.Mac} to {current.Mac}; verify before treating as a confirmed conflict.");
        if (previous.Hostname != current.Hostname) Add("Hostname", $"{previous.Hostname ?? "Unknown"} → {current.Hostname ?? "Unknown"}");
        foreach (var port in current.Services.Select(s => s.Port).Except(previous.Services.Select(s => s.Port))) Add("New port", $"TCP/{port} opened");
        foreach (var port in previous.Services.Where(s => s.Protocol == "TCP").Select(s => s.Port).Intersect(current.ScannedTcpPorts).Except(current.Services.Select(s => s.Port))) Add("Port no longer observed", $"TCP/{port} did not accept a connection; it may be closed or filtered.");
        return events;
    }
}
