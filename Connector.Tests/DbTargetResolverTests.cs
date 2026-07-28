using Connector.Shared.Data;
using Connector.Shared.Models;

namespace Connector.Tests;

public sealed class DbTargetResolverTests
{
    private static DatabaseSettings LocalSettings() => new()
    {
        WaproMagDatabase = "LocalMag",
        WaproFakirDatabase = "LocalFakir"
    };

    [Fact]
    public void Resolve_prefers_request_database_name_over_local_settings()
    {
        var catalog = DbTargetResolver.Resolve("TenantMagDb", targetDatabase: 1, LocalSettings());

        Assert.Equal("TenantMagDb", catalog);
    }

    [Fact]
    public void Resolve_trims_request_database_name()
    {
        var catalog = DbTargetResolver.Resolve("  TenantFakirDb  ", targetDatabase: 2, LocalSettings());

        Assert.Equal("TenantFakirDb", catalog);
    }

    [Fact]
    public void Resolve_falls_back_to_local_mag_when_request_empty()
    {
        var catalog = DbTargetResolver.Resolve("  ", targetDatabase: 1, LocalSettings());

        Assert.Equal("LocalMag", catalog);
    }

    [Fact]
    public void Resolve_falls_back_to_local_fakir_when_request_empty()
    {
        var catalog = DbTargetResolver.Resolve(null, targetDatabase: 2, LocalSettings());

        Assert.Equal("LocalFakir", catalog);
    }
}
