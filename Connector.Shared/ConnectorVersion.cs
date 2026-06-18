using System.Reflection;

namespace Connector.Shared;

public static class ConnectorVersion
{
    public static string Current { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        return version.Revision >= 0
            ? version.ToString(3)
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
