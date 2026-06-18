using Connector.Shared.Models;

namespace Connector.Shared.Network;

public static class ServerAddressResolver
{
    public static string Resolve(ServerSettings serverSettings)
    {
        var address = serverSettings.Address.Trim();
        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return address;
        }

        var scheme = serverSettings.UseTls ? "https" : "http";
        return $"{scheme}://{address}";
    }
}
