using System.Text.RegularExpressions;

namespace DiscoBox.Core.Parsing;

public class ParsedLine
{
    public int LineNumber { get; set; }

    public string Raw { get; set; } = "";

    public string? Token { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? Proxy { get; set; }

    public string? Error { get; set; }

    public bool DuplicateInFile { get; set; }

    public bool DuplicateStored { get; set; }

    public bool Ok => Error is null && Token is not null;
}

public static class AccountLineParser
{
    public const string FormatAuto = "auto";
    public const string FormatToken = "token";
    public const string FormatEmailPasswordToken = "email:password:token";
    public const string FormatTokenProxy = "token:proxy";
    public const string FormatEmailPasswordTokenProxy = "email:password:token:proxy";

    private static readonly Regex TokenRegex = new(
        @"^[\w-]{20,30}\.[\w-]{4,10}\.[\w-]{20,40}$",
        RegexOptions.Compiled);

    private static readonly Regex TokenFinder = new(
        @"(?<![\w.-])([\w-]{20,30}\.[\w-]{4,10}\.[\w-]{20,40})(?![\w.-])",
        RegexOptions.Compiled);

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    private static readonly Regex ProxyRegex = new(
        @"(?:https?|socks5)://[^\s]+",
        RegexOptions.Compiled);

    private static readonly Regex BareProxyRegex = new(
        @"^[\w.-]+:\d{2,5}$",
        RegexOptions.Compiled);

    private static readonly Regex BareProxyFinder = new(
        @"(?<![\w./-])([\w.-]+:\d{2,5})(?![\w.-])",
        RegexOptions.Compiled);

    public static ParsedLine Parse(string rawLine, int lineNumber, string format = FormatAuto)
    {
        var line = rawLine.Trim();
        var result = new ParsedLine { LineNumber = lineNumber, Raw = line };
        if (line.Length == 0 || line.StartsWith('#'))
        {
            result.Error = "empty or comment line";
            return result;
        }

        switch (format)
        {
            case FormatToken:
                result.Token = TokenRegex.IsMatch(line) ? line : null;
                break;
            case FormatEmailPasswordToken:
                ParseEmailPasswordToken(line, result);
                break;
            case FormatTokenProxy:
                ParseTokenProxy(line, result);
                break;
            case FormatEmailPasswordTokenProxy:
                ParseEmailPasswordTokenProxy(line, result);
                break;
            default:
                ParseAuto(line, result);
                break;
        }

        if (result.Error is null && result.Token is null)
        {
            result.Error = "no discord token recognized";
        }

        return result;
    }

    private static void ParseAuto(string line, ParsedLine result)
    {
        var rest = line;
        var proxy = ProxyRegex.Match(rest);
        if (proxy.Success)
        {
            result.Proxy = proxy.Value.TrimEnd(',', ';');
            rest = rest.Remove(proxy.Index, proxy.Length);
        }
        else
        {
            // FIXME bare host:port detection can misfire on password:12345 shapes, rare in real lists
            var bare = BareProxyFinder.Match(rest);
            if (bare.Success)
            {
                result.Proxy = bare.Value;
                rest = rest.Remove(bare.Index, bare.Length);
            }
        }

        var token = TokenFinder.Match(rest);
        if (token.Success)
        {
            result.Token = token.Value;
            rest = rest.Remove(token.Index, token.Length);
        }

        var parts = rest.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var passwordParts = new List<string>();
        foreach (var part in parts)
        {
            if (result.Email is null && EmailRegex.IsMatch(part))
            {
                result.Email = part;
            }
            else if (result.Proxy is null && BareProxyRegex.IsMatch(part))
            {
                result.Proxy = part;
            }
            else if (part.Length > 0)
            {
                passwordParts.Add(part);
            }
        }

        if (passwordParts.Count > 0)
        {
            result.Password = string.Join(':', passwordParts);
        }
    }

    private static void ParseEmailPasswordToken(string line, ParsedLine result)
    {
        var parts = line.Split(':');
        if (parts.Length < 3)
        {
            result.Error = "expected email:password:token";
            return;
        }

        result.Email = parts[0];
        result.Token = parts[^1];
        result.Password = string.Join(':', parts[1..^1]);
        if (!TokenRegex.IsMatch(result.Token))
        {
            result.Token = null;
        }
    }

    private static void ParseTokenProxy(string line, ParsedLine result)
    {
        var parts = line.Split(':');
        if (parts.Length < 2 || !TokenRegex.IsMatch(parts[0]))
        {
            result.Error = "expected token:proxy";
            return;
        }

        result.Token = parts[0];
        result.Proxy = string.Join(':', parts[1..]);
    }

    private static void ParseEmailPasswordTokenProxy(string line, ParsedLine result)
    {
        var parts = line.Split(':');
        if (parts.Length < 4)
        {
            result.Error = "expected email:password:token:proxy";
            return;
        }

        result.Email = parts[0];
        var tokenIndex = Array.FindIndex(parts, p => TokenRegex.IsMatch(p));
        if (tokenIndex < 1)
        {
            return;
        }

        result.Token = parts[tokenIndex];
        result.Password = string.Join(':', parts[1..tokenIndex]);
        if (tokenIndex + 1 < parts.Length)
        {
            result.Proxy = string.Join(':', parts[(tokenIndex + 1)..]);
        }
    }

    public static List<ParsedLine> ParseAll(string text, string format = FormatAuto)
    {
        var results = new List<ParsedLine>();
        var lineNumber = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNumber++;
            results.Add(Parse(raw, lineNumber, format));
        }

        return results;
    }

    public static void MarkDuplicates(List<ParsedLine> lines, IEnumerable<string> existingTokens)
    {
        var known = new HashSet<string>(existingTokens);
        var seen = new HashSet<string>();
        foreach (var line in lines.Where(l => l.Ok))
        {
            if (!seen.Add(line.Token!))
            {
                line.DuplicateInFile = true;
            }
            else if (!known.Add(line.Token!))
            {
                line.DuplicateStored = true;
            }
        }
    }
}
