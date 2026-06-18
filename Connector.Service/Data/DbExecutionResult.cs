namespace Connector.Service.Data;

public sealed class DbExecutionResult
{
    public bool Success { get; init; }

    public string PayloadJson { get; init; } = string.Empty;

    public int RowsAffected { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> ResultSetsJson { get; init; } = Array.Empty<string>();
}
