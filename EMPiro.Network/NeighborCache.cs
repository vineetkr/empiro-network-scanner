using System.Net;
using System.Runtime.InteropServices;
namespace Empiro.Network;
/// <summary>Queries the Windows neighbor resolver only for selected local IPv4 hosts.</summary>
public sealed class NeighborCache
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destination, uint source, byte[] mac, ref uint length);
    public string? Resolve(IPAddress address)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var bytes = new byte[6]; uint length = 6;
        return SendARP(BitConverter.ToUInt32(address.GetAddressBytes()), 0, bytes, ref length) == 0 && length == 6
            ? string.Join(":", bytes.Select(b => b.ToString("X2"))) : null;
    }
}
