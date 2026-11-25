using Empiro.Domain;
namespace Empiro.App;
/// <summary>Explicitly synthetic assets, used only in the isolated demo workspace.</summary>
public static class DemoData
{
    public static IReadOnlyList<Asset> Create()
    {
        var entries = new (string Name, DeviceKind Kind, string Vendor, string Model, int[] Ports)[]
        {
            ("Office gateway", DeviceKind.Router, "Example Networks", "Gateway R1", [80,443]),
            ("Core PoE switch", DeviceKind.Switch, "Example Networks", "Switch 24 PoE", [22,443]),
            ("Office Wi-Fi", DeviceKind.AccessPoint, "Example Wireless", "AP 6", [443]),
            ("Reception camera", DeviceKind.Camera, "Example Vision", "Dome 4MP", [80,443,554]),
            ("Entrance camera", DeviceKind.Camera, "Example Vision", "Bullet 4MP", [80,554]),
            ("Parking camera", DeviceKind.Camera, "Example Vision", "Bullet 8MP", [443,554]),
            ("Warehouse camera", DeviceKind.Camera, "Example Vision", "Dome 4MP", [80,554]),
            ("Office recorder", DeviceKind.Nvr, "Example Vision", "NVR 16", [443,554]),
            ("Fileserver", DeviceKind.Server, "Example Systems", "Storage server", [22,443,445]),
            ("Design workstation", DeviceKind.Computer, "Example Systems", "Workstation", [445,3389]),
            ("Reception PC", DeviceKind.Computer, "Example Systems", "Desktop", [445]),
            ("Meeting room printer", DeviceKind.Printer, "Example Print", "Laser MFP", [80,443,9100]),
            ("Environment sensor", DeviceKind.IoT, "Example Sensors", "Room sensor", [80]),
            ("Unidentified device", DeviceKind.Unknown, "Unverified", "Not identified", [8080])
        };
        return entries.Select((e, i) => new Asset
        {
            Ip = $"192.0.2.{i + 1}", Hostname = e.Name, Kind = e.Kind, Vendor = e.Vendor, Model = e.Model,
            Mac = $"02:00:00:00:00:{i + 1:X2}", Synthetic = true, Online = i != 6,
            Confidence = e.Kind == DeviceKind.Unknown ? 0 : 95, Favorite = i is 3 or 7,
            FirstSeen = DateTimeOffset.UtcNow.AddDays(-12), LastSeen = DateTimeOffset.UtcNow.AddMinutes(i == 6 ? -48 : -2),
            Notes = "Synthetic example. No real network device.", Tags = e.Kind is DeviceKind.Camera or DeviceKind.Nvr ? "CCTV, Office" : "Office",
            Services = e.Ports.Select(p => new Service(p, "TCP", p switch { 80 or 8080 => "HTTP", 443 => "HTTPS", 554 => "RTSP", 22 => "SSH", 445 => "SMB", 3389 => "RDP", _ => "Print" }, null, 95, "Synthetic example")).ToList(),
            Evidence = [new("Identity", e.Name, "Synthetic demo", 100, DateTimeOffset.UtcNow), .. i > 1 ? new[] { new Evidence("Neighbor", "192.0.2.2", "LLDP", 95, DateTimeOffset.UtcNow) } : Array.Empty<Evidence>()]
        }).ToArray();
    }
}
