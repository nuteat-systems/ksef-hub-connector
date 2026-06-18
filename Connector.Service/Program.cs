using Connector.Service.Data;
using Connector.Service.Grpc;
using Connector.Service.Workers;
using Connector.Shared.Storage;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
var isWindowsService = WindowsServiceHelpers.IsWindowsService();

if (isWindowsService)
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "KSeF Hub Connector";
    });
}

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<SqlDbExecutor>();
builder.Services.AddSingleton<IDbExecutor>(sp => sp.GetRequiredService<SqlDbExecutor>());
builder.Services.AddSingleton<GrpcConnectorClient>();
builder.Services.AddHostedService<ConnectorWorker>();

var logFile = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "KSeFHub",
    "Connector",
    "logs",
    "connector-.log");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

builder.Logging.ClearProviders();
if (isWindowsService)
{
    builder.Logging.AddEventLog();
}
else
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
}

builder.Services.AddSerilog();

await builder.Build().RunAsync();
