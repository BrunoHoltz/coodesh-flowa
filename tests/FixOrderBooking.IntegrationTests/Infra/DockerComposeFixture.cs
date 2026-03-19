using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[SetUpFixture]
public class DockerComposeFixture
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables("INTEGRATION_")
        .Build();

    private string? _dockerExe;
    private string? _composeFile;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _dockerExe = FindDockerExe();
        EnsureDockerRunning(_dockerExe);
        _composeFile = FindComposeFile();

        await RunComposeAsync("down --remove-orphans --volumes");
        await RunComposeAsync("up --build -d");

        var timeout = TimeSpan.FromSeconds(Config.GetValue("Infra:Timeouts:StartSeconds", 300));
        var fixDelay = TimeSpan.FromSeconds(Config.GetValue("Infra:Timeouts:FixSessionDelaySeconds", 5));

        await WaitForPortAsync("localhost", Config.GetValue("Infra:Endpoints:Server:HTTP:Port", 5000), "Server HTTP", timeout);
        await WaitForPortAsync("localhost", Config.GetValue("Infra:Endpoints:Server:FIX:Port",  5001), "Server FIX",  timeout);
        await WaitForPortAsync("localhost", Config.GetValue("Infra:Endpoints:Client:HTTP:Port", 5100), "Client HTTP", timeout);
        await WaitForPortAsync("localhost", Config.GetValue("Infra:Endpoints:Client:FIX:Port",  5002), "Client FIX",  timeout);

        await Task.Delay(fixDelay);
    }

    [OneTimeTearDown]
    public Task StopAsync() =>
        _dockerExe is not null && _composeFile is not null
            ? RunComposeAsync("down --remove-orphans --volumes")
            : Task.CompletedTask;

    private static string FindDockerExe()
    {
        var configured = Config["Infra:Docker:ExePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!File.Exists(configured))
                Assert.Ignore($"Configured Docker executable not found: '{configured}'. Update Infra:Docker:ExePath in appsettings.json.");
            return configured;
        }

        var locator = OperatingSystem.IsWindows() ? "where" : "which";
        try
        {
            var psi = new ProcessStartInfo(locator, "docker") { RedirectStandardOutput = true, UseShellExecute = false };
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode == 0)
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var path = line.Trim();
                    if (OperatingSystem.IsWindows() && !path.EndsWith(".exe")) path += ".exe";
                    if (File.Exists(path)) return path;
                }
        }
        catch { }

        var fallback = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "resources", "bin", "docker.exe")
            : "/usr/local/bin/docker";
        if (File.Exists(fallback)) return fallback;

        Assert.Ignore("Docker not found. Install Docker Desktop or set Infra:Docker:ExePath in appsettings.json.");
        return null!;
    }
    private static string FindComposeFile()
    {
        var configured = Config["Infra:Docker:ComposeFile"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!File.Exists(configured))
                throw new FileNotFoundException($"Compose file not found: '{configured}'. Update Infra:Docker:ComposeFile in appsettings.json.", configured);
            return configured;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docker-compose.yml");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("docker-compose.yml not found. Set Infra:Docker:ComposeFile in appsettings.json or place the file in an ancestor directory.");
    }

    private static void EnsureDockerRunning(string dockerExe)
    {
        try
        {
            var psi = new ProcessStartInfo(dockerExe, "info") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using var proc = Process.Start(psi)!;
            if (proc.WaitForExit(10_000) && proc.ExitCode == 0) return;
        }
        catch { }
        Assert.Ignore("Docker daemon is not running. Start Docker Desktop and retry.");
    }    
    private static async Task WaitForPortAsync(string host, int port, string description, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(host, port, cts.Token); return; }
            catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"{host}:{port} ({description}) did not become reachable within {timeout.TotalSeconds:N0}s.");
    }

    private async Task RunComposeAsync(string args)
    {
        var psi = new ProcessStartInfo(_dockerExe!, $"compose -f \"{_composeFile}\" {args}")
        {
            WorkingDirectory = Path.GetDirectoryName(_composeFile)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start docker compose.");
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new Exception($"docker compose {args} failed (exit {proc.ExitCode}).\n{stderr}\n{stdout}");
    }
}
