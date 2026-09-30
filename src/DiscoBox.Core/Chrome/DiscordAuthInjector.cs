using System.Text.Json;
using DiscoBox.Core.Logging;
using PuppeteerSharp;

namespace DiscoBox.Core.Chrome;

public class CdpHandle
{
    public required IBrowser Browser { get; init; }

    public IPage Page { get; set; } = null!;
}

public class DiscordAuthInjector
{
    private readonly FileLogger logger;

    public DiscordAuthInjector(FileLogger logger)
    {
        this.logger = logger;
    }

    public async Task<CdpHandle> InjectAsync(int debugPort, string token, CancellationToken token2 = default)
    {
        await WaitForDebugPortAsync(debugPort, token2);

        var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserURL = $"http://127.0.0.1:{debugPort}",
            DefaultViewport = null,
        });

        var page = await FindDiscordPageAsync(browser, token2);
        var extras = (await browser.PagesAsync()).Where(p => p != page).ToList();

        await page.WaitForFunctionAsync("() => typeof localStorage !== 'undefined'");
        var wanted = JsonSerializer.Serialize(token);
        var current = await page.EvaluateExpressionAsync<string>("localStorage.getItem('token')");
        if (current != wanted)
        {
            await page.EvaluateFunctionAsync("(t) => localStorage.setItem('token', t)", wanted);
            logger.Info("cdp", "token written to localStorage, reloading into app");
            await page.GoToAsync("https://discord.com/channels/@me", WaitUntilNavigation.DOMContentLoaded);
        }
        else
        {
            logger.Info("cdp", "profile already carries the token, injection skipped");
        }

        await page.BringToFrontAsync();
        foreach (var extra in extras)
        {
            try
            {
                await extra.CloseAsync();
            }
            catch (Exception exception)
            {
                logger.Debug("cdp", $"stray tab close: {exception.Message}");
            }
        }

        return new CdpHandle { Browser = browser, Page = page };
    }

    public static async Task<string?> ReadStoredTokenAsync(IPage page)
    {
        var raw = await page.EvaluateExpressionAsync<string>("localStorage.getItem('token')");
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(raw);
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static async Task<IPage> FindDiscordPageAsync(IBrowser browser, CancellationToken token)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            var page = (await browser.PagesAsync())
                .FirstOrDefault(p => p.Url.StartsWith("https://discord.com", StringComparison.Ordinal));
            if (page is not null)
            {
                return page;
            }

            await Task.Delay(300, token);
        }

        var created = await browser.NewPageAsync();
        await created.GoToAsync("https://discord.com/login", WaitUntilNavigation.DOMContentLoaded);
        return created;
    }

    private async Task WaitForDebugPortAsync(int debugPort, CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var response = await client.GetAsync($"http://127.0.0.1:{debugPort}/json/version");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(250, token);
        }

        throw new TimeoutException($"chrome debug port {debugPort} did not open in 20s");
    }
}
