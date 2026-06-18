using System.ServiceProcess;
using System.Windows;
using Connector.Contracts.Grpc;
using Connector.Shared;
using Connector.Shared.Data;
using Connector.Shared.Models;
using Connector.Shared.Network;
using Connector.Shared.Storage;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Data.SqlClient;

namespace Connector.Configurator;

public partial class MainWindow : Window
{
    private const string ServiceName = "KSeF Hub Connector";
    private const string DefaultServerAddress = "https://connector.ksefhub.app";
    private const int DefaultCommandTimeoutSeconds = 30;

    private readonly SettingsStore _settingsStore = new();
    private ConnectorSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsStore.LoadAsync();
        HydrateLegacyDatabaseFields(_settings);
        BindToUi(_settings);
        SetStatus($"Załadowano ustawienia: {_settingsStore.SettingsPath}");
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = ReadFromUi();
            await _settingsStore.SaveAsync(_settings);
            SetStatus("Ustawienia zapisane. Wykonaj testy i zrestartuj usługę.");
        }
        catch (Exception ex)
        {
            SetStatus($"Błąd zapisu: {ex.Message}");
        }
    }

    private async void TestGrpcButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var candidate = ReadFromUi();
            var serverAddress = ServerAddressResolver.Resolve(candidate.Server);
            using var channel = GrpcChannel.ForAddress(serverAddress);
            var client = new ConnectorGateway.ConnectorGatewayClient(channel);

            var metadata = new Metadata();
            if (!string.IsNullOrWhiteSpace(candidate.Server.AccessToken))
            {
                metadata.Add("authorization", $"Bearer {candidate.Server.AccessToken}");
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var call = client.Connect(headers: metadata, cancellationToken: cts.Token);

            await call.RequestStream.WriteAsync(new ClientMessage
            {
                Hello = new ClientHello
                {
                    ConnectorId = candidate.Server.ConnectorId,
                    MachineName = Environment.MachineName,
                    Version = ConnectorVersion.Current
                }
            });

            var ackReceived = false;
            await foreach (var message in call.ResponseStream.ReadAllAsync(cts.Token))
            {
                if (message.PayloadCase == ServerMessage.PayloadOneofCase.Ack)
                {
                    ackReceived = true;
                    break;
                }
            }

            if (!ackReceived)
            {
                throw new InvalidOperationException("Brak ACK z serwera gRPC.");
            }

            await call.RequestStream.CompleteAsync();
            SetStatus("Test gRPC: połączenie + token OK.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            SetStatus("Test gRPC: błąd autoryzacji (token). ");
        }
        catch (Exception ex)
        {
            SetStatus($"Test gRPC: błąd ({ex.Message})");
        }
    }

    private async void TestSqlButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var candidate = ReadFromUi();
            var connectionString = SqlConnectionStringFactory.Build(candidate.Database);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            SetStatus("Test SQL: połączenie OK.");
        }
        catch (Exception ex)
        {
            SetStatus($"Test SQL: błąd ({ex.Message})");
        }
    }

    private async void RestartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Task.Run(() =>
            {
                using var controller = new ServiceController(ServiceName);
                if (controller.Status is ServiceControllerStatus.Running)
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }

                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            });

            SetStatus("Usluga zostala zrestartowana.");
        }
        catch (Exception ex)
        {
            SetStatus($"Restart usługi nieudany: {ex.Message}");
        }
    }

    private void IntegratedSecurityCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = !(IntegratedSecurityCheckBox.IsChecked ?? true);
        DbUserTextBox.IsEnabled = enabled;
        DbPasswordBox.IsEnabled = enabled;
    }

    private async void LoadDatabasesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var candidate = ReadFromUi();
            var connectionString = SqlConnectionStringFactory.Build(candidate.Database, "master");
            SetStatus("Pobieranie listy baz...");

            var databases = new List<string>();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(
                "SELECT [name] FROM sys.databases WHERE [state] = 0 ORDER BY [name];",
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                databases.Add(reader.GetString(0));
            }

            DbWaproMagComboBox.ItemsSource = databases;
            DbWaproFakirComboBox.ItemsSource = databases;

            if (databases.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(DbWaproMagComboBox.Text))
                {
                    DbWaproMagComboBox.Text = databases[0];
                }

                if (string.IsNullOrWhiteSpace(DbWaproFakirComboBox.Text))
                {
                    DbWaproFakirComboBox.Text = databases[0];
                }

                SetStatus($"Pobrano bazy: {databases.Count}");
            }
            else
            {
                SetStatus("Brak baz do wyswietlenia.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Pobieranie baz: błąd ({ex.Message})");
        }
    }

    private ConnectorSettings ReadFromUi()
    {
        var dbPort = int.TryParse(DbPortTextBox.Text, out var parsedPort) ? parsedPort : 1433;
        var waproMagDb = DbWaproMagComboBox.Text.Trim();
        var waproFakirDb = DbWaproFakirComboBox.Text.Trim();
        var serverAddress = ServerAddressTextBox.Text.Trim();

        return new ConnectorSettings
        {
            Server = new ServerSettings
            {
                Address = string.IsNullOrWhiteSpace(serverAddress) ? DefaultServerAddress : serverAddress,
                ConnectorId = ConnectorIdTextBox.Text.Trim(),
                AccessToken = AccessTokenTextBox.Text.Trim(),
                UseTls = true
            },
            Database = new DatabaseSettings
            {
                Host = DbHostComboBox.Text.Trim(),
                Port = dbPort,
                Database = waproMagDb,
                WaproMagDatabase = waproMagDb,
                WaproFakirDatabase = waproFakirDb,
                UseIntegratedSecurity = IntegratedSecurityCheckBox.IsChecked ?? true,
                UserName = DbUserTextBox.Text.Trim(),
                Password = DbPasswordBox.Password,
                CommandTimeoutSeconds = _settings.Database.CommandTimeoutSeconds > 0
                    ? _settings.Database.CommandTimeoutSeconds
                    : DefaultCommandTimeoutSeconds
            },
            Runtime = _settings.Runtime ?? new RuntimeSettings()
        };
    }

    private void BindToUi(ConnectorSettings settings)
    {
        ServerAddressTextBox.Text = string.IsNullOrWhiteSpace(settings.Server.Address) ? DefaultServerAddress : settings.Server.Address;
        ConnectorIdTextBox.Text = settings.Server.ConnectorId;
        AccessTokenTextBox.Text = settings.Server.AccessToken;

        DbHostComboBox.Text = settings.Database.Host;
        DbPortTextBox.Text = settings.Database.Port.ToString();
        DbWaproMagComboBox.Text = settings.Database.WaproMagDatabase;
        DbWaproFakirComboBox.Text = settings.Database.WaproFakirDatabase;
        IntegratedSecurityCheckBox.IsChecked = settings.Database.UseIntegratedSecurity;
        DbUserTextBox.Text = settings.Database.UserName;
        DbPasswordBox.Password = settings.Database.Password;

        IntegratedSecurityCheckBox_Changed(this, new RoutedEventArgs());
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = $"Status: {message}";
    }

    private static void HydrateLegacyDatabaseFields(ConnectorSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Database.WaproMagDatabase))
        {
            settings.Database.WaproMagDatabase = settings.Database.Database;
        }

        if (string.IsNullOrWhiteSpace(settings.Database.WaproFakirDatabase))
        {
            settings.Database.WaproFakirDatabase = settings.Database.Database;
        }
    }
}
