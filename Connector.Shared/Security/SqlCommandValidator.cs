using System.Text.RegularExpressions;

namespace Connector.Shared.Security;

public sealed class SqlCommandValidator
{
    private static readonly Regex FromOrJoinRegex = new(
        @"\b(?:FROM|JOIN)\s+([\[\]\w\.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HashSet<string> _allowedSelectObjects;
    private readonly HashSet<string> _allowedStoredProcedures;
    private readonly bool _allowNonQuery;

    public SqlCommandValidator(
        IEnumerable<string> allowedSelectObjects,
        IEnumerable<string> allowedStoredProcedures,
        bool allowNonQuery)
    {
        _allowedSelectObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in allowedSelectObjects)
        {
            var normalized = NormalizeIdentifier(item);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _allowedSelectObjects.Add(normalized);
            }
        }

        _allowedStoredProcedures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in allowedStoredProcedures)
        {
            var normalized = NormalizeIdentifier(item);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _allowedStoredProcedures.Add(normalized);
            }
        }

        _allowNonQuery = allowNonQuery;
    }

    public string Validate(
        string commandText,
        int commandType,
        bool trustBackendCommands,
        bool closeSession)
    {
        if (trustBackendCommands)
        {
            return closeSession || !string.IsNullOrWhiteSpace(commandText)
                ? string.Empty
                : "Command text is empty.";
        }

        if (string.IsNullOrWhiteSpace(commandText))
        {
            return "Command text is empty.";
        }

        return commandType switch
        {
            1 => ValidateSelect(commandText),
            2 => ValidateNonQuery(),
            3 => ValidateStoredProcedure(commandText),
            _ => $"Unsupported command type: {commandType}"
        };
    }

    private string ValidateSelect(string commandText)
    {
        if (_allowedSelectObjects.Count == 0)
        {
            return "SELECT is blocked. No allowlist configured (SqlSecurity:AllowedSelectObjects).";
        }

        if (!commandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return "Only SELECT statements are allowed for command type SELECT.";
        }

        if (commandText.Contains(';'))
        {
            return "Multi-statement SQL is blocked.";
        }

        var matches = FromOrJoinRegex.Matches(commandText);
        if (matches.Count == 0)
        {
            return "SELECT must include FROM/JOIN with explicit object names.";
        }

        foreach (Match match in matches)
        {
            var requestedObject = NormalizeIdentifier(match.Groups[1].Value);
            if (!_allowedSelectObjects.Contains(requestedObject))
            {
                return $"SELECT object not allowed: {requestedObject}";
            }
        }

        return string.Empty;
    }

    private string ValidateStoredProcedure(string commandText)
    {
        if (_allowedStoredProcedures.Count == 0)
        {
            return "Stored procedures are blocked. No allowlist configured (SqlSecurity:AllowedStoredProcedures).";
        }

        var nameToken = commandText.Trim()
            .Split([' ', '\t', '\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(nameToken))
        {
            return "Stored procedure name is empty.";
        }

        var normalized = NormalizeIdentifier(nameToken);
        if (!_allowedStoredProcedures.Contains(normalized))
        {
            return $"Stored procedure not allowed: {normalized}";
        }

        return string.Empty;
    }

    private string ValidateNonQuery()
    {
        return _allowNonQuery
            ? string.Empty
            : "NON_QUERY is blocked by policy (SqlSecurity:AllowNonQuery=false).";
    }

    internal static string NormalizeIdentifier(string value)
    {
        return value.Replace("[", string.Empty).Replace("]", string.Empty).Trim();
    }
}
