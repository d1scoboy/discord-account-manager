using System.Diagnostics;
using System.Management;
using DiscoBox.Core.Logging;
using DiscoBox.Core.Models;

namespace DiscoBox.Core.Chrome;

public sealed class ChromeSession
{
    public required string AccountId { get; init; }

    public Account Account { get; set; } = null!;

    public required Process Process { get; init; }

    public required string WorkDir { get; init; }

    public required int DebugPort { get; init; }

    public GostBridge? Bridge { get; init; }

    public CdpHandle? Cdp { get; set; }
}

public class ChromeLauncher
{
    private static readonly string[] UpdaterNames =
    {
        "GoogleUpdate", "chrome_updater", "GoogleChromeUpdater", "elevation_service",
    };

    private readonly FileLogger logger;

    public ChromeLauncher(FileLogger logger)
    {
        this.logger = logger;
    }

    public static string[] BuildArguments(string userDataDir, string? proxyServer, int debugPort, string startUrl)
    {
        var args = new List<string>
        {
            $"--user-data-dir={userDataDir}",
        };
        if (!string.IsNullOrWhiteSpace(proxyServer))
        {
            args.Add($"--proxy-server={proxyServer}");
        }

        args.AddRange(new[]
        {
            $"--remote-debugging-port={debugPort}",
            "--restore-last-session",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-component-update",
            "--disable-background-networking",
            "--disable-domain-reliability",
            "--disable-sync",
            "--disable-default-apps",
            "--disable-extensions",
            "--disable-translate",
            "--no-service-autorun",
            "--metrics-recording-only",
            "--mute-audio",
            "--disable-breakpad",
            "--lang=en-US",
            "--accept-lang=en-US,en",
            startUrl,
        });
        return args.ToArray();
    }

    public ChromeSession Launch(string chromePath, string accountId, string workDir, string? proxyServer,
        string startUrl = "https://discord.com/login")
    {
        var debugPort = GostBridge.FreePort();
        var args = BuildArguments(workDir, proxyServer, debugPort, startUrl);
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = string.Join(' ', args),
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("failed to start chrome");

        KillUpdaters();
        logger.Info("chrome", $"launched chrome pid {process.Id} for account {accountId}, debug port {debugPort}");
        return new ChromeSession
        {
            AccountId = accountId,
            Process = process,
            WorkDir = workDir,
            DebugPort = debugPort,
        };
    }

    public void KillUpdaters()
    {
        foreach (var name in UpdaterNames)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    process.Kill(entireProcessTree: false);
                    process.Dispose();
                }
            }
            catch (Exception exception)
            {
                logger.Debug("chrome", $"updater kill {name}: {exception.Message}");
            }
        }
    }

    public void KillOrphans(string workDir)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE CommandLine LIKE '%{workDir.Replace("\\", "\\\\")}%'");
            foreach (var obj in searcher.Get())
            {
                var pid = Convert.ToInt32(obj["ProcessId"]);
                try
                {
                    using var process = Process.GetProcessById(pid);
                    if (process.ProcessName.Contains("chrome", StringComparison.OrdinalIgnoreCase)
                        || process.ProcessName.Contains("crashpad", StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill(entireProcessTree: false);
                        logger.Info("chrome", $"killed orphan {process.ProcessName} pid {pid}");
                    }
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
        catch (Exception exception)
        {
            logger.Warn("chrome", $"orphan sweep failed: {exception.Message}");
        }
    }

    public async Task WaitForExitAsync(ChromeSession session)
    {
        await session.Process.WaitForExitAsync();
        KillOrphans(session.WorkDir);
        KillUpdaters();
        logger.Info("chrome", $"chrome exited for account {session.AccountId} (code {session.Process.ExitCode})");
    }
}
