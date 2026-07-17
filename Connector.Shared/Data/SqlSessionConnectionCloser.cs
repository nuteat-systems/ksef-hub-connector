using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Connector.Shared.Data;

/// <summary>
/// Best-effort close of a sticky SQL session connection (rollback open tran, then dispose).
/// </summary>
public static class SqlSessionConnectionCloser
{
    public static async Task CloseAsync(
        SqlConnection connection,
        string sessionId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            if (connection.State == ConnectionState.Open)
            {
                try
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION";
                    command.CommandTimeout = 5;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(
                        ex,
                        "Best-effort ROLLBACK failed while closing SQL session {SessionId}",
                        sessionId);
                }
            }
        }
        finally
        {
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to dispose SQL session connection {SessionId}", sessionId);
            }
        }
    }
}
