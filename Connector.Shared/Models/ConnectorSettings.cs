using System.Text.Json.Serialization;

namespace Connector.Shared.Models;

public sealed class ConnectorSettings
{
    public const string DefaultFileName = "connector.settings.json";

    public ServerSettings Server { get; set; } = new();

    public DatabaseSettings Database { get; set; } = new();

    public RuntimeSettings Runtime { get; set; } = new();
}

public sealed class ServerSettings
{
    public string Address { get; set; } = "https://connector.ksefhub.app";

    public string ConnectorId { get; set; } = Environment.MachineName;

    [JsonIgnore]
    public string AccessToken { get; set; } = string.Empty;

    // Keep encrypted blob in file; plain text token remains in memory only.
    public string EncryptedAccessToken { get; set; } = string.Empty;

    // Legacy plain-text field from older settings files; migrated on load and not written back.
    [JsonPropertyName("accessToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPlainAccessToken { get; set; }

    public bool UseTls { get; set; } = true;
}

public sealed class DatabaseSettings
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1433;

    // Legacy field kept for backward compatibility with old settings files.
    public string Database { get; set; } = "master";

    public string WaproMagDatabase { get; set; } = "master";

    public string WaproFakirDatabase { get; set; } = "master";

    public bool UseIntegratedSecurity { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    [JsonIgnore]
    public string Password { get; set; } = string.Empty;

    // Keep encrypted blob in file; plain text password remains in memory only.
    public string EncryptedPassword { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public string GetPrimaryDatabase()
    {
        if (!string.IsNullOrWhiteSpace(WaproMagDatabase))
        {
            return WaproMagDatabase;
        }

        if (!string.IsNullOrWhiteSpace(Database))
        {
            return Database;
        }

        return WaproFakirDatabase;
    }
}

public sealed class RuntimeSettings
{
    public int ReconnectDelaySeconds { get; set; } = 5;

    public int HeartbeatIntervalSeconds { get; set; } = 15;
}
