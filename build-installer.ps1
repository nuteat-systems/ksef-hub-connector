param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "artifacts"
}

$publishRoot = Join-Path $root "publish"
$servicePublish = Join-Path $publishRoot "Service"
$configuratorPublish = Join-Path $publishRoot "Configurator"
$payloadRoot = Join-Path $publishRoot "Payload"
$payloadServiceDir = Join-Path $payloadRoot "Service"
$payloadConfiguratorDir = Join-Path $payloadRoot "Configurator"
$installerPayloadDir = Join-Path $root "Connector.Installer\Payload"
$payloadZip = Join-Path $installerPayloadDir "payload.zip"
$installerPublish = Join-Path $publishRoot "Installer"
$finalExe = Join-Path $OutputDir "KSeFHubConnectorSetup.exe"

Remove-Item -Recurse -Force $publishRoot -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $installerPayloadDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $servicePublish, $configuratorPublish, $payloadRoot, $payloadServiceDir, $payloadConfiguratorDir, $installerPayloadDir, $OutputDir | Out-Null

Write-Host "Publishing Connector.Service..."
dotnet publish (Join-Path $root "Connector.Service\Connector.Service.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $servicePublish

Write-Host "Publishing Connector.Configurator..."
dotnet publish (Join-Path $root "Connector.Configurator\Connector.Configurator.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $configuratorPublish

$requiredServiceFiles = @(
    "Connector.Service.exe",
    "Microsoft.Extensions.Logging.Abstractions.dll",
    "Microsoft.Extensions.Hosting.dll",
    "Grpc.Net.Client.dll",
    "Microsoft.Data.SqlClient.dll"
)

foreach ($fileName in $requiredServiceFiles) {
    $filePath = Join-Path $servicePublish $fileName
    if (-not (Test-Path $filePath)) {
        throw "Service publish is incomplete. Missing required file: $filePath"
    }
}

Copy-Item -Recurse -Force (Join-Path $servicePublish "*") $payloadServiceDir
Copy-Item -Recurse -Force (Join-Path $configuratorPublish "*") $payloadConfiguratorDir

Write-Host "Creating installer payload..."
Compress-Archive -Path (Join-Path $payloadRoot "*") -DestinationPath $payloadZip -Force

Write-Host "Publishing installer EXE..."
dotnet publish (Join-Path $root "Connector.Installer\Connector.Installer.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -o $installerPublish

$builtExe = Join-Path $installerPublish "KSeFHubConnectorSetup.exe"
if (-not (Test-Path $builtExe)) {
    throw "Installer EXE not found: $builtExe"
}

Copy-Item -Force $builtExe $finalExe
Write-Host "Installer ready: $finalExe"
