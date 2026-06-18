using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;

namespace Connector.Installer;

internal static class Program
{
    private const string ProductName = "KSeF Hub Connector";
    private const string ServiceName = "KSeF Hub Connector";
    private static readonly string[] LegacyServiceNames = ["WaproKSeF Connector"];
    private const string InstallDirName = "KSeF Hub Connector";
    private const string ServiceDirName = "Service";
    private const string ConfiguratorDirName = "Configurator";
    private const string ServiceExeName = "Connector.Service.exe";
    private const string ConfiguratorExeName = "Connector.Configurator.exe";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Installer jest przeznaczony dla systemu Windows.");
                return 1;
            }

            if (!IsAdministrator())
            {
                return RelaunchAsAdministrator(args);
            }

            var uninstall = args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase));
            var purge = args.Any(arg => string.Equals(arg, "--purge", StringComparison.OrdinalIgnoreCase));
            if (uninstall)
            {
                Uninstall(purge);
                return 0;
            }

            Install();
            return 0;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return 1;
        }
    }

    private static void Install()
    {
        var installDir = GetInstallDir();
        var tempDir = Path.Combine(Path.GetTempPath(), "KSeFHubConnectorSetup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            Console.WriteLine($"Instalacja: {ProductName}");
            StopAndDeleteServiceIfExists();
            ExtractPayload(tempDir);
            TryDeleteDirectory(installDir);
            CopyDirectory(tempDir, installDir);
            InstallService(installDir);
            StartService();
            LaunchConfigurator(installDir);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static void Uninstall(bool purge)
    {
        Console.WriteLine($"Odinstalowanie: {ProductName}");
        StopAndDeleteServiceIfExists();
        TryDeleteDirectory(GetInstallDir());
        if (purge)
        {
            TryDeleteDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "KSeFHub",
                "Connector"));
        }
        ShowInfo(purge ? "Konektor został odinstalowany razem z konfiguracją." : "Konektor został odinstalowany. Konfiguracja w ProgramData została zachowana.");
    }

    private static string GetInstallDir()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, InstallDirName);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static int RelaunchAsAdministrator(string[] args)
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException("Nie mozna ustalic sciezki instalatora do podniesienia uprawnien administratora.");
        }
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = string.Join(" ", args.Select(QuoteArgument))
        };
        Process.Start(startInfo);
        return 0;
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\\\"") + "\""
            : value;
    }

    private static void ExtractPayload(string destination)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            throw new InvalidOperationException("Brak osadzonego payloadu instalatora. Uruchom Connector/build-installer.ps1.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Nie mozna odczytac osadzonego payloadu instalatora.");
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(destination, overwriteFiles: true);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var sourceFile in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var destinationFile = Path.Combine(destinationDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: true);
        }
    }

    private static void InstallService(string installDir)
    {
        var serviceExe = Path.Combine(installDir, ServiceDirName, ServiceExeName);
        if (!File.Exists(serviceExe))
        {
            throw new FileNotFoundException("Nie znaleziono pliku usługi konektora.", serviceExe);
        }

        RunSc("create", ServiceName, $"binPath= \"{serviceExe}\"", "start= auto", $"DisplayName= \"{ProductName}\"");
        RunSc("description", ServiceName, "\"Local connector service for KSeF Hub SaaS\"");
    }

    private static void StartService()
    {
        RunScAllowFailure("start", ServiceName);
    }

    private static void StopAndDeleteServiceIfExists()
    {
        foreach (var serviceName in LegacyServiceNames.Append(ServiceName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!ServiceExists(serviceName))
            {
                continue;
            }

            RunScAllowFailure("stop", serviceName);
            Thread.Sleep(TimeSpan.FromSeconds(2));
            RunScAllowFailure("delete", serviceName);
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }

    private static bool ServiceExists(string serviceName)
    {
        var result = RunProcess("sc.exe", $"query \"{serviceName}\"", throwOnError: false);
        return result.ExitCode == 0;
    }

    private static void RunSc(params string[] args)
    {
        var joinedArgs = string.Join(" ", args.Select(QuoteScArgument));
        RunProcess("sc.exe", joinedArgs, throwOnError: true);
    }

    private static void RunScAllowFailure(params string[] args)
    {
        var joinedArgs = string.Join(" ", args.Select(QuoteScArgument));
        RunProcess("sc.exe", joinedArgs, throwOnError: false);
    }

    private static string QuoteScArgument(string value)
    {
        return value.Contains(' ') && !value.Contains('=')
            ? "\"" + value.Replace("\"", "\\\"") + "\""
            : value;
    }

    private static ProcessResult RunProcess(string fileName, string arguments, bool throwOnError)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Nie mozna uruchomic procesu: {fileName}");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} {arguments} zakonczyl sie kodem {process.ExitCode}.\n{output}\n{error}");
        }
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static void LaunchConfigurator(string installDir)
    {
        var configurator = Path.Combine(installDir, ConfiguratorDirName, ConfiguratorExeName);
        if (!File.Exists(configurator))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = configurator,
            UseShellExecute = true
        });
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup; reinstall can overwrite files later.
        }
    }

    private static void ShowInfo(string message)
    {
        Console.WriteLine(message);
        TryShowMessageBox(message, ProductName, 0x40);
    }

    private static void ShowError(Exception ex)
    {
        Console.Error.WriteLine(ex);
        TryShowMessageBox(ex.Message, ProductName + " - blad instalacji", 0x10);
    }

    private static void TryShowMessageBox(string text, string caption, uint type)
    {
        try
        {
            _ = MessageBox(IntPtr.Zero, text, caption, type);
        }
        catch
        {
            // Console output is enough when user32 is unavailable.
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
