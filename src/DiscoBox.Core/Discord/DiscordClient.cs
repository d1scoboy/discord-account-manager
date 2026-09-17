using System.Net;
using System.Text.Json;
using DiscoBox.Core.Logging;
using DiscoBox.Core.Models;
using DiscoBox.Core.Net;

namespace DiscoBox.Core.Discord;

public class DiscordProfile
{
    public string Id { get; set; } = "";

    public string Username { get; set; } = "";

    public string? Avatar { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool MfaEnabled { get; set; }

    public bool Verified { get; set; }

    public int PremiumType { get; set; }

    public int Flags { get; set; }

    public string? Locale { get; set; }
}

public class CheckResult
{
    public AccountStatus Status { get; init; }

    public DiscordProfile? Profile { get; init; }

    public int? StatusCode { get; init; }

    public string? Error { get; init; }

    public TimeSpan Elapsed { get; init; }
}

public class DiscordClient
{
    private const string Source = "discord";

    private readonly FileLogger logger;
    private readonly string baseUrl;

    public DiscordClient(FileLogger logger, string baseUrl = "https://discord.com/api/v9")
    {
        this.logger = logger;
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<CheckResult> CheckAccountAsync(string token, ProxyEndpoint? proxy,
        int timeoutSeconds = 20, CancellationToken token2 = default)
    {
        var url = $"{baseUrl}/users/@me";
        var started = DateTime.UtcNow;
        logger.Info(Source, $"GET {url} via {(proxy?.MaskedUrl ?? "direct")}");

        try
        {
            using var client = HttpFactory.Create(proxy, timeoutSeconds);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", token);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await client.SendAsync(request, token2);
            var body = await response.Content.ReadAsStringAsync(token2);
            var elapsed = DateTime.UtcNow - started;
            var result = Map((int)response.StatusCode, body, elapsed);
            logger.Info(Source,
                $"GET {url} -> {(int)response.StatusCode} {result.Status} in {elapsed.TotalMilliseconds:F0} ms");
            return result;
        }
        catch (Exception exception)
        {
            var elapsed = DateTime.UtcNow - started;
            logger.Error(Source, $"GET {url} failed after {elapsed.TotalMilliseconds:F0} ms", exception);
            return new CheckResult
            {
                Status = AccountStatus.Error,
                Error = exception.Message,
                Elapsed = elapsed,
            };
        }
    }

    private CheckResult Map(int statusCode, string body, TimeSpan elapsed)
    {
        switch (statusCode)
        {
            case 200:
                return new CheckResult
                {
                    Status = AccountStatus.Valid,
                    Profile = ParseProfile(body),
                    StatusCode = statusCode,
                    Elapsed = elapsed,
                };
            case 401:
                return new CheckResult { Status = AccountStatus.Invalid, StatusCode = statusCode, Elapsed = elapsed };
            case 403:
                return new CheckResult { Status = AccountStatus.Locked, StatusCode = statusCode, Elapsed = elapsed };
            case 429:
                // TODO read retry_after from the body and sleep exactly that instead of guessing later
                return new CheckResult
                {
                    Status = AccountStatus.RateLimited,
                    StatusCode = statusCode,
                    Elapsed = elapsed,
                };
            default:
                return new CheckResult
                {
                    Status = AccountStatus.Error,
                    StatusCode = statusCode,
                    Error = $"unexpected status {statusCode}",
                    Elapsed = elapsed,
                };
        }
    }

    private static DiscordProfile? ParseProfile(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return new DiscordProfile
            {
                Id = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Username = root.TryGetProperty("username", out var name) ? name.GetString() ?? "" : "",
                Avatar = root.TryGetProperty("avatar", out var avatar) ? avatar.GetString() : null,
                Email = root.TryGetProperty("email", out var email) ? email.GetString() : null,
                Phone = root.TryGetProperty("phone", out var phone) ? phone.GetString() : null,
                MfaEnabled = root.TryGetProperty("mfa_enabled", out var mfa) && mfa.GetBoolean(),
                Verified = root.TryGetProperty("verified", out var verified) && verified.GetBoolean(),
                PremiumType = root.TryGetProperty("premium_type", out var premium) ? premium.GetInt32() : 0,
                Flags = root.TryGetProperty("flags", out var flags) ? flags.GetInt32() : 0,
                Locale = root.TryGetProperty("locale", out var locale) ? locale.GetString() : null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
