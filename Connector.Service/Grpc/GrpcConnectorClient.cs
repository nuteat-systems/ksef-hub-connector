using System.Diagnostics;
using Connector.Contracts.Grpc;
using Connector.Service.Data;
using Connector.Shared;
using Connector.Shared.Network;
using Connector.Shared.Storage;
using Grpc.Core;
using Grpc.Net.Client;

namespace Connector.Service.Grpc;

public sealed class GrpcConnectorClient(
    SettingsStore settingsStore,
    IDbExecutor dbExecutor,
    ILogger<GrpcConnectorClient> logger,
    IConfiguration configuration)
{
    private readonly SettingsStore _settingsStore = settingsStore;
    private readonly IDbExecutor _dbExecutor = dbExecutor;
    private readonly ILogger<GrpcConnectorClient> _logger = logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _dbRequestSemaphore = new(NormalizeMaxConcurrentDbRequests(configuration));

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var settings = await _settingsStore.LoadAsync(cancellationToken);
                var serverAddress = ServerAddressResolver.Resolve(settings.Server);
                _logger.LogInformation("Connecting to gRPC server: {ServerAddress}", serverAddress);

                using var channel = GrpcChannel.ForAddress(serverAddress);
                var client = new ConnectorGateway.ConnectorGatewayClient(channel);
                var metadata = new Metadata();
                if (!string.IsNullOrWhiteSpace(settings.Server.AccessToken))
                {
                    metadata.Add("authorization", $"Bearer {settings.Server.AccessToken}");
                }

                using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var sessionToken = sessionCts.Token;

                using var call = client.Connect(headers: metadata, cancellationToken: sessionToken);
                await SendHelloAsync(call, settings.Server.ConnectorId, sessionToken);
                _logger.LogInformation("gRPC session started for connector: {ConnectorId}", settings.Server.ConnectorId);

                var heartbeatTask = SendHeartbeatsAsync(call, settings.Runtime.HeartbeatIntervalSeconds, sessionToken);
                var dbTasks = new List<Task>();

                try
                {
                    await foreach (var message in call.ResponseStream.ReadAllAsync(sessionToken))
                    {
                        if (message.PayloadCase != ServerMessage.PayloadOneofCase.DbRequest)
                        {
                            continue;
                        }

                        dbTasks.RemoveAll(task => task.IsCompleted);
                        await _dbRequestSemaphore.WaitAsync(sessionToken);
                        var dbRequest = message.DbRequest;
                        dbTasks.Add(HandleDbRequestAsync(call, dbRequest, sessionToken));
                    }

                    if (dbTasks.Count > 0)
                    {
                        await Task.WhenAll(dbTasks);
                    }
                }
                finally
                {
                    await sessionCts.CancelAsync();
                    try
                    {
                        await heartbeatTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when the gRPC session ends.
                    }
                }
            }
            catch (RpcException rpcEx) when (rpcEx.StatusCode is StatusCode.Cancelled or StatusCode.Unavailable)
            {
                _logger.LogWarning("gRPC connection dropped: {Status} ({Details})", rpcEx.StatusCode, rpcEx.Status.Detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connector loop failed.");
            }

            var reconnectDelay = await GetReconnectDelayAsync(cancellationToken);
            _logger.LogInformation("Reconnecting in {DelaySeconds}s...", reconnectDelay);
            await Task.Delay(TimeSpan.FromSeconds(reconnectDelay), cancellationToken);
        }
    }

    private async Task HandleDbRequestAsync(
        AsyncDuplexStreamingCall<ClientMessage, ServerMessage> call,
        DbCommandRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Received DB request {RequestId}", request.RequestId);
            var result = await _dbExecutor.ExecuteAsync(request, cancellationToken);
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds >= 1000)
            {
                _logger.LogWarning(
                    "Slow DB request {RequestId}: {ElapsedMs} ms success={Success}",
                    request.RequestId,
                    stopwatch.ElapsedMilliseconds,
                    result.Success);
            }
            await SendResultAsync(call, request.RequestId, result, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "DB request {RequestId} failed after {ElapsedMs} ms", request.RequestId, stopwatch.ElapsedMilliseconds);
            try
            {
                await SendResultAsync(
                    call,
                    request.RequestId,
                    new DbExecutionResult { Success = false, ErrorMessage = ex.Message },
                    cancellationToken);
            }
            catch (Exception sendEx)
            {
                _logger.LogDebug(sendEx, "Failed to send DB error result for request {RequestId}", request.RequestId);
            }
        }
        finally
        {
            _dbRequestSemaphore.Release();
        }
    }

    private static async Task SendHelloAsync(AsyncDuplexStreamingCall<ClientMessage, ServerMessage> call, string connectorId, CancellationToken cancellationToken)
    {
        var hello = new ClientMessage
        {
            Hello = new ClientHello
            {
                ConnectorId = connectorId,
                MachineName = Environment.MachineName,
                Version = ConnectorVersion.Current
            }
        };

        await call.RequestStream.WriteAsync(hello, cancellationToken);
    }

    private async Task SendHeartbeatsAsync(AsyncDuplexStreamingCall<ClientMessage, ServerMessage> call, int heartbeatIntervalSeconds, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(heartbeatIntervalSeconds, 2)), cancellationToken);

            var heartbeat = new ClientMessage
            {
                Heartbeat = new Heartbeat
                {
                    UnixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            await SafeWriteAsync(call, heartbeat, cancellationToken);
        }
    }

    private async Task SendResultAsync(
        AsyncDuplexStreamingCall<ClientMessage, ServerMessage> call,
        string requestId,
        DbExecutionResult result,
        CancellationToken cancellationToken)
    {
        var payload = new ClientMessage
        {
            DbResult = new DbCommandResult
            {
                RequestId = requestId,
                Success = result.Success,
                PayloadJson = result.PayloadJson,
                RowsAffected = result.RowsAffected,
                ErrorMessage = result.ErrorMessage
            }
        };
        payload.DbResult.ResultSetsJson.AddRange(result.ResultSetsJson);

        await SafeWriteAsync(call, payload, cancellationToken);
    }

    private async Task SafeWriteAsync(
        AsyncDuplexStreamingCall<ClientMessage, ServerMessage> call,
        ClientMessage message,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await call.RequestStream.WriteAsync(message, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<int> GetReconnectDelayAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        return Math.Max(settings.Runtime.ReconnectDelaySeconds, 1);
    }

    private static int NormalizeMaxConcurrentDbRequests(IConfiguration configuration)
    {
        var value = configuration.GetValue<int?>("Connector:MaxConcurrentDbRequests") ?? 4;
        return Math.Clamp(value, 1, 16);
    }
}
