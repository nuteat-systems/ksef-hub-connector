using System.Text.Json;
using System.Runtime.Versioning;
using Connector.Shared.Models;
using Connector.Shared.Security;

namespace Connector.Shared.Storage;

[SupportedOSPlatform("windows")]
public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly string? _legacySettingsPath;

    public SettingsStore(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "KSeFHub",
            "Connector");

        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, ConnectorSettings.DefaultFileName);

        if (baseDirectory is null)
        {
            _legacySettingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "WaproKSeFHub",
                "Connector",
                ConnectorSettings.DefaultFileName);
        }
    }

    public string SettingsPath => _settingsPath;

    public async Task<ConnectorSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            if (!string.IsNullOrWhiteSpace(_legacySettingsPath) && File.Exists(_legacySettingsPath))
            {
                File.Copy(_legacySettingsPath, _settingsPath, overwrite: false);
            }
        }

        if (!File.Exists(_settingsPath))
        {
            return new ConnectorSettings();
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<ConnectorSettings>(stream, _jsonOptions, cancellationToken)
            ?? new ConnectorSettings();

        settings.Database.Password = SecretProtector.Unprotect(settings.Database.EncryptedPassword);
        HydrateSecrets(settings);
        return settings;
    }

    public async Task SaveAsync(ConnectorSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Database.EncryptedPassword = SecretProtector.Protect(settings.Database.Password);
        settings.Server.EncryptedAccessToken = SecretProtector.Protect(settings.Server.AccessToken);
        settings.Server.LegacyPlainAccessToken = null;
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
    }

    private static void HydrateSecrets(ConnectorSettings settings)
    {
        settings.Server.AccessToken = SecretProtector.Unprotect(settings.Server.EncryptedAccessToken);
        if (string.IsNullOrWhiteSpace(settings.Server.AccessToken) &&
            !string.IsNullOrWhiteSpace(settings.Server.LegacyPlainAccessToken))
        {
            settings.Server.AccessToken = settings.Server.LegacyPlainAccessToken;
        }
    }
}
