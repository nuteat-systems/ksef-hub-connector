using Connector.Shared.Data;
using Connector.Shared.Models;

namespace Connector.Tests;

public sealed class SqlConnectionStringFactoryTests
{
    [Fact]
    public void Build_uses_host_and_port_for_tcp_endpoint()
    {
        var settings = new DatabaseSettings
        {
            Host = "192.168.1.10",
            Port = 1433,
            WaproMagDatabase = "WaproMag",
            UseIntegratedSecurity = false,
            UserName = "sa",
            Password = "secret"
        };

        var connectionString = SqlConnectionStringFactory.Build(settings);

        Assert.Contains("Data Source=192.168.1.10,1433", connectionString);
        Assert.Contains("Initial Catalog=WaproMag", connectionString);
        Assert.Contains("User ID=sa", connectionString);
        Assert.Contains("Password=secret", connectionString);
        Assert.Contains("Encrypt=True", connectionString);
        Assert.Contains("Trust Server Certificate=True", connectionString);
    }

    [Fact]
    public void Build_preserves_named_instance_without_port_suffix()
    {
        var settings = new DatabaseSettings
        {
            Host = "SERVER\\WAPRO",
            Port = 1433,
            WaproMagDatabase = "master",
            UseIntegratedSecurity = true
        };

        var connectionString = SqlConnectionStringFactory.Build(settings);

        Assert.Contains("Data Source=SERVER\\WAPRO", connectionString);
        Assert.DoesNotContain("SERVER\\WAPRO,1433", connectionString);
        Assert.Contains("Integrated Security=True", connectionString);
    }

    [Fact]
    public void Build_allows_database_override()
    {
        var settings = new DatabaseSettings
        {
            Host = "localhost",
            WaproMagDatabase = "WaproMag"
        };

        var connectionString = SqlConnectionStringFactory.Build(settings, "master");

        Assert.Contains("Initial Catalog=master", connectionString);
    }
}
