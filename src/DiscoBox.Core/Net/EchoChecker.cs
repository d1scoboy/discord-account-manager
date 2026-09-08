using System.Text.Json;

namespace DiscoBox.Core.Net;

public class ProxyCheckResult
{
    public bool Ok { get; init; }

    public bool Transient { get; init; }

    public string? ExitIp { get; init; }

    public string? Service { get; init; }

    public string? Error { get; init; }

    public TimeSpan Elapsed { get; init; }
}

public static class EchoChecker
{
    // HACK: ipinfo rate-limits shared exit ips hard, hence three services in a row
    private static readonly (string Url, string Name)[] Services =
    {
        ("https://ipinfo.io/json", "ipinfo.io"),
        ("https://api.ipify.org?format=json", "api.ipify.org"),
        ("https://ifconfig.me/ip", "ifconfig.me"),
    };

    public static async Task<ProxyCheckResult> CheckAsync(ProxyEndpoint? proxy, int timeoutSeconds = 15)
    {
        using var client = HttpFactory.Create(proxy, timeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.4.0");
        var started = DateTime.UtcNow;

        foreach (var (url, name) in Services)
        {
            try
            {
                var response = await client.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode == 503 && body.Contains("no exit node", StringComparison.OrdinalIgnoreCase))
                {
                    return new ProxyCheckResult
                    {
                        Transient = true,
                        Error = $"{name}: provider has no exit node right now",
                        Elapsed = DateTime.UtcNow - started,
                    };
                }

                response.EnsureSuccessStatusCode();
                var ip = ExtractIp(body);
                if (ip is not null)
                {
                    return new ProxyCheckResult
                    {
                        Ok = true,
                        ExitIp = ip,
                        Service = name,
                        Elapsed = DateTime.UtcNow - started,
                    };
                }
            }
            catch (Exception exception)
            {
                if (name == Services[^1].Name)
                {
                    return new ProxyCheckResult
                    {
                        Error = $"{name}: {exception.Message}",
                        Elapsed = DateTime.UtcNow - started,
                    };
                }
            }
        }

        return new ProxyCheckResult
        {
            Error = "no echo service returned an ip",
            Elapsed = DateTime.UtcNow - started,
        };
    }

    private static string? ExtractIp(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.TryGetProperty("ip", out var ip))
                {
                    return ip.GetString();
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return System.Net.IPAddress.TryParse(trimmed, out _) ? trimmed : null;
    }
}
