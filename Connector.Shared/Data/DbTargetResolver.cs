using Connector.Shared.Models;

namespace Connector.Shared.Data;

public static class DbTargetResolver
{
    /// <summary>
    /// Resolves the SQL catalog for a request. Non-empty <paramref name="requestDatabaseName"/>
    /// (from Hub) wins; otherwise Mag/Fakir names from local connector settings are used.
    /// </summary>
    public static string Resolve(string? requestDatabaseName, int targetDatabase, DatabaseSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(requestDatabaseName))
        {
            return requestDatabaseName.Trim();
        }

        return targetDatabase switch
        {
            2 when !string.IsNullOrWhiteSpace(settings.WaproFakirDatabase) => settings.WaproFakirDatabase,
            1 when !string.IsNullOrWhiteSpace(settings.WaproMagDatabase) => settings.WaproMagDatabase,
            _ => settings.GetPrimaryDatabase()
        };
    }
}
