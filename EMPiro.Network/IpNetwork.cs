using System.Net;
using System.Net.Sockets;
namespace Empiro.Network;

/// <summary>Validated IPv4 network with bounded, lazy host enumeration.</summary>
public sealed class IpNetwork
{
    public uint Network { get; }
    public int Prefix { get; }
    public ulong Count => Prefix >= 31 ? 1UL << (32 - Prefix) : (1UL << (32 - Prefix)) - 2;
    public IpNetwork(string value)
    {
        var parts = value.Trim().Split('/');
        if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Enter an IPv4 address or CIDR, for example 192.168.1.0/24.");
        Prefix = parts.Length == 1 ? 32 : int.TryParse(parts[1], out var prefix) && prefix is >= 0 and <= 32 ? prefix : throw new ArgumentException("IPv4 prefix must be 0–32.");
        Network = ToUInt(address) & Mask(Prefix);
    }
    public static uint ToUInt(IPAddress address) { var b = address.GetAddressBytes(); return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3]; }
    public static IPAddress ToAddress(uint value) => new(new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
    private static uint Mask(int prefix) => prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
    public bool Contains(IPAddress ip) => ip.AddressFamily == AddressFamily.InterNetwork && (ToUInt(ip) & Mask(Prefix)) == Network;
    public IEnumerable<IPAddress> Hosts(int maximum)
    {
        if (Count > (ulong)maximum) throw new ArgumentException($"This target contains {Count:N0} hosts; the limit is {maximum:N0}. Narrow the target or increase the advanced limit.");
        var start = (ulong)Network + (Prefix < 31 ? 1UL : 0UL);
        for (ulong i = 0; i < Count; i++) yield return ToAddress((uint)(start + i));
    }
    public override string ToString() => $"{ToAddress(Network)}/{Prefix}";
}
