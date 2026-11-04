using System.Net;
using System.Net.Sockets;

namespace Notisight.Api.Infrastructure.Http;

/// <summary>
/// Guards outbound calls to user-supplied provider base urls. The API posts the user's
/// provider key and note content to whatever address is configured here, so internal
/// targets must never be reachable.
/// </summary>
public static class SafeOutboundUrl
{
    public static async Task<bool> IsSafeAsync(string? rawUrl, CancellationToken cancellationToken)
    {
        if (!TryGetHost(rawUrl, out var host) || IsBlockedHostname(host))
        {
            return false;
        }

        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            return false;
        }

        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool TryGetHost(string? rawUrl, out string host)
    {
        host = string.Empty;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host.Trim('[', ']');

        return true;
    }

    private static bool IsBlockedHostname(string host)
    {
        if (IPAddress.TryParse(host, out _))
        {
            return false;
        }

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".home.arpa", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();

        if (bytes.Length == 4)
        {
            return !(bytes[0] == 0 ||
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && (bytes[1] == 0 || bytes[1] == 168)) ||
                (bytes[0] == 198 && bytes[1] >= 18 && bytes[1] <= 19) ||
                bytes[0] >= 224);
        }

        return !(bytes.Length == 16 &&
            (address.Equals(IPAddress.IPv6Loopback) ||
             (bytes[0] == 0xfe && (bytes[1] & 0xC0) == 0x80) ||
             (bytes[0] & 0xFE) == 0xFC ||
             bytes[0] == 0xFF ||
             (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)));
    }
}
