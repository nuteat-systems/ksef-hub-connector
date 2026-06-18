using Connector.Shared.Models;
using Microsoft.Data.SqlClient;

namespace Connector.Shared.Data;

public static class SqlConnectionStringFactory
{
    public static string Build(DatabaseSettings dbSettings, string? targetDatabase = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = BuildDataSource(dbSettings),
            InitialCatalog = targetDatabase ?? dbSettings.GetPrimaryDatabase(),
            Encrypt = true,
            TrustServerCertificate = true
        };

        if (dbSettings.UseIntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = dbSettings.UserName;
            builder.Password = dbSettings.Password;
        }

        return builder.ConnectionString;
    }

    public static string BuildDataSource(DatabaseSettings dbSettings)
    {
        var host = dbSettings.Host.Trim();
        return host.Contains('\\') ? host : $"{host},{dbSettings.Port}";
    }
}
