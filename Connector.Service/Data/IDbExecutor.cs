using Connector.Contracts.Grpc;

namespace Connector.Service.Data;

public interface IDbExecutor
{
    Task<DbExecutionResult> ExecuteAsync(DbCommandRequest request, CancellationToken cancellationToken);
}
