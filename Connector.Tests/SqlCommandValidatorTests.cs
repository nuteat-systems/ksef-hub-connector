using Connector.Shared.Security;

namespace Connector.Tests;

public sealed class SqlCommandValidatorTests
{
    private readonly SqlCommandValidator _validator = new(
        allowedSelectObjects: ["dbo.ARTYKUL", "dbo.FIRMA"],
        allowedStoredProcedures: ["dbo.spConnectorPing"],
        allowNonQuery: false);

    [Fact]
    public void Validate_allows_select_when_object_is_on_allowlist()
    {
        var error = _validator.Validate(
            "SELECT TOP 1 * FROM dbo.ARTYKUL",
            commandType: 1,
            trustBackendCommands: false,
            closeSession: false);

        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Validate_blocks_select_for_unknown_object()
    {
        var error = _validator.Validate(
            "SELECT * FROM dbo.SEKRET",
            commandType: 1,
            trustBackendCommands: false,
            closeSession: false);

        Assert.Contains("not allowed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_blocks_non_query_when_disabled()
    {
        var error = _validator.Validate(
            "UPDATE dbo.ARTYKUL SET nazwa = 'x'",
            commandType: 2,
            trustBackendCommands: false,
            closeSession: false);

        Assert.Contains("NON_QUERY is blocked", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_skips_policy_when_backend_is_trusted()
    {
        var error = _validator.Validate(
            "DELETE FROM dbo.SEKRET",
            commandType: 2,
            trustBackendCommands: true,
            closeSession: false);

        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Validate_allows_close_session_without_command_text_when_backend_is_trusted()
    {
        var error = _validator.Validate(
            string.Empty,
            commandType: 2,
            trustBackendCommands: true,
            closeSession: true);

        Assert.Equal(string.Empty, error);
    }
}
