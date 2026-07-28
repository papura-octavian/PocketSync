using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PocketSync.Server;

/// <summary>
/// Detectează IP-ul real din LAN, filtrând adaptoarele virtuale
/// (Hyper-V, VirtualBox, WSL, Tailscale, VPN) care ar produce un QR mort.
/// </summary>
public static class NetworkHelper
{
    private static readonly string[] VirtualMarkers =
    [
        "virtual", "vmware", "hyper-v", "vethernet", "wsl",
        "tailscale", "vpn", "loopback", "tap", "docker", "bluetooth"
    ];

    public static List<IPAddress> GetLanAddresses()
    {
        var strict = Collect(requireGateway: true);
        if (strict.Count > 0) return strict;

        // fallback: fără cerința de gateway (rețele fără default route, dar reale)
        return Collect(requireGateway: false);
    }

    public static IPAddress? GetBestLanAddress()
        => GetLanAddresses().FirstOrDefault();

    private static List<IPAddress> Collect(bool requireGateway)
    {
        var result = new List<IPAddress>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var name = (nic.Name + " " + nic.Description).ToLowerInvariant();
            if (VirtualMarkers.Any(name.Contains)) continue;

            var props = nic.GetIPProperties();

            if (requireGateway &&
                !props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                continue;

            foreach (var ua in props.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ua.Address)) continue;
                result.Add(ua.Address);
            }
        }

        return result;
    }
}
