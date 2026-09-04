namespace DiscoBox.Core.Net;

public class ProxyEndpoint
{
    public string Scheme { get; init; } = "http";

    public string Host { get; init; } = "";

    public int Port { get; init; }

    public string? User { get; init; }

    public string? Password { get; init; }

    public bool IsSocks => Scheme == "socks5";

    public string Url
    {
        get
        {
            var auth = User is null ? "" : $"{User}:{Password}@";
            return $"{Scheme}://{auth}{Host}:{Port}";
        }
    }

    public string MaskedUrl
    {
        get
        {
            var auth = User is null ? "" : "***:***@";
            return $"{Scheme}://{auth}{Host}:{Port}";
        }
    }

    public static ProxyEndpoint Parse(string url)
    {
        var trimmed = url.Trim();
        var scheme = "http";
        var body = trimmed;
        var schemeIndex = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex > 0)
        {
            scheme = trimmed[..schemeIndex].ToLowerInvariant();
            body = trimmed[(schemeIndex + 3)..];
        }

        if (scheme is not ("http" or "https" or "socks5"))
        {
            throw new FormatException($"Unsupported proxy scheme: {scheme}");
        }

        string? user = null;
        string? password = null;
        var atIndex = body.LastIndexOf('@');
        if (atIndex >= 0)
        {
            var auth = body[..atIndex];
            body = body[(atIndex + 1)..];
            var colon = auth.IndexOf(':');
            if (colon >= 0)
            {
                user = auth[..colon];
                password = auth[(colon + 1)..];
            }
            else
            {
                user = auth;
            }
        }

        var portIndex = body.LastIndexOf(':');
        if (portIndex < 0 || !int.TryParse(body[(portIndex + 1)..], out var port))
        {
            throw new FormatException($"Proxy url must end with :port — got '{trimmed}'");
        }

        var host = body[..portIndex];
        if (host.Length == 0)
        {
            throw new FormatException($"Empty proxy host in '{trimmed}'");
        }

        return new ProxyEndpoint
        {
            Scheme = scheme,
            Host = host,
            Port = port,
            User = user,
            Password = password,
        };
    }
}
