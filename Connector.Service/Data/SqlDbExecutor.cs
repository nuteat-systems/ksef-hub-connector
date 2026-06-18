using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using Connector.Contracts.Grpc;
using Connector.Shared.Data;
using Connector.Shared.Models;
using Connector.Shared.Security;
using Connector.Shared.Storage;
using Microsoft.Data.SqlClient;

namespace Connector.Service.Data;

public sealed class SqlDbExecutor : IDbExecutor, IAsyncDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly ILogger<SqlDbExecutor> _logger;
    private readonly SqlCommandValidator _commandValidator;
    private readonly bool _trustBackendCommands;
    private readonly ConcurrentDictionary<string, SqlConnection> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new(StringComparer.Ordinal);

    public SqlDbExecutor(
        SettingsStore settingsStore,
        ILogger<SqlDbExecutor> logger,
        IConfiguration configuration)
    {
        _settingsStore = settingsStore;
        _logger = logger;
        _trustBackendCommands = configuration.GetValue<bool>("SqlSecurity:TrustBackendCommands");
        _commandValidator = new SqlCommandValidator(
            configuration.GetSection("SqlSecurity:AllowedSelectObjects").Get<string[]>() ?? Array.Empty<string>(),
            configuration.GetSection("SqlSecurity:AllowedStoredProcedures").Get<string[]>() ?? Array.Empty<string>(),
            configuration.GetValue<bool>("SqlSecurity:AllowNonQuery"));
    }

    public async Task<DbExecutionResult> ExecuteAsync(DbCommandRequest request, CancellationToken cancellationToken)
    {
        var useSession = !string.IsNullOrWhiteSpace(request.SessionId);
        if (!useSession)
        {
            return await ExecuteCoreAsync(request, useSession: false, cancellationToken);
        }

        var sessionLock = _sessionLocks.GetOrAdd(request.SessionId, _ => new SemaphoreSlim(1, 1));
        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteCoreAsync(request, useSession: true, cancellationToken);
        }
        finally
        {
            sessionLock.Release();
            if (request.CloseSession && _sessionLocks.TryRemove(request.SessionId, out var removedLock))
            {
                removedLock.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sessionId in _sessions.Keys.ToArray())
        {
            await CloseSessionAsync(sessionId);
        }

        foreach (var sessionLock in _sessionLocks.Values)
        {
            sessionLock.Dispose();
        }

        _sessionLocks.Clear();
    }

    private async Task<DbExecutionResult> ExecuteCoreAsync(DbCommandRequest request, bool useSession, CancellationToken cancellationToken)
    {
        try
        {
            if (request.CloseSession)
            {
                await CloseSessionAsync(request.SessionId);
                return new DbExecutionResult { Success = true };
            }

            var commandType = (int)request.CommandType;
            var securityError = _commandValidator.Validate(
                request.CommandText,
                commandType,
                _trustBackendCommands,
                request.CloseSession);
            if (!string.IsNullOrWhiteSpace(securityError))
            {
                return new DbExecutionResult
                {
                    Success = false,
                    ErrorMessage = securityError
                };
            }

            var settings = await _settingsStore.LoadAsync(cancellationToken);
            var targetDb = ResolveTargetDatabase(request, settings.Database);
            var connectionString = SqlConnectionStringFactory.Build(settings.Database, targetDb);

            var connection = await GetConnectionAsync(request.SessionId, connectionString, useSession, cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = request.CommandText;
                command.CommandTimeout = settings.Database.CommandTimeoutSeconds;
                command.CommandType = commandType == 3 ? CommandType.StoredProcedure : CommandType.Text;

                foreach (var parameter in request.Parameters)
                {
                    command.Parameters.AddWithValue(parameter.Name, parameter.IsNull ? DBNull.Value : parameter.Value);
                }

                return commandType switch
                {
                    1 => await ExecuteReaderCommandAsync(command, cancellationToken),
                    2 => await ExecuteNonQueryCommandAsync(command, cancellationToken),
                    3 => await ExecuteReaderCommandAsync(command, cancellationToken),
                    _ => new DbExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Unsupported command type: {request.CommandType}"
                    }
                };
            }
            finally
            {
                if (!useSession)
                {
                    await connection.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database execution failed for request {RequestId}", request.RequestId);
            return new DbExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<SqlConnection> GetConnectionAsync(
        string sessionId,
        string connectionString,
        bool useSession,
        CancellationToken cancellationToken)
    {
        if (!useSession)
        {
            var transientConnection = new SqlConnection(connectionString);
            await transientConnection.OpenAsync(cancellationToken);
            return transientConnection;
        }

        if (_sessions.TryGetValue(sessionId, out var existing) && existing.State == ConnectionState.Open)
        {
            return existing;
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        _sessions.AddOrUpdate(
            sessionId,
            connection,
            (_, previous) =>
            {
                try
                {
                    previous.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to dispose previous SQL session connection {SessionId}", sessionId);
                }

                return connection;
            });
        return connection;
    }

    private async Task CloseSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        if (!_sessions.TryRemove(sessionId, out var connection))
        {
            return;
        }

        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to close SQL session {SessionId}", sessionId);
        }
    }

    private static async Task<DbExecutionResult> ExecuteNonQueryCommandAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return new DbExecutionResult
        {
            Success = true,
            RowsAffected = rowsAffected
        };
    }

    private static async Task<DbExecutionResult> ExecuteReaderCommandAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var resultSetsJson = new List<string>();
        var totalRows = 0;
        do
        {
            if (reader.FieldCount <= 0)
            {
                continue;
            }

            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }

            totalRows += rows.Count;
            resultSetsJson.Add(JsonSerializer.Serialize(rows));
        } while (await reader.NextResultAsync(cancellationToken));

        var recordsAffected = reader.RecordsAffected;

        return new DbExecutionResult
        {
            Success = true,
            RowsAffected = resultSetsJson.Count > 0 ? totalRows : recordsAffected,
            PayloadJson = resultSetsJson.FirstOrDefault() ?? string.Empty,
            ResultSetsJson = resultSetsJson
        };
    }

    private static string ResolveTargetDatabase(DbCommandRequest request, DatabaseSettings settings)
    {
        return (int)request.TargetDatabase switch
        {
            2 when !string.IsNullOrWhiteSpace(settings.WaproFakirDatabase) => settings.WaproFakirDatabase,
            1 when !string.IsNullOrWhiteSpace(settings.WaproMagDatabase) => settings.WaproMagDatabase,
            _ => settings.GetPrimaryDatabase()
        };
    }
}
