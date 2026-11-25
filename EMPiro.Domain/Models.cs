namespace Empiro.Domain;

public enum DeviceKind { Unknown, Computer, Server, Router, Switch, AccessPoint, Camera, Nvr, Printer, IoT }
public sealed record Evidence(string Property, string Value, string Source, int Confidence, DateTimeOffset ObservedAt);
public sealed record Service(int Port, string Protocol, string Name, string? Version, int Confidence, string Evidence);
public sealed record Asset
{
    public string Ip { get; init; } = "";
    public string? Mac { get; init; }
    public string? Hostname { get; init; }
    public string? Vendor { get; init; }
    public string? Model { get; init; }
    public DeviceKind Kind { get; init; }
    public int Confidence { get; init; }
    public bool Online { get; init; } = true;
    public bool Favorite { get; init; }
    public string Notes { get; init; } = "";
    public string Tags { get; init; } = "";
    public bool Synthetic { get; init; }
    public DateTimeOffset FirstSeen { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeen { get; init; } = DateTimeOffset.UtcNow;
    public List<Service> Services { get; init; } = [];
    public List<int> ScannedTcpPorts { get; init; } = [];
    public List<Evidence> Evidence { get; init; } = [];
    public string DisplayName => Hostname ?? Model ?? (Kind == DeviceKind.Unknown ? "Unknown device" : Kind.ToString());
    public string Status => Online ? "Online" : "Offline";
    public string Ports => string.Join(", ", Services.Select(s => s.Port));
    public string Identity => Mac is { Length: > 0 } ? Mac : Ip;
}
public sealed record Adapter(string Id, string Name, string Address, int Prefix, string? Gateway, string? Mac)
{
    public string Label => $"{Name} · {Address}/{Prefix}";
    public override string ToString() => Label;
}
public sealed record ScanOptions(string Target, int[] Ports, int Concurrency = 32, int TimeoutMs = 700, int MaximumHosts = 4096, bool Cctv = false)
{
    public void Validate()
    {
        if (Concurrency is < 1 or > 256 || TimeoutMs is < 100 or > 10000 || MaximumHosts is < 1 or > 65536)
            throw new ArgumentException("Concurrency must be 1–256, timeout 100–10000 ms, and host limit 1–65536.");
        if (Ports.Length > 65535 || Ports.Any(p => p is < 1 or > 65535)) throw new ArgumentException("Ports must be between 1 and 65535.");
    }
}
public sealed record ScanProgress(int Completed, int Total, int Found, string Stage)
{
    public double Percent => Total == 0 ? 0 : Completed * 100.0 / Total;
}
public sealed record ScanSession(Guid Id, DateTimeOffset Started, string Target, string Profile, int Found, string Outcome, double Seconds);
public sealed record NetworkEvent(DateTimeOffset At, string Ip, string Category, string Description);
public sealed record TopologyEdge(string Source, string Destination, string Evidence, int Confidence, DateTimeOffset At);
public sealed record Finding(string Ip, string Severity, string Title, string Evidence, string Recommendation);
