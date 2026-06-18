using Connector.Service.Grpc;
using Connector.Service.Data;

namespace Connector.Service.Workers;

public sealed class ConnectorWorker(
    GrpcConnectorClient grpcConnectorClient,
    SqlDbExecutor sqlDbExecutor,
    ILogger<ConnectorWorker> logger) : BackgroundService
{
    private readonly GrpcConnectorClient _grpcConnectorClient = grpcConnectorClient;
    private readonly SqlDbExecutor _sqlDbExecutor = sqlDbExecutor;
    private readonly ILogger<ConnectorWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Connector worker started.");
        await _grpcConnectorClient.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connector worker stopping.");
        await base.StopAsync(cancellationToken);
        await _sqlDbExecutor.DisposeAsync();
    }
}
