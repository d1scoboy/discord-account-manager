using System.Text.Json;
using DiscoBox.Core.Logging;
using DiscoBox.Core.Models;
using DiscoBox.Core.Proxy;

namespace DiscoBox.Core.Chrome;

public class OpenAccountService
{
    private readonly object gate = new();
    private readonly ProfileStore profiles;
    private readonly ChromeLauncher launcher;
    private readonly DiscordAuthInjector injector;
    private readonly FileLogger logger;
    private readonly Func<string> chromePathProvider;
    private readonly Func<string> gostPathProvider;
    private readonly Action<Account, string>? onTokenRotated;
    private ChromeSession? current;

    public OpenAccountService(ProfileStore profiles, ChromeLauncher launcher, DiscordAuthInjector injector,
        FileLogger logger, Func<string> chromePathProvider, Func<string> gostPathProvider,
        Action<Account, string>? onTokenRotated = null)
    {
        this.profiles = profiles;
        this.launcher = launcher;
        this.injector = injector;
        this.logger = logger;
        this.chromePathProvider = chromePathProvider;
        this.gostPathProvider = gostPathProvider;
        this.onTokenRotated = onTokenRotated;
    }

    public string? CurrentAccountId
    {
        get
        {
            lock (gate)
            {
                return current?.AccountId;
            }
        }
    }

    public event Action<string>? SessionClosed;

    public async Task<(bool Ok, string Message)> OpenAsync(Account account, ResolvedProxy? proxy,
        CancellationToken token)
    {
        lock (gate)
        {
            // TODO second session becomes possible once profiles stop being single-writer zips
            if (current is not null)
            {
                return current.AccountId == account.Id
                    ? (true, $"Session for {account.Username} is already open")
                    : (false, "Close the open session first (one Chrome session at a time)");
            }
        }

        var chromePath = chromePathProvider();
        if (string.IsNullOrWhiteSpace(chromePath) || !File.Exists(chromePath))
        {
            return (false, "Chrome path is not set or missing (Settings → Browser)");
        }

        var workDir = Path.Combine(Path.GetTempPath(), "DiscoBox", account.Id);
        try
        {
            profiles.UnzipTo(account.Id, workDir);

            GostBridge? bridge = null;
            string? proxyServer = null;
            if (proxy is not null)
            {
                if (proxy.Endpoint.User is not null)
                {
                    var gostPath = gostPathProvider();
                    if (string.IsNullOrWhiteSpace(gostPath) || !File.Exists(gostPath))
                    {
                        return (false, "Proxy needs auth bridge: set gost path in Settings");
                    }

                    bridge = await GostBridge.StartAsync(gostPath, proxy.Endpoint, logger, token);
                    proxyServer = bridge.ProxyServerArg;
                }
                else
                {
                    proxyServer = $"{proxy.Endpoint.Scheme}://{proxy.Endpoint.Host}:{proxy.Endpoint.Port}";
                }
            }

            var launched = launcher.Launch(chromePath, account.Id, workDir, proxyServer,
                "https://discord.com/channels/@me");
            var session = new ChromeSession
            {
                AccountId = account.Id,
                Account = account,
                Process = launched.Process,
                WorkDir = launched.WorkDir,
                DebugPort = launched.DebugPort,
                Bridge = bridge,
            };

            try
            {
                session.Cdp = await injector.InjectAsync(session.DebugPort, account.Token, token);
            }
            catch (Exception exception)
            {
                logger.Warn("chrome", $"token injection failed, leaving browser as is: {exception.Message}");
            }

            lock (gate)
            {
                current = session;
            }

            _ = Task.Run(() => MonitorExitAsync(session));
            _ = Task.Run(() => TokenWatchAsync(session));
            _ = Task.Run(() => UpdaterSweepAsync(session));
            logger.Info("chrome", $"session opened for account {account.Username}");
            return (true, $"Chrome session opened for {account.Username}");
        }
        catch (Exception exception)
        {
            logger.Error("chrome", $"failed to open session for {account.Username}", exception);
            return (false, $"Failed to open session: {exception.Message}");
        }
    }

    public async Task CloseAsync()
    {
        ChromeSession? session;
        lock (gate)
        {
            session = current;
        }

        if (session is null)
        {
            return;
        }

        await HarvestTokenAsync(session);
        try
        {
            if (!session.Process.HasExited)
            {
                session.Process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception)
        {
            logger.Warn("chrome", $"session kill issue: {exception.Message}");
        }
    }

    private async Task TokenWatchAsync(ChromeSession session)
    {
        while (!session.Process.HasExited)
        {
            await Task.Delay(5000);
            await HarvestTokenAsync(session);
        }
    }

    private async Task HarvestTokenAsync(ChromeSession session)
    {
        if (session.Cdp is null)
        {
            try
            {
                var browser = await PuppeteerSharp.Puppeteer.ConnectAsync(new PuppeteerSharp.ConnectOptions
                {
                    BrowserURL = $"http://127.0.0.1:{session.DebugPort}",
                    DefaultViewport = null,
                });
                session.Cdp = new CdpHandle { Browser = browser };
                logger.Info("chrome", "token watch reconnected to chrome debug port");
            }
            catch (Exception exception)
            {
                logger.Debug("chrome", $"token watch connect failed: {exception.Message}");
                return;
            }
        }

        try
        {
            var page = await LiveDiscordPageAsync(session);
            if (page is null)
            {
                return;
            }

            session.Cdp.Page = page;
            var stored = await DiscordAuthInjector.ReadStoredTokenAsync(page);
            if (stored is not null && stored != session.Account.Token)
            {
                logger.Info("chrome", $"token rotated by discord for account {session.Account.Username}, storage updated");
                onTokenRotated?.Invoke(session.Account, stored);
            }
        }
        catch (Exception exception)
        {
            logger.Debug("chrome", $"token watch read failed: {exception.Message}");
        }
    }

    private static async Task<PuppeteerSharp.IPage?> LiveDiscordPageAsync(ChromeSession session)
    {
        var current = session.Cdp!.Page;
        if (current is not null && !current.IsClosed &&
            current.Url.StartsWith("https://discord.com", StringComparison.Ordinal))
        {
            return current;
        }

        var pages = await session.Cdp.Browser.PagesAsync();
        return pages.FirstOrDefault(p => !p.IsClosed &&
                                         p.Url.StartsWith("https://discord.com", StringComparison.Ordinal));
    }

    private async Task UpdaterSweepAsync(ChromeSession session)
    {
        while (!session.Process.HasExited)
        {
            await Task.Delay(5000);
            launcher.KillUpdaters();
        }
    }

    private async Task MonitorExitAsync(ChromeSession session)
    {
        try
        {
            await launcher.WaitForExitAsync(session);
        }
        finally
        {
            session.Bridge?.Dispose();
            try
            {
                profiles.ZipFrom(session.AccountId, session.WorkDir);
                logger.Info("chrome", $"profile for {session.AccountId} packed back to zip");
            }
            catch (Exception exception)
            {
                logger.Error("chrome", $"profile packing failed for {session.AccountId}", exception);
            }

            lock (gate)
            {
                if (ReferenceEquals(current, session))
                {
                    current = null;
                }
            }

            SessionClosed?.Invoke(session.AccountId);
        }
    }
}
